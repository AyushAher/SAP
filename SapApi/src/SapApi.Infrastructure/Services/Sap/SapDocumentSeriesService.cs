using SapApi.Domain.Interfaces;
using SapApi.Shared;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;
using SapApi.Shared.Sap;

namespace SapApi.Infrastructure.Services.Sap;

/// <summary>
/// Resolves SAP document numbering series (NNM1) via Service Layer SeriesService.
/// Explicit Series on the payload bypasses broken per-user default series (common cause of
/// "To generate this document, first define the numbering series…").
/// </summary>
public class SapDocumentSeriesService(IHttpRequestHandler requestHandler)
{
    public async Task EnsurePurchaseOrderSeriesAsync(
        SapPurchaseOrdersResponse payload,
        CancellationToken cancellationToken = default)
    {
        if (payload.Series is > 0)
            return;

        var docDate = payload.DocDate ?? DateTime.UtcNow.Date;
        payload.DocDate ??= docDate;
        var bplId = payload.BPLId ?? 1;
        var period = SapDocumentSeriesResolver.GetIndiaFinancialYearPeriodIndicator(docDate);

        var seriesList = await GetDocumentSeriesAsync(Constants.SapDocumentObject.PurchaseOrder, cancellationToken);
        var match = SapDocumentSeriesResolver.FindSeries(seriesList, bplId, period);
        if (match is null)
        {
            throw new ApiErrorException(
                BaseErrorCodes.ValidationFailed,
                SapDocumentSeriesResolver.FormatMissingSeriesMessage(
                    "Purchase Order (OPOR)", bplId, period, docDate));
        }

        payload.Series = match.Series;
    }

    /// <summary>
    /// Sets Series on A/P Down Payment Request (PurchaseDownPayments / object 204) for BPL + DocDate FY.
    /// Also ensures DocDate/TaxDate are present so series selection matches what SAP will post.
    /// </summary>
    public async Task EnsurePurchaseDownPaymentSeriesAsync(
        SapPurchaseDownPaymentRequest payload,
        CancellationToken cancellationToken = default)
    {
        var docDate = payload.DocDate?.Date ?? DateTime.UtcNow.Date;
        payload.DocDate ??= docDate;
        payload.TaxDate ??= docDate;

        if (payload.Series is > 0)
            return;

        var bplId = payload.BPLId ?? 1;
        var period = SapDocumentSeriesResolver.GetIndiaFinancialYearPeriodIndicator(docDate);

        var seriesList = await GetDocumentSeriesAsync(
            Constants.SapDocumentObject.PurchaseDownPayment, cancellationToken);
        var match = SapDocumentSeriesResolver.FindSeries(seriesList, bplId, period);
        if (match is null)
        {
            throw new ApiErrorException(
                BaseErrorCodes.ValidationFailed,
                SapDocumentSeriesResolver.FormatMissingSeriesMessage(
                    "A/P Down Payment Request (ODPO)", bplId, period, docDate));
        }

        payload.Series = match.Series;
    }

    public async Task<IReadOnlyList<SapDocumentSeriesEntry>> GetDocumentSeriesAsync(
        string documentObjectCode,
        CancellationToken cancellationToken = default)
    {
        var body = new
        {
            DocumentTypeParams = new
            {
                Document = documentObjectCode,
            },
        };

        var response = await requestHandler.PostAsync<object, SapDocumentSeriesListResponse>(
            Constants.SapApiUrls.SeriesServiceGetDocumentSeries,
            body,
            cancellationToken);

        return response?.Value ?? [];
    }
}
