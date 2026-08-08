using SapApi.Shared.Responses.Sap;

namespace SapApi.Shared.Sap;

/// <summary>
/// Pure helpers for matching SAP document numbering series to branch + posting date.
/// PBBPL uses India financial-year period indicators (FY26-27 = Apr 2026–Mar 2027).
/// </summary>
public static class SapDocumentSeriesResolver
{
    /// <summary>
    /// India FY period indicator used on PBBPL UAT/prod numbering series (e.g. FY26-27).
    /// </summary>
    public static string GetIndiaFinancialYearPeriodIndicator(DateTime docDate)
    {
        var startYear = docDate.Month >= 4 ? docDate.Year : docDate.Year - 1;
        var yy = startYear % 100;
        var yyNext = (startYear + 1) % 100;
        return $"FY{yy:D2}-{yyNext:D2}";
    }

    public static bool IsUnlocked(SapDocumentSeriesEntry series) =>
        !string.Equals(series.Locked, Constants.SapBoolean.SapTrue, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Picks the unlocked series for the given BPL + period. Returns null when none match
    /// (SAP create then fails with ODBC -2028).
    /// </summary>
    public static SapDocumentSeriesEntry? FindSeries(
        IEnumerable<SapDocumentSeriesEntry>? series,
        int bplId,
        string periodIndicator)
    {
        if (series is null || string.IsNullOrWhiteSpace(periodIndicator))
            return null;

        return series
            .Where(IsUnlocked)
            .Where(s => s.BPLID == bplId)
            .Where(s => string.Equals(s.PeriodIndicator, periodIndicator, StringComparison.OrdinalIgnoreCase))
            .Where(s => !string.Equals(s.IsManual, Constants.SapBoolean.SapTrue, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Series)
            .FirstOrDefault();
    }

    public static string FormatMissingSeriesMessage(
        string documentLabel,
        int bplId,
        string periodIndicator,
        DateTime docDate) =>
        $"No {documentLabel} numbering series is configured in SAP for branch BPL {bplId} " +
        $"and financial year {periodIndicator} (posting date {docDate:yyyy-MM-dd}). " +
        "Ask a SAP administrator to create the series for this branch/year " +
        "(Administration → System Initialization → Document Numbering), then retry.";
}
