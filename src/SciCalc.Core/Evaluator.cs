using System.Globalization;

namespace SciCalc.Core;

public enum AngleMode { Deg, Rad, Gra }

/// <summary>Raised for anything the calculator cannot compute. Message is what the LCD shows.</summary>
public sealed class CalcException : Exception
{
    public CalcException(string message) : base(message) { }
    public static CalcException Syntax() => new("Syntax ERROR");
    public static CalcException Math() => new("Math ERROR");
}

/// <summary>
/// Recursive-descent evaluator for the expression shown on the display.
/// Precedence, lowest to highest: + -  |  × ÷ and implicit multiply  |  nPr nCr  |  unary -  |  ^ and root  |  postfix (² ³ ⁻¹ ! %)
/// </summary>
public sealed class Evaluator
{
    private readonly AngleMode _angle;
    private readonly double _ans;
    private readonly double _memory;
    private string _s = "";
    private int _i;

    public Evaluator(AngleMode angle = AngleMode.Deg, double ans = 0, double memory = 0)
    {
        _angle = angle;
        _ans = ans;
        _memory = memory;
    }

    public double Evaluate(string expression)
    {
        _s = expression;
        _i = 0;
        if (string.IsNullOrWhiteSpace(_s)) throw CalcException.Syntax();
        var v = ParseSum();
        SkipSpace();
        if (_i < _s.Length) throw CalcException.Syntax();
        return Check(v);
    }

    // ---------- helpers ----------

    private static double Check(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) throw CalcException.Math();
        return v;
    }

    private void SkipSpace() { while (_i < _s.Length && _s[_i] == ' ') _i++; }

    private bool Peek(string t)
    {
        SkipSpace();
        return string.CompareOrdinal(_s, _i, t, 0, t.Length) == 0;
    }

    private bool Eat(string t)
    {
        if (!Peek(t)) return false;
        _i += t.Length;
        return true;
    }

    private bool AtEnd { get { SkipSpace(); return _i >= _s.Length; } }

    // ---------- grammar ----------

    private double ParseSum()
    {
        var v = ParseProduct();
        while (true)
        {
            if (Eat("+")) v += ParseProduct();
            else if (Eat("−") || Eat("-")) v -= ParseProduct();
            else return v;
        }
    }

    private bool StartsOperand()
    {
        SkipSpace();
        if (_i >= _s.Length) return false;
        var c = _s[_i];
        return char.IsDigit(c) || c == '.' || c == '(' || c == 'π' || c == '√' || c == '∛' || char.IsLetter(c) && !IsBinaryLetter();
    }

    // P and C are the nPr / nCr operators when they follow an operand.
    private bool IsBinaryLetter()
    {
        var c = _s[_i];
        return (c == 'P' || c == 'C') && !(_i + 1 < _s.Length && char.IsLetter(_s[_i + 1]));
    }

    private double ParseProduct()
    {
        var v = ParseCombinatorics();
        while (true)
        {
            if (Eat("×") || Eat("*")) v *= ParseCombinatorics();
            else if (Eat("÷") || Eat("/"))
            {
                var d = ParseCombinatorics();
                if (d == 0) throw CalcException.Math();
                v /= d;
            }
            else if (StartsOperand()) v *= ParseCombinatorics(); // implicit multiplication: 2π, 3(4+1), 2sin(30)
            else return v;
        }
    }

    private double ParseCombinatorics()
    {
        var v = ParseUnary();
        while (true)
        {
            SkipSpace();
            if (_i < _s.Length && _s[_i] == 'P' && IsBinaryLetter()) { _i++; v = Permutations(v, ParseUnary()); }
            else if (_i < _s.Length && _s[_i] == 'C' && IsBinaryLetter()) { _i++; v = Combinations(v, ParseUnary()); }
            else return v;
        }
    }

    private double ParseUnary()
    {
        if (Eat("−") || Eat("-")) return -ParseUnary();
        if (Eat("+")) return ParseUnary();
        return ParsePower();
    }

    private double ParsePower()
    {
        var v = ParsePostfix();
        if (Eat("^"))
        {
            var e = ParsePowerOperand();
            return Pow(v, e);
        }
        if (Eat("ˣ√")) // n ˣ√ x  = the n-th root of x
        {
            var x = ParsePowerOperand();
            if (v == 0) throw CalcException.Math();
            return Root(x, v);
        }
        return v;
    }

    // Right-associative, and allows a sign: 2^-3
    private double ParsePowerOperand()
    {
        if (Eat("−") || Eat("-")) return -ParsePowerOperand();
        if (Eat("+")) return ParsePowerOperand();
        return ParsePower();
    }

    private double ParsePostfix()
    {
        var v = ParsePrimary();
        while (true)
        {
            if (Eat("²")) v = Check(v * v);
            else if (Eat("³")) v = Check(v * v * v);
            else if (Eat("⁻¹")) { if (v == 0) throw CalcException.Math(); v = 1 / v; }
            else if (Eat("!")) v = Factorial(v);
            else if (Eat("%")) v /= 100;
            else return v;
        }
    }

    private double ParsePrimary()
    {
        SkipSpace();
        if (_i >= _s.Length) throw CalcException.Syntax();
        var c = _s[_i];

        if (char.IsDigit(c) || c == '.') return ParseNumber();
        if (c == '(')
        {
            _i++;
            var v = ParseSum();
            if (!Eat(")")) { if (!AtEnd) throw CalcException.Syntax(); } // a missing ) at the very end is allowed, like the real thing
            return v;
        }
        if (c == 'π') { _i++; return Math.PI; }
        if (c == '√') { _i++; return Sqrt(ParseRadicand()); }
        if (c == '∛') { _i++; return Root(ParseRadicand(), 3); }
        if (char.IsLetter(c)) return ParseName();
        throw CalcException.Syntax();
    }

    // √( … ) or √9 : the thing under the root sign.
    private double ParseRadicand()
    {
        if (Peek("(")) return ParsePrimary();
        return ParsePostfix();
    }

    private double ParseNumber()
    {
        var start = _i;
        var dot = false;
        while (_i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] == '.'))
        {
            if (_s[_i] == '.') { if (dot) throw CalcException.Syntax(); dot = true; }
            _i++;
        }
        var text = _s[start.._i];
        if (text == ".") throw CalcException.Syntax();
        var value = double.Parse(text, CultureInfo.InvariantCulture);

        // × 10 to the power: 1.5E3
        if (_i < _s.Length && _s[_i] == 'E')
        {
            var save = _i;
            _i++;
            var sign = 1;
            if (_i < _s.Length && (_s[_i] == '−' || _s[_i] == '-')) { sign = -1; _i++; }
            else if (_i < _s.Length && _s[_i] == '+') _i++;
            var ds = _i;
            while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
            if (_i == ds) { _i = save; throw CalcException.Syntax(); }
            value *= Math.Pow(10, sign * int.Parse(_s[ds.._i], CultureInfo.InvariantCulture));
        }
        return Check(value);
    }

    private double ParseName()
    {
        var start = _i;
        while (_i < _s.Length && char.IsLetter(_s[_i])) _i++;
        var name = _s[start.._i];
        // inverse functions are written sin⁻¹
        if (Peek("⁻¹") && IsFunction(name + "⁻¹")) { _i += 2; name += "⁻¹"; }

        switch (name)
        {
            case "e": return Math.E;
            case "Ans": return _ans;
            case "M": return _memory;
        }
        if (!IsFunction(name)) throw CalcException.Syntax();

        if (!Eat("(")) throw CalcException.Syntax();
        var arg = ParseSum();
        Eat(")"); // closing bracket optional at the end
        return Apply(name, arg);
    }

    private static bool IsFunction(string n) => n is
        "sin" or "cos" or "tan" or "sin⁻¹" or "cos⁻¹" or "tan⁻¹" or
        "sinh" or "cosh" or "tanh" or "sinh⁻¹" or "cosh⁻¹" or "tanh⁻¹" or
        "log" or "ln" or "Abs";

    // ---------- maths ----------

    private double ToRad(double x) => _angle switch
    {
        AngleMode.Deg => x * Math.PI / 180,
        AngleMode.Gra => x * Math.PI / 200,
        _ => x,
    };

    private double FromRad(double x) => _angle switch
    {
        AngleMode.Deg => x * 180 / Math.PI,
        AngleMode.Gra => x * 200 / Math.PI,
        _ => x,
    };

    /// <summary>Snap results like 6.1e-17 to 0 and 0.49999999999999994 to 0.5, as a real calculator's 15-digit working does.</summary>
    private static double Tidy(double v) => Math.Abs(v) < 1e-14 ? 0 : Math.Round(v, 13);

    private double Apply(string name, double x)
    {
        switch (name)
        {
            case "sin": return Tidy(Math.Sin(ToRad(x)));
            case "cos": return Tidy(Math.Cos(ToRad(x)));
            case "tan":
            {
                var c = Math.Cos(ToRad(x));
                if (Math.Abs(c) < 1e-14) throw CalcException.Math();
                return Tidy(Math.Sin(ToRad(x)) / c);
            }
            case "sin⁻¹": if (x < -1 || x > 1) throw CalcException.Math(); return Tidy(FromRad(Math.Asin(x)));
            case "cos⁻¹": if (x < -1 || x > 1) throw CalcException.Math(); return Tidy(FromRad(Math.Acos(x)));
            case "tan⁻¹": return Tidy(FromRad(Math.Atan(x)));
            case "sinh": return Check(Math.Sinh(x));
            case "cosh": return Check(Math.Cosh(x));
            case "tanh": return Check(Math.Tanh(x));
            case "sinh⁻¹": return Check(Math.Asinh(x));
            case "cosh⁻¹": if (x < 1) throw CalcException.Math(); return Check(Math.Acosh(x));
            case "tanh⁻¹": if (x <= -1 || x >= 1) throw CalcException.Math(); return Check(Math.Atanh(x));
            case "log": if (x <= 0) throw CalcException.Math(); return Tidy(Math.Log10(x));
            case "ln": if (x <= 0) throw CalcException.Math(); return Math.Log(x);
            case "Abs": return Math.Abs(x);
        }
        throw CalcException.Syntax();
    }

    private static double Sqrt(double x)
    {
        if (x < 0) throw CalcException.Math();
        return Math.Sqrt(x);
    }

    private static double Root(double x, double n)
    {
        if (n == 0) throw CalcException.Math();
        if (x < 0)
        {
            // odd integer roots of negatives are real: ∛(-8) = -2
            if (n == Math.Round(n) && Math.Abs(n) % 2 == 1) return -Math.Pow(-x, 1 / n);
            throw CalcException.Math();
        }
        var r = Math.Pow(x, 1 / n);
        var rounded = Math.Round(r);
        return Math.Abs(r - rounded) < 1e-12 && Math.Abs(Math.Pow(rounded, n) - x) < 1e-9 * Math.Max(1, Math.Abs(x)) ? rounded : r;
    }

    private static double Pow(double b, double e)
    {
        if (b == 0 && e <= 0) throw CalcException.Math();
        if (b < 0 && e != Math.Round(e)) throw CalcException.Math();
        return Check(Math.Pow(b, e));
    }

    private static double Factorial(double n)
    {
        if (n < 0 || n != Math.Round(n) || n > 69) throw CalcException.Math(); // the real thing stops at 69!
        double r = 1;
        for (var k = 2; k <= (int)n; k++) r *= k;
        return r;
    }

    private static double Permutations(double n, double r)
    {
        if (n < 0 || r < 0 || n != Math.Round(n) || r != Math.Round(r) || r > n) throw CalcException.Math();
        double p = 1;
        for (var k = 0; k < (int)r; k++) p *= n - k;
        return Check(p);
    }

    private static double Combinations(double n, double r)
    {
        if (n < 0 || r < 0 || n != Math.Round(n) || r != Math.Round(r) || r > n) throw CalcException.Math();
        r = Math.Min(r, n - r);
        double c = 1;
        for (var k = 1; k <= (int)r; k++) c = c * (n - r + k) / k;
        return Check(Math.Round(c));
    }
}
