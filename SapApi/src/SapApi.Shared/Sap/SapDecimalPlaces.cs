using System.Globalization;

namespace SapApi.Shared.Sap;

/// <summary>
/// SAP Business One Administration → Decimal Places for this company:
/// Amounts 2, Prices 3, Rates 4, Quantities 4, Percent 2, Units 2.
/// </summary>
public static class SapDecimalPlaces
{
    public const int Amounts = 2;
    public const int Prices = 3;
    public const int Rates = 4;
    public const int Quantities = 4;
    public const int Percent = 2;
    public const int Units = 2;

    public static double Round(double value, int decimals) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    public static string Format(double value, int decimals) =>
        Round(value, decimals).ToString("0." + new string('#', Math.Max(decimals, 0)), CultureInfo.InvariantCulture);
}
