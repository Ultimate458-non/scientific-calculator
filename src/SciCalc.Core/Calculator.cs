using System.Globalization;

namespace SciCalc.Core;

/// <summary>
/// The calculator's brain: the expression being typed, the cursor, the answer, memory and modes.
/// The user interface only sends it commands (see <see cref="Execute"/>) and draws its state.
/// </summary>
public sealed class Calculator
{
    private string _expr = "";
    private bool _fresh; // a result is showing; the next key decides whether to continue from it or start over

    public string Expression => _expr;
    public int Cursor { get; private set; }
    public string Result { get; private set; } = "";
    public bool IsError { get; private set; }
    public bool Shift { get; private set; }
    public AngleMode Angle { get; private set; } = AngleMode.Deg;
    public double Answer { get; private set; }
    public double Memory { get; private set; }
    public bool HasMemory => Memory != 0;

    private void Set(string expr, int cursor)
    {
        _expr = expr;
        Cursor = Math.Clamp(cursor, 0, expr.Length);
    }

    /// <summary>Runs one key command. Commands: digit/insert text as "ins:…", operators as "op:…", plus the named ones below.</summary>
    public void Execute(string command)
    {
        if (command == "shift") { Shift = !Shift; return; }
        Shift = false;

        if (command.StartsWith("ins:", StringComparison.Ordinal)) { Insert(command[4..]); return; }
        if (command.StartsWith("op:", StringComparison.Ordinal)) { Operator(command[3..]); return; }

        switch (command)
        {
            case "ac": Clear(); break;
            case "del": Delete(); break;
            case "eq": Compute(); break;
            case "left": MoveLeft(); break;
            case "right": MoveRight(); break;
            case "drg": Angle = Angle switch { AngleMode.Deg => AngleMode.Rad, AngleMode.Rad => AngleMode.Gra, _ => AngleMode.Deg }; break;
            case "ans": Insert("Ans"); break;
            case "m+": Compute(); if (!IsError) Memory += Answer; break;
            case "m-": Compute(); if (!IsError) Memory -= Answer; break;
            case "mr": Insert("M"); break;
            case "mc": Memory = 0; break;
        }
    }

    private void Insert(string text)
    {
        if (_fresh)
        {
            // Typing a number/function after a result starts a fresh calculation.
            _fresh = false;
            Result = "";
            IsError = false;
            Set("", 0);
        }
        else if (IsError)
        {
            IsError = false;
            Result = "";
        }
        Set(_expr.Insert(Cursor, text), Cursor + text.Length);
    }

    private void Operator(string symbol)
    {
        // After a result, an operator carries on from it: "Ans+".
        if (_fresh && !IsError)
        {
            _fresh = false;
            Result = "";
            Set("Ans", 3);
        }
        else if (_expr.Length == 0 && symbol is "×" or "÷" or "+" or "^" or "ˣ√")
        {
            Set("Ans", 3);
        }
        Insert(symbol);
    }

    private void Clear()
    {
        Set("", 0);
        Result = "";
        IsError = false;
        _fresh = false;
    }

    private void Delete()
    {
        if (_fresh) { MoveLeft(); return; }
        if (IsError) { IsError = false; Result = ""; return; }
        if (Cursor == 0) return;
        // Remove a whole function name like "sin⁻¹(" in one press.
        var n = TokenLengthBefore(Cursor);
        Set(_expr.Remove(Cursor - n, n), Cursor - n);
    }

    private int TokenLengthBefore(int pos)
    {
        foreach (var t in Tokens)
            if (pos >= t.Length && string.CompareOrdinal(_expr, pos - t.Length, t, 0, t.Length) == 0) return t.Length;
        return 1;
    }

    private static readonly string[] Tokens =
    {
        "sinh⁻¹(", "cosh⁻¹(", "tanh⁻¹(", "sin⁻¹(", "cos⁻¹(", "tan⁻¹(", "sinh(", "cosh(", "tanh(",
        "sin(", "cos(", "tan(", "log(", "ln(", "Abs(", "Ans", "ˣ√", "⁻¹",
    };

    private void MoveLeft()
    {
        if (_fresh) { _fresh = false; IsError = false; Cursor = _expr.Length; return; }
        if (IsError) { IsError = false; Result = ""; Cursor = _expr.Length; return; }
        if (Cursor > 0) Cursor -= TokenLengthBefore(Cursor);
    }

    private void MoveRight()
    {
        if (_fresh) { _fresh = false; Cursor = 0; return; }
        if (Cursor >= _expr.Length) return;
        foreach (var t in Tokens)
            if (string.CompareOrdinal(_expr, Cursor, t, 0, t.Length) == 0) { Cursor += t.Length; return; }
        Cursor++;
    }

    private void Compute()
    {
        if (_expr.Length == 0) return;
        try
        {
            var value = new Evaluator(Angle, Answer, Memory).Evaluate(_expr);
            Answer = value;
            Result = Format.Number(value);
            IsError = false;
        }
        catch (CalcException e)
        {
            Result = e.Message;
            IsError = true;
        }
        _fresh = true;
        Cursor = _expr.Length;
    }

    /// <summary>True when the display is showing an answer to the expression above it.</summary>
    public bool ShowingResult => _fresh;
}
