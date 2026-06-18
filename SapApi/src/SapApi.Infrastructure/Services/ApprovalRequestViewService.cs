using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SapApi.Domain.Entities;
using SapApi.Infrastructure.Identity;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared;
using SapApi.Shared.Enums;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services;

public class ApprovalRequestViewService(
    AppDbContext db,
    SapMasterDataService masterDataService,
    SapPurchaseOrderService purchaseOrderService,
    IHttpContextAccessor httpContext)
{
    public async Task<ApprovalRequest?> GetRequestAsync(int requestId, CancellationToken cancellationToken = default)
    {
        var userId = httpContext.GetUserIdAsync();
        var request = await db.ApprovalRequests.AsNoTracking()
            .Include(x => x.RequesterUser)
            .Include(x => x.Policy)
            .Include(x => x.UserApprovals).ThenInclude(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);

        if (request is null || !userId.HasValue)
            return null;

        var userApproval = request.UserApprovals.FirstOrDefault(x => x.UserId == userId);
        if (userApproval is not null)
        {
            var maxPriority = request.UserApprovals.Max(x => x.Priority);
            request.IsLastApproval = userApproval.Priority == maxPriority;
        }

        return request;
    }

    public async Task<ApprovalPaymentContextResponse?> GetPaymentContextAsync(
        int requestId,
        CancellationToken cancellationToken = default)
    {
        var request = await db.ApprovalRequests.AsNoTracking()
            .Include(x => x.UserApprovals).ThenInclude(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);

        if (request is null || request.DocumentType != ApprovalDocumentType.Payments)
            return null;

        var paymentBody = string.IsNullOrEmpty(request.RequestBody)
            ? null
            : JsonSerializer.Deserialize<SapVendorPaymentRequests>(request.RequestBody);

        if (paymentBody is null)
            return null;

        var currentUserId = httpContext.GetUserIdAsync() ?? 0;
        var po = await purchaseOrderService.GetPurchaseOrders(request.SupportingData ?? "0");
        var vendor = await masterDataService.GetBusinessPartnerByCardCodeAsync(paymentBody.CardCode ?? string.Empty, cancellationToken);
        var projectName = await masterDataService.GetProjectNameAsync(paymentBody.ProjectCode, cancellationToken);

        string? branch = null;
        if (paymentBody.BPLId is not null)
        {
            var businessPlace = await masterDataService.GetBusinessPlaceByIdAsync(paymentBody.BPLId, cancellationToken);
            branch = businessPlace?.BplName ?? paymentBody.BPLId.ToString();
        }

        var stagePayment = await db.StageWisePayments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ApprovalRequestId != null
                && (x.ApprovalRequestId == requestId.ToString()
                    || x.ApprovalRequestId.StartsWith(requestId + ",")
                    || x.ApprovalRequestId.EndsWith("," + requestId)
                    || x.ApprovalRequestId.Contains("," + requestId + ",")),
                cancellationToken);

        var poDocNum = po?.DocNum;
        var stagePayments = poDocNum is not null
            ? await db.StageWisePayments.AsNoTracking()
                .Where(x => x.DocNumber == poDocNum && x.Status != StageWisePaymentStatus.Cancelled)
                .ToListAsync(cancellationToken)
            : [];

        var paymentTerms = po?.CreateUdfList() ?? [];

        return new ApprovalPaymentContextResponse
        {
            VendorDisplay = $"{paymentBody.CardCode} - {vendor?.CardName ?? paymentBody.CardCode}",
            PoDetails = po is not null
                ? $"{po.DocNum} - {po.DocDate:dd/MM/yyyy}"
                : paymentBody.PoNumber,
            ProjectName = !string.IsNullOrWhiteSpace(projectName)
                ? $"{paymentBody.ProjectCode} - {projectName}"
                : paymentBody.ProjectCode,
            BankAccount = Constants.BankAccounts.Banks.TryGetValue(paymentBody.TransferAccount ?? string.Empty, out var bankLabel)
                ? bankLabel
                : paymentBody.TransferAccount,
            Branch = branch,
            TransferAmount = double.TryParse(paymentBody.TransferSum, out var amount) ? amount : null,
            UtrNo = stagePayment?.UtrNo,
            UtrDate = stagePayment?.UtrDate,
            PreviousApprovals = request.UserApprovals
                .Where(x => x.UserId != currentUserId
                    && (x.ApprovalStatus == ApprovalStatus.Approved || x.ApprovalStatus == ApprovalStatus.Forwarded))
                .OrderBy(x => x.Priority)
                .ThenBy(x => x.ActionDate)
                .Select(x => new ApprovalTimelineItemDto
                {
                    ApproverName = x.User?.FullName ?? x.User?.UserName,
                    ActionDate = x.ActionDate,
                    Comment = x.Comment,
                    Status = x.ApprovalStatus.ToString(),
                })
                .ToList(),
            StageWisePayments = BuildSummaryRows(stagePayments, paymentTerms),
            PaymentTerms = paymentTerms.Select(t => new PaymentTermSummaryItemDto
            {
                Id = t.Id,
                Desc = t.Desc,
                Type = t.Type,
            }).ToList(),
        };
    }

    private static List<StageWisePaymentSummaryItemDto> BuildSummaryRows(
        List<StageWisePayment> payments,
        List<PaymentTermsUdf> paymentTerms)
    {
        var rows = payments.Select(p =>
        {
            var term = paymentTerms.FirstOrDefault(x => x.Id == p.PaymentTermsType);
            var netBasic = (p.GrossAmount ?? 0) - (p.Tds ?? 0);
            var gross = (p.GrossAmount ?? 0) + (p.GstAmount ?? 0);
            return new StageWisePaymentSummaryItemDto
            {
                RequestId = p.ApprovalRequestId ?? string.Empty,
                PaymentStage = !string.IsNullOrWhiteSpace(p.StageDesc)
                    ? p.StageDesc
                    : term?.Desc ?? p.Stage.ToString(),
                NetBasicAmount = netBasic,
                TdsAmount = p.Tds ?? 0,
                GstAmount = p.GstAmount ?? 0,
                GrossAmount = gross,
                Status = MapPaymentStatus(p.Status),
            };
        }).ToList();

        if (rows.Count > 0)
        {
            rows.Add(new StageWisePaymentSummaryItemDto
            {
                IsTotalRow = true,
                RequestId = "Total",
                NetBasicAmount = rows.Sum(r => r.NetBasicAmount),
                TdsAmount = rows.Sum(r => r.TdsAmount),
                GstAmount = rows.Sum(r => r.GstAmount),
                GrossAmount = rows.Sum(r => r.GrossAmount),
            });
        }

        return rows;
    }

    private static string MapPaymentStatus(StageWisePaymentStatus status) => status switch
    {
        StageWisePaymentStatus.PendingApproval => "Approval Pending",
        StageWisePaymentStatus.Approved => "Approved",
        StageWisePaymentStatus.Added => "Created",
        StageWisePaymentStatus.Cancelled => "Cancelled",
        _ => status.ToString(),
    };
}
