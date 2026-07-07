namespace SapApi.Shared.Helpers;

public static class AmountInWords
{
    public static string ConvertToWords(double amount)
    {
        var rupees = (long)Math.Floor(amount);
        var paise = (int)((amount - rupees) * 100);

        var result = $"{NumberToWords(rupees)} Rupees";
        if (paise > 0)
            result += $" and {NumberToWords(paise)} Paise";

        return result + " Only";
    }

    static string NumberToWords(long number)
    {
        if (number == 0)
            return "Zero";

        string[] units =
        [
            "", "One", "Two", "Three", "Four", "Five", "Six",
            "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve",
            "Thirteen", "Fourteen", "Fifteen", "Sixteen",
            "Seventeen", "Eighteen", "Nineteen"
        ];

        string[] tens =
        [
            "", "", "Twenty", "Thirty", "Forty", "Fifty",
            "Sixty", "Seventy", "Eighty", "Ninety"
        ];

        if (number < 20)
            return units[number];

        if (number < 100)
            return tens[number / 10] + (number % 10 > 0 ? " " + units[number % 10] : "");

        if (number < 1000)
            return units[number / 100] + " Hundred"
                   + (number % 100 > 0 ? " " + NumberToWords(number % 100) : "");

        if (number < 100000)
            return NumberToWords(number / 1000) + " Thousand"
                   + (number % 1000 > 0 ? " " + NumberToWords(number % 1000) : "");

        if (number < 10000000)
            return NumberToWords(number / 100000) + " Lakh"
                   + (number % 100000 > 0 ? " " + NumberToWords(number % 100000) : "");

        return NumberToWords(number / 10000000) + " Crore"
               + (number % 10000000 > 0 ? " " + NumberToWords(number % 10000000) : "");
    }
}
