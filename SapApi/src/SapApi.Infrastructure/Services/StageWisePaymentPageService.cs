using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Responses;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services;

public class StageWisePaymentPageService(
    AppDbContext db,
    SapPurchaseOrderService purchaseOrderService,
    SapVendorPaymentService vendorPaymentService,
    SapMasterDataService masterDataService,
    ISapLoginService sapLogin,
    ICurrentCompanyDbAccessor companyDbAccessor)
{
    private string CompanyDb => companyDbAccessor.GetCompanyDbName();

    public async Task<StageWisePaymentPageDataResponse?> LoadPageDataAsync(int poDocEntry, CancellationToken cancellationToken = default)
    {
        await sapLogin.SapLoginAsync(cancellationToken);

        var po = await purchaseOrderService.GetPurchaseOrderForPaymentPage(poDocEntry.ToString(), cancellationToken);
        if (po?.Error?.Message?.Value is { } sapError)
            throw new ApiErrorException("SYS-01", sapError);
        if (po is null || po.DocEntry is null)
            return null;

        var docNum = po.DocNum ?? poDocEntry;
        var tableRecords = await db.StageWisePayments
            .AsNoTracking()
            .Where(x => x.CompanyDb == CompanyDb && x.DocNumber == docNum)
            .OrderByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        var activeRecords = tableRecords.Where(x => x.Status != StageWisePaymentStatus.Cancelled).ToList();
        var totalBasic = (po.DocTotal ?? 0) - (po.VatSum ?? 0);

        var projectNameTask = masterDataService.GetProjectNameAsync(po.Project, cancellationToken);
        var apInvoicesTask = LoadApInvoicesAsync(po, cancellationToken);
        var wtCodesTask = LoadWithholdingTaxCodesAsync(po.CardCode, cancellationToken);

        await Task.WhenAll(projectNameTask, apInvoicesTask, wtCodesTask);

        var banks = Constants.BankAccounts.GetBanksForBplId(po.BPLId)
            .Select(b => new StageWisePaymentBankOption { Key = b.Key, Value = b.Value })
            .ToList();

        return new StageWisePaymentPageDataResponse
        {
            PurchaseOrder = po,
            ProjectName = await projectNameTask,
            TotalBasic = totalBasic,
            BalancePayment = StageWisePaymentCalculations.GetBalancePayment(po, activeRecords),
            PaymentTerms = po.CreateUdfList(),
            TableRecords = tableRecords.Select(MapRecord).ToList(),
            ActiveRecords = activeRecords.Select(MapRecord).ToList(),
            Banks = banks,
            BankLabels = Constants.BankAccounts.Banks,
            ApInvoices = await apInvoicesTask,
            WithholdingTaxCodes = await wtCodesTask,
            PaymentSummary = StageWisePaymentCalculations.BuildPaymentSummary(po, activeRecords),
        };
    }

    public async Task<SapPurchaseInvoicesResponse?> ResolveApInvoiceAsync(
        SapPurchaseOrdersResponse po,
        string? apInvoiceDocEntry,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(apInvoiceDocEntry))
            return null;

        var apInvoices = await LoadApInvoicesAsync(po, cancellationToken);
        return apInvoices.FirstOrDefault(x => x.DocEntry.ToString() == apInvoiceDocEntry);
    }

    private async Task<List<SapPurchaseInvoicesResponse>> LoadApInvoicesAsync(
        SapPurchaseOrdersResponse po,
        CancellationToken cancellationToken)
    {
        if (po.DocEntry is null)
            return [];

        var cardCode = po.CardCode ?? string.Empty;
        var poDocEntry = po.DocEntry.Value;

        var directInvoices = await vendorPaymentService.GetApInvoicesForPurchaseOrder(cardCode, poDocEntry);
        if (directInvoices?.Error is not null)
            return await LoadApInvoicesFallbackAsync(po, cancellationToken);

        var direct = directInvoices?.Value ?? [];
        if (direct.Count > 0)
            return direct;

        var grpos = await vendorPaymentService.GetGrposForPurchaseOrder(cardCode, poDocEntry);
        if (grpos?.Error is not null)
            return await LoadApInvoicesFallbackAsync(po, cancellationToken);

        var grpoDocEntries = grpos?.Value?
            .Where(x => x.DocEntry.HasValue)
            .Select(x => x.DocEntry!.Value)
            .Distinct()
            .ToList() ?? [];

        if (grpoDocEntries.Count == 0)
            return [];

        var grpoInvoices = await vendorPaymentService.GetApInvoicesForGrpos(cardCode, grpoDocEntries);
        if (grpoInvoices?.Error is not null)
            return await LoadApInvoicesFallbackAsync(po, cancellationToken);

        return grpoInvoices?.Value ?? [];
    }

    private async Task<List<SapPurchaseInvoicesResponse>> LoadApInvoicesFallbackAsync(
        SapPurchaseOrdersResponse po,
        CancellationToken cancellationToken)
    {
        var allApInvoicesTask = vendorPaymentService.GetApInvoices(po.CardCode ?? string.Empty);
        var grposTask = vendorPaymentService.GetGrpo(po.CardCode ?? string.Empty);
        await Task.WhenAll(allApInvoicesTask, grposTask);

        var apInvoices = allApInvoicesTask.Result?.Value?
            .Where(x => x.BaseEntry == po.DocEntry && x.DocumentStatus == "bost_Open")
            .ToList() ?? [];

        if (apInvoices.Count > 0)
            return apInvoices;

        var relatedGrpos = grposTask.Result?.Value?
            .Where(x => x.BaseType == 22 && x.BaseEntry == po.DocEntry)
            .ToList() ?? [];

        foreach (var grpo in relatedGrpos)
        {
            apInvoices.AddRange(allApInvoicesTask.Result?.Value?
                .Where(x => x.BaseType == 20 && x.BaseEntry == grpo.DocEntry)
                .ToList() ?? []);
        }

        return apInvoices;
    }

    private async Task<List<StageWisePaymentWtCodeOption>> LoadWithholdingTaxCodesAsync(
        string? cardCode,
        CancellationToken cancellationToken)
    {
        var partner = await masterDataService.GetBusinessPartnerByCardCodeAsync(cardCode ?? string.Empty, cancellationToken);
        var wtCodes = partner?.WithholdingTaxDataCollectionResponse?
            .Select(wt => wt.WtCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        if (wtCodes.Count == 0)
            return [];

        var wtMaster = await masterDataService.GetWithholdingTaxByCodesAsync(wtCodes, cancellationToken);
        return wtCodes.Select(code => new StageWisePaymentWtCodeOption
        {
            WtCode = code,
            WtName = wtMaster.FirstOrDefault(m => m.WtCode == code)?.WtName,
            Rate = wtMaster.FirstOrDefault(m => m.WtCode == code)?.Rate,
        }).ToList();
    }

    private static StageWisePaymentRecordDto MapRecord(StageWisePayment record) => new()
    {
        Id = record.Id,
        PaymentTermsType = record.PaymentTermsType,
        StageDesc = record.StageDesc,
        Bank = record.Bank,
        UtrNo = record.UtrNo,
        UtrDate = record.UtrDate,
        ApprovalRequestId = record.ApprovalRequestId,
        ApInvoiceDocEntry = record.ApInvoiceDocEntry,
        ApDownPaymentInvoiceEntryNumber = record.ApDownPaymentInvoiceEntryNumber,
        WtCode = record.WtCode,
        GrossAmount = record.GrossAmount,
        GstAmount = record.GstAmount,
        Tds = record.Tds,
        Status = MapStatus(record.Status),
        DocNumber = record.DocNumber,
        CreatedOn = record.CreatedOn,
        LastModifiedOn = record.LastModifiedOn,
    };

    private static string MapStatus(StageWisePaymentStatus status) => status switch
    {
        StageWisePaymentStatus.PendingApproval => "Approval Pending",
        StageWisePaymentStatus.Approved => "Approved",
        StageWisePaymentStatus.Added => "Created",
        StageWisePaymentStatus.Cancelled => "Cancelled",
        _ => status.ToString(),
    };
}
