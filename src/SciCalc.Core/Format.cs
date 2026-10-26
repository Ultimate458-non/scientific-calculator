using System.Globalization;

namespace SciCalc.Core;

/// <summary>Formats a number the way a 10-digit calculator display shows it.</summary>
public static class Format
{
    public const int Digits = 10;

    public static string Number(double v)
    {
        if (v == 0) return "0";
        var neg = v < 0;
        var a = Math.Abs(v);
        // Round to 10 significant digits first.
        var rounded = double.Parse(a.ToString("E" + (Digits - 1), CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        string body;
        if (rounded >= 1e10 || rounded < 1e-9)
        {
            var parts = rounded.ToString("E" + (Digits - 1), CultureInfo.InvariantCulture).Split('E');
            var mant = parts[0].Contains('.') ? parts[0].TrimEnd('0').TrimEnd('.') : parts[0];
            var exp = int.Parse(parts[1], CultureInfo.InvariantCulture);
            body = $"{mant}E{(exp < 0 ? "−" : "")}{Math.Abs(exp)}";
        }
        else
        {
            body = ((decimal)rounded).ToString("0.############################", CultureInfo.InvariantCulture);
        }
        return neg ? "−" + body : body;
    }
}
