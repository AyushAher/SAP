using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
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
    IHttpContextAccessor httpContext,
    ICurrentCompanyDbAccessor companyDbAccessor)
{
    private static readonly JsonSerializerOptions RequestBodyJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private string CompanyDb => companyDbAccessor.GetCompanyDbName();

    public async Task<ApprovalRequest?> GetRequestAsync(int requestId, CancellationToken cancellationToken = default)
    {
        var userId = httpContext.GetUserIdAsync();
        var request = await db.ApprovalRequests.AsNoTracking()
            .Include(x => x.RequesterUser)
            .Include(x => x.Policy)
            .Include(x => x.UserApprovals).ThenInclude(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == requestId && x.CompanyDb == CompanyDb, cancellationToken);

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
            .FirstOrDefaultAsync(x => x.Id == requestId && x.CompanyDb == CompanyDb, cancellationToken);

        if (request is null)
            return null;

        if (request.DocumentType is not (
            ApprovalDocumentType.Payments
            or ApprovalDocumentType.StagewisePayments_DP))
        {
            return null;
        }

        var paymentFields = ResolvePaymentFields(request);
        if (paymentFields is null)
            return null;

        var currentUserId = httpContext.GetUserIdAsync() ?? 0;
        var po = await TryGetPurchaseOrderAsync(request.SupportingData, cancellationToken);

        var cardCode = FirstNonEmpty(paymentFields.CardCode, po?.CardCode);
        var projectCode = FirstNonEmpty(paymentFields.ProjectCode, po?.Project);
        var bplId = paymentFields.BplId ?? po?.BPLId;

        var vendorName = await TryGetVendorNameAsync(cardCode, cancellationToken)
            ?? po?.CardName;
        var projectName = await TryGetProjectNameAsync(projectCode, cancellationToken);
        var branchName = await TryGetBranchNameAsync(bplId, cancellationToken);

        var stagePayment = await db.StageWisePayments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyDb == CompanyDb && x.ApprovalRequestId != null
                && (x.ApprovalRequestId == requestId.ToString()
                    || x.ApprovalRequestId.StartsWith(requestId + ",")
                    || x.ApprovalRequestId.EndsWith("," + requestId)
                    || x.ApprovalRequestId.Contains("," + requestId + ",")),
                cancellationToken);

        var poDocNum = po?.DocNum;
        var stagePayments = request.PurchaseOrderId is not null
            ? await db.StageWisePayments.AsNoTracking()
                .Where(x => x.CompanyDb == CompanyDb
                    && x.PurchaseOrderId == request.PurchaseOrderId
                    && x.Status != StageWisePaymentStatus.Cancelled)
                .ToListAsync(cancellationToken)
            : poDocNum is not null
                ? await db.StageWisePayments.AsNoTracking()
                    .Where(x => x.CompanyDb == CompanyDb && x.DocNumber == poDocNum && x.Status != StageWisePaymentStatus.Cancelled)
                    .ToListAsync(cancellationToken)
                : [];

        var paymentTerms = po?.CreateUdfList() ?? [];

        return new ApprovalPaymentContextResponse
        {
            VendorDisplay = FormatCodeWithName(cardCode, vendorName),
            PoDetails = po is not null
                ? $"{po.DocNum} - {po.DocDate:dd/MM/yyyy}"
                : paymentFields.PoNumber,
            ProjectName = FormatCodeWithName(projectCode, projectName),
            BankAccount = ResolveBankLabel(paymentFields.TransferAccount),
            Branch = branchName ?? bplId?.ToString(),
            TransferAmount = paymentFields.TransferAmount,
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

    private static PaymentFields? ResolvePaymentFields(ApprovalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestBody))
            return null;

        if (request.DocumentType == ApprovalDocumentType.Payments)
        {
            var paymentBody = JsonSerializer.Deserialize<SapVendorPaymentRequests>(
                request.RequestBody, RequestBodyJsonOptions);
            if (paymentBody is null)
                return null;

            double? transferAmount = double.TryParse(paymentBody.TransferSum, out var amount) ? amount : null;
            return new PaymentFields(
                paymentBody.CardCode,
                paymentBody.ProjectCode,
                paymentBody.PoNumber,
                paymentBody.BPLId,
                paymentBody.TransferAccount,
                transferAmount);
        }

        var downPaymentBody = JsonSerializer.Deserialize<SapPurchaseDownPaymentRequest>(
            request.RequestBody, RequestBodyJsonOptions);
        if (downPaymentBody is null)
            return null;

        return new PaymentFields(
            downPaymentBody.CardCode,
            ProjectCode: null,
            PoNumber: null,
            downPaymentBody.BPLId,
            TransferAccount: null,
            TransferAmount: downPaymentBody.DocTotal
                ?? downPaymentBody.DownPaymentAmount
                ?? downPaymentBody.DownPayment);
    }

    private async Task<SapPurchaseOrdersResponse?> TryGetPurchaseOrderAsync(
        string? supportingData,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(supportingData))
            return null;

        try
        {
            return await purchaseOrderService.GetPurchaseOrderForPaymentPage(supportingData, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> TryGetVendorNameAsync(string? cardCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cardCode))
            return null;

        try
        {
            var vendor = await masterDataService.GetBusinessPartnerByCardCodeAsync(
                cardCode, cancellationToken: cancellationToken);
            return vendor?.CardName;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> TryGetProjectNameAsync(string? projectCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectCode))
            return null;

        try
        {
            return await masterDataService.GetProjectNameAsync(projectCode, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> TryGetBranchNameAsync(int? bplId, CancellationToken cancellationToken)
    {
        if (bplId is null)
            return null;

        try
        {
            var businessPlace = await masterDataService.GetBusinessPlaceByIdAsync(
                bplId, cancellationToken: cancellationToken);
            if (!string.IsNullOrWhiteSpace(businessPlace?.BplName))
                return businessPlace.BplName;
        }
        catch
        {
            // Fall through to local branch options / id.
        }

        try
        {
            var branches = await masterDataService.ListBranchOptionsAsync(cancellationToken);
            var match = branches.FirstOrDefault(b => b.Id == bplId.Value);
            if (!string.IsNullOrWhiteSpace(match?.Name))
                return match.Name;
        }
        catch
        {
            // Ignore and fall back to the numeric id.
        }

        return null;
    }

    private static string? ResolveBankLabel(string? transferAccount)
    {
        if (string.IsNullOrWhiteSpace(transferAccount))
            return null;

        return Constants.BankAccounts.Banks.TryGetValue(transferAccount, out var bankLabel)
            ? bankLabel
            : transferAccount;
    }

    private static string? FormatCodeWithName(string? code, string? name)
    {
        if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(name))
            return null;
        if (string.IsNullOrWhiteSpace(code))
            return name;
        if (string.IsNullOrWhiteSpace(name) || string.Equals(code, name, StringComparison.OrdinalIgnoreCase))
            return code;
        return $"{code} - {name}";
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

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

    private sealed record PaymentFields(
        string? CardCode,
        string? ProjectCode,
        string? PoNumber,
        int? BplId,
        string? TransferAccount,
        double? TransferAmount);
}
