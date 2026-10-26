using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace SciCalc.Core;

public enum ExamEventKind
{
    Started, Left, Returned, WarningShown, Locked, Unlocked, FullscreenExited, Reloaded, TimeUp, Ended,
}

public sealed class ExamEvent
{
    public DateTimeOffset At { get; set; }
    public ExamEventKind Kind { get; set; }
    public string Detail { get; set; } = "";
}

/// <summary>Everything about one exam sitting. Saved to the phone so reloading the page cannot be used to escape.</summary>
public sealed class ExamState
{
    public string StudentName { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string PinSalt { get; set; } = "";
    public string PinHash { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public int DurationMinutes { get; set; }
    public int UtcOffsetMinutes { get; set; }
    public bool Active { get; set; }
    public bool Away { get; set; }
    public DateTimeOffset? AwaySince { get; set; }
    public int Violations { get; set; }
    public bool Locked { get; set; }
    public bool WarningPending { get; set; }
    public bool TimeUp { get; set; }
    public List<ExamEvent> Events { get; set; } = new();
}

[JsonSerializable(typeof(ExamState))]
[JsonSourceGenerationOptions(WriteIndented = false)]
public partial class ExamJsonContext : JsonSerializerContext { }

public enum ReturnOutcome { None, Warning, Locked }

/// <summary>
/// The exam rules, kept apart from the screen so they can be tested.
/// First time the student leaves the calculator: a warning. Second time and after: the calculator locks and sounds an alarm until the invigilator's PIN is entered.
/// Every move is recorded with a time stamp.
/// </summary>
public sealed class ExamSession
{
    public const int WarningsBeforeLock = 1;

    public ExamState State { get; }

    public ExamSession(ExamState state) { State = state; }

    public static ExamSession Start(string name, string studentId, string pin, int durationMinutes, int utcOffsetMinutes, DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter the student's name.");
        if (pin is null || pin.Length < 4) throw new ArgumentException("The invigilator PIN needs at least 4 characters.");
        if (durationMinutes < 0 || durationMinutes > 600) throw new ArgumentException("Duration must be 0 to 600 minutes.");

        var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var now = utcNow.ToOffset(TimeSpan.FromMinutes(utcOffsetMinutes));
        var s = new ExamState
        {
            StudentName = name.Trim(),
            StudentId = (studentId ?? "").Trim(),
            PinSalt = salt,
            PinHash = Hash(salt, pin),
            StartedAt = now,
            DurationMinutes = durationMinutes,
            UtcOffsetMinutes = utcOffsetMinutes,
            Active = true,
        };
        var session = new ExamSession(s);
        session.Log(now, ExamEventKind.Started, durationMinutes > 0 ? $"{durationMinutes} minute exam" : "no time limit");
        return session;
    }

    private static string Hash(string salt, string pin) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(salt + "|" + pin)));

    public bool VerifyPin(string pin) =>
        pin is not null && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(State.PinSalt, pin)), Encoding.UTF8.GetBytes(State.PinHash));

    private DateTimeOffset Local(DateTimeOffset utcNow) => utcNow.ToOffset(TimeSpan.FromMinutes(State.UtcOffsetMinutes));

    private void Log(DateTimeOffset at, ExamEventKind kind, string detail = "") =>
        State.Events.Add(new ExamEvent { At = at, Kind = kind, Detail = detail });

    /// <summary>The student left the calculator (switched app, locked screen, pulled down the notification bar, etc.).</summary>
    public void Left(string reason, DateTimeOffset utcNow)
    {
        if (!State.Active || State.Away) return;
        var now = Local(utcNow);
        State.Away = true;
        State.AwaySince = now;
        State.Violations++;
        Log(now, ExamEventKind.Left, $"{reason} (time {State.Violations})");
    }

    public ReturnOutcome Returned(DateTimeOffset utcNow)
    {
        if (!State.Active || !State.Away) return ReturnOutcome.None;
        var now = Local(utcNow);
        var away = State.AwaySince is { } since ? now - since : TimeSpan.Zero;
        State.Away = false;
        State.AwaySince = null;
        Log(now, ExamEventKind.Returned, $"away for {Duration(away)}");

        if (State.Violations > WarningsBeforeLock)
        {
            State.Locked = true;
            Log(now, ExamEventKind.Locked, "calculator locked, invigilator PIN needed");
            return ReturnOutcome.Locked;
        }
        State.WarningPending = true;
        Log(now, ExamEventKind.WarningShown, "warning shown to student");
        return ReturnOutcome.Warning;
    }

    /// <summary>The page was reloaded or the browser was closed and reopened during the exam.</summary>
    public ReturnOutcome Resumed(DateTimeOffset utcNow)
    {
        if (!State.Active) return ReturnOutcome.None;
        var now = Local(utcNow);
        Log(now, ExamEventKind.Reloaded, "page reloaded or app reopened");
        State.Away = false;
        State.AwaySince = null;
        State.Violations++;
        if (State.Violations > WarningsBeforeLock)
        {
            State.Locked = true;
            Log(now, ExamEventKind.Locked, "calculator locked, invigilator PIN needed");
            return ReturnOutcome.Locked;
        }
        State.WarningPending = true;
        Log(now, ExamEventKind.WarningShown, "warning shown to student");
        return ReturnOutcome.Warning;
    }

    public void FullscreenExited(DateTimeOffset utcNow)
    {
        if (State.Active) Log(Local(utcNow), ExamEventKind.FullscreenExited, "left full screen");
    }

    public void AcknowledgeWarning() => State.WarningPending = false;

    public bool TryUnlock(string pin, DateTimeOffset utcNow)
    {
        if (!State.Active || !State.Locked || State.TimeUp || !VerifyPin(pin)) return false;
        State.Locked = false;
        Log(Local(utcNow), ExamEventKind.Unlocked, "unlocked by invigilator");
        return true;
    }

    public bool TryEnd(string pin, DateTimeOffset utcNow)
    {
        if (!State.Active || !VerifyPin(pin)) return false;
        var now = Local(utcNow);
        State.Active = false;
        State.Locked = false;
        State.WarningPending = false;
        State.EndedAt = now;
        Log(now, ExamEventKind.Ended, "ended by invigilator");
        return true;
    }

    public TimeSpan? TimeLeft(DateTimeOffset utcNow)
    {
        if (State.DurationMinutes <= 0) return null;
        var left = State.StartedAt.AddMinutes(State.DurationMinutes) - Local(utcNow);
        return left < TimeSpan.Zero ? TimeSpan.Zero : left;
    }

    /// <summary>Marks time up once the clock runs out. Returns true the first time it happens.</summary>
    public bool CheckTime(DateTimeOffset utcNow)
    {
        if (!State.Active || State.TimeUp || TimeLeft(utcNow) != TimeSpan.Zero) return false;
        State.TimeUp = true;
        State.Locked = true;
        Log(Local(utcNow), ExamEventKind.TimeUp, "time is up");
        return true;
    }

    public TimeSpan TotalTimeAway(DateTimeOffset utcNow)
    {
        var total = TimeSpan.Zero;
        DateTimeOffset? leftAt = null;
        foreach (var e in State.Events)
        {
            if (e.Kind == ExamEventKind.Left) leftAt = e.At;
            else if (e.Kind == ExamEventKind.Returned && leftAt is { } l) { total += e.At - l; leftAt = null; }
        }
        if (leftAt is { } open) total += Local(utcNow) - open;
        return total;
    }

    public static string Duration(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes}m {t.Seconds}s"
        : t.TotalMinutes >= 1 ? $"{t.Minutes}m {t.Seconds}s"
        : $"{Math.Max(0, (int)t.TotalSeconds)}s";

    public static string Stamp(DateTimeOffset t) => t.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>A plain-text record the invigilator can copy, save or send.</summary>
    public string Report(DateTimeOffset utcNow)
    {
        var s = State;
        var sb = new StringBuilder();
        sb.AppendLine("CALCULATOR EXAM RECORD");
        sb.AppendLine($"Student: {s.StudentName}{(s.StudentId == "" ? "" : " (" + s.StudentId + ")")}");
        sb.AppendLine($"Started: {Stamp(s.StartedAt)}");
        sb.AppendLine($"Ended:   {(s.EndedAt is { } e ? Stamp(e) : "not ended")}");
        sb.AppendLine($"Times left the calculator: {s.Violations}");
        sb.AppendLine($"Total time away: {Duration(TotalTimeAway(utcNow))}");
        sb.AppendLine();
        sb.AppendLine("Time stamp           | Event");
        foreach (var ev in s.Events)
            sb.AppendLine($"{Stamp(ev.At)} | {ev.Kind}{(ev.Detail == "" ? "" : ": " + ev.Detail)}");
        return sb.ToString();
    }
}
