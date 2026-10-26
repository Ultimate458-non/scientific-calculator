using SciCalc.Core;

// A tiny test runner with no packages, so it runs anywhere `dotnet` does.
var failed = 0;
var passed = 0;

void Check(string name, bool ok, string detail = "")
{
    if (ok) passed++;
    else { failed++; Console.WriteLine($"FAIL {name} {detail}"); }
}

string Show(string expr, AngleMode a = AngleMode.Deg, double ans = 0, double mem = 0)
{
    try { return Format.Number(new Evaluator(a, ans, mem).Evaluate(expr)); }
    catch (CalcException e) { return e.Message; }
}

void Eq(string expr, string expected, AngleMode a = AngleMode.Deg, double ans = 0, double mem = 0)
{
    var got = Show(expr, a, ans, mem);
    Check(expr, got == expected, $"expected {expected} got {got}");
}

// arithmetic and precedence
Eq("1+2×3", "7");
Eq("(1+2)×3", "9");
Eq("10÷4", "2.5");
Eq("1÷3", "0.3333333333");
Eq("2÷3", "0.6666666667");
Eq("0.1+0.2", "0.3");
Eq("7−10", "−3");
Eq("−2²", "−4");
Eq("(−2)²", "4");
Eq("2^3^2", "512");
Eq("2^−2", "0.25");
Eq("5÷0", "Math ERROR");
Eq("2(3+4)", "14");
Eq("3(4)(5)", "60");
Eq("2π", "6.283185307");
Eq("(1+2", "3"); // missing close bracket is allowed at the end
Eq("1+", "Syntax ERROR");
Eq("", "Syntax ERROR");
Eq("×3", "Syntax ERROR");
Eq("1..2+3", "Syntax ERROR");

// powers, roots, reciprocals, factorial, percent
Eq("5²", "25");
Eq("3³", "27");
Eq("4⁻¹", "0.25");
Eq("0⁻¹", "Math ERROR");
Eq("√(16)", "4");
Eq("√16", "4");
Eq("√2", "1.414213562");
Eq("√(−4)", "Math ERROR");
Eq("∛(27)", "3");
Eq("∛(−8)", "−2");
Eq("3ˣ√27", "3");
Eq("4ˣ√16", "2");
Eq("2^10", "1024");
Eq("5!", "120");
Eq("0!", "1");
Eq("69!", "1.711224524E98");
Eq("70!", "Math ERROR");
Eq("(−1)!", "Math ERROR");
Eq("50%", "0.5");
Eq("200×15%", "30");
Eq("1.5E3", "1500");
Eq("2E−3", "0.002");
Eq("1E10", "1E10");
Eq("1E−10", "1E−10");
Eq("123456789012", "1.23456789E11");

// combinatorics
Eq("5P2", "20");
Eq("5C2", "10");
Eq("10C3", "120");
Eq("3P5", "Math ERROR");
Eq("2×5C2", "20");

// trig, degrees
Eq("sin(30)", "0.5");
Eq("sin(90)", "1");
Eq("sin(180)", "0");
Eq("cos(60)", "0.5");
Eq("cos(90)", "0");
Eq("tan(45)", "1");
Eq("tan(90)", "Math ERROR");
Eq("sin⁻¹(0.5)", "30");
Eq("cos⁻¹(0.5)", "60");
Eq("tan⁻¹(1)", "45");
Eq("sin⁻¹(2)", "Math ERROR");
Eq("sin(30", "0.5");
Eq("2sin(30)", "1");
// radians and gradians
Eq("sin(π÷2)", "1", AngleMode.Rad);
Eq("cos(π)", "−1", AngleMode.Rad);
Eq("sin(100)", "1", AngleMode.Gra);
Eq("tan⁻¹(1)", "0.7853981634", AngleMode.Rad);
// hyperbolic
Eq("sinh(0)", "0");
Eq("cosh(0)", "1");
Eq("cosh⁻¹(0.5)", "Math ERROR");

// logs
Eq("log(1000)", "3");
Eq("log(0)", "Math ERROR");
Eq("ln(e)", "1");
Eq("ln(−1)", "Math ERROR");
Eq("e", "2.718281828");
Eq("Abs(−7)", "7");

// Ans and memory
Eq("Ans+1", "11", AngleMode.Deg, 10);
Eq("2M", "10", AngleMode.Deg, 0, 5);
Eq("foo(1)", "Syntax ERROR");

// formatting
Check("fmt 0", Format.Number(0) == "0");
Check("fmt big", Format.Number(12345678901234) == "1.23456789E13", Format.Number(12345678901234));
Check("fmt neg", Format.Number(-0.5) == "−0.5");
Check("fmt small", Format.Number(0.000000001) == "0.000000001", Format.Number(0.000000001));
Check("fmt tiny", Format.Number(1.5e-12) == "1.5E−12", Format.Number(1.5e-12));
Check("fmt 10dp", Format.Number(100.0 / 3) == "33.33333333", Format.Number(100.0 / 3));

// the calculator state machine
Calculator K(params string[] keys)
{
    var c = new Calculator();
    foreach (var k in keys) c.Execute(k);
    return c;
}

var c1 = K("ins:1", "ins:2", "op:+", "ins:3", "eq");
Check("12+3", c1.Result == "15", c1.Result);
var c2 = K("ins:1", "ins:2", "op:+", "ins:3", "eq", "op:×", "ins:2", "eq");
Check("continue from Ans", c2.Result == "30" && c2.Expression == "Ans×2", c2.Result + " " + c2.Expression);
var c3 = K("ins:1", "op:+", "ins:1", "eq", "ins:5");
Check("digit after = starts new", c3.Expression == "5" && c3.Result == "", c3.Expression);
var c4 = K("ins:1", "ins:2", "ins:3", "del");
Check("del", c4.Expression == "12");
var c5 = K("ins:sin⁻¹(", "del");
Check("del whole function", c5.Expression == "");
var c6 = K("ins:1", "ins:2", "left", "ins:9");
Check("insert at cursor", c6.Expression == "192", c6.Expression);
var c7 = K("ins:1", "op:÷", "ins:0", "eq");
Check("error shown", c7.IsError && c7.Result == "Math ERROR");
var c8 = K("ins:1", "op:÷", "ins:0", "eq", "ins:4");
Check("error clears on typing", !c8.IsError && c8.Expression == "4");
var c9 = K("ins:1", "op:+", "ins:1", "eq", "left", "del");
Check("replay then edit", c9.Expression == "1+" , c9.Expression);
var c10 = K("ins:5", "m+", "ins:3", "m+", "mr", "op:+", "ins:0", "eq");
Check("memory add", c10.Memory == 8 && c10.Result == "8", $"{c10.Memory} {c10.Result}");
var c11 = K("ins:5", "m+", "ins:2", "m-");
Check("memory subtract", c11.Memory == 3, c11.Memory.ToString());
var c12 = K("drg");
Check("drg cycles", c12.Angle == AngleMode.Rad);
c12.Execute("drg"); c12.Execute("drg");
Check("drg wraps", c12.Angle == AngleMode.Deg);
var c13 = K("shift");
Check("shift on", c13.Shift);
c13.Execute("ins:1");
Check("shift clears after key", !c13.Shift);
var c14 = K("op:×", "ins:2", "eq");
Check("leading operator uses Ans", c14.Expression == "Ans×2" && c14.Result == "0", c14.Expression + " " + c14.Result);
var c15 = K("ins:2", "eq", "ins:3", "ac");
Check("ac clears", c15.Expression == "" && c15.Result == "");


// ---- exam mode rules ----
var t0 = new DateTimeOffset(2026, 11, 3, 8, 0, 0, TimeSpan.Zero);
ExamSession NewExam(int minutes = 0) => ExamSession.Start("Thandi", "2026001", "4821", minutes, 120, t0);

var ex = NewExam();
Check("exam starts", ex.State.Active && ex.State.Events.Count == 1);
Check("times are local", ex.State.StartedAt.Offset == TimeSpan.FromHours(2) && ex.State.StartedAt.Hour == 10);
Check("pin not stored plain", !ex.State.PinHash.Contains("4821") && ex.VerifyPin("4821") && !ex.VerifyPin("0000"));

ex.Left("switched app", t0.AddMinutes(5));
Check("first leave counted", ex.State.Violations == 1 && ex.State.Away);
ex.Left("blur", t0.AddMinutes(5)); // duplicate signal
Check("duplicate signals ignored", ex.State.Violations == 1);
var o1 = ex.Returned(t0.AddMinutes(5).AddSeconds(30));
Check("first return gives warning", o1 == ReturnOutcome.Warning && ex.State.WarningPending && !ex.State.Locked);
ex.AcknowledgeWarning();
Check("warning acknowledged", !ex.State.WarningPending);
Check("return when not away is nothing", ex.Returned(t0.AddMinutes(6)) == ReturnOutcome.None);

ex.Left("switched app", t0.AddMinutes(10));
var o2 = ex.Returned(t0.AddMinutes(10).AddSeconds(20));
Check("second return locks", o2 == ReturnOutcome.Locked && ex.State.Locked && ex.State.Violations == 2);
Check("wrong pin does not unlock", !ex.TryUnlock("1111", t0.AddMinutes(11)) && ex.State.Locked);
Check("right pin unlocks", ex.TryUnlock("4821", t0.AddMinutes(11)) && !ex.State.Locked);
Check("total away time", ex.TotalTimeAway(t0.AddMinutes(12)) == TimeSpan.FromSeconds(50), ex.TotalTimeAway(t0.AddMinutes(12)).ToString());

ex.Left("again", t0.AddMinutes(13));
Check("third leave while unlocked", ex.Returned(t0.AddMinutes(13).AddSeconds(5)) == ReturnOutcome.Locked && ex.State.Violations == 3);
Check("cannot end with wrong pin", !ex.TryEnd("nope", t0.AddMinutes(14)) && ex.State.Active);
Check("end with right pin", ex.TryEnd("4821", t0.AddMinutes(15)) && !ex.State.Active);
var rep = ex.Report(t0.AddMinutes(16));
Check("report lists events", rep.Contains("Thandi") && rep.Contains("2026-11-03 10:05:00") && rep.Contains("Left: switched app (time 1)") && rep.Contains("Times left the calculator: 3"), rep);

var ex2 = NewExam(60);
Check("time left", ex2.TimeLeft(t0.AddMinutes(10)) == TimeSpan.FromMinutes(50));
Check("not up yet", !ex2.CheckTime(t0.AddMinutes(59)));
Check("time up fires once", ex2.CheckTime(t0.AddMinutes(61)) && !ex2.CheckTime(t0.AddMinutes(62)) && ex2.State.Locked && ex2.State.TimeUp);
Check("time up cannot be unlocked", !ex2.TryUnlock("4821", t0.AddMinutes(63)));
Check("time up can be ended", ex2.TryEnd("4821", t0.AddMinutes(64)));

var ex3 = NewExam();
Check("reload counts as leaving", ex3.Resumed(t0.AddMinutes(1)) == ReturnOutcome.Warning && ex3.State.Violations == 1);
Check("second reload locks", ex3.Resumed(t0.AddMinutes(2)) == ReturnOutcome.Locked);

var json = System.Text.Json.JsonSerializer.Serialize(ex3.State, ExamJsonContext.Default.ExamState);
var back = System.Text.Json.JsonSerializer.Deserialize(json, ExamJsonContext.Default.ExamState)!;
Check("state survives save and load", back.Violations == 2 && back.Locked && back.Events.Count == ex3.State.Events.Count && back.StartedAt == ex3.State.StartedAt && new ExamSession(back).VerifyPin("4821"));

bool Throws(Action a) { try { a(); return false; } catch (ArgumentException) { return true; } }
Check("needs a name", Throws(() => ExamSession.Start("", "", "4821", 0, 0, t0)));
Check("needs a pin", Throws(() => ExamSession.Start("A", "", "12", 0, 0, t0)));

Console.WriteLine($"{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
