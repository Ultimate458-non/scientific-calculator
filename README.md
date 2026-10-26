# Scientific Calculator with Exam Mode (C#)

**A free scientific calculator that runs on any phone, with an exam mode that warns, alarms and records when a student leaves it.**

Many high school students sit maths exams with a phone but no scientific calculator. This project puts one on the phone they already have: open a web page, nothing to install, nothing to lose. Built in C# with Blazor WebAssembly, it looks and works like a handheld scientific calculator.

> Not affiliated with or endorsed by any calculator maker. The design is original.

## Features
- Natural expression entry with a cursor: move with ◀ ▶, insert in the middle, DEL, AC
- `+ − × ÷`, brackets, implicit multiplication (`2π`, `3(4+1)`), correct precedence
- `x²`, `x³`, `xʸ`, `ˣ√`, `√`, `∛`, `x⁻¹`, `x!`, `%`, `Abs`
- `sin cos tan` and their inverses (`SHIFT`), in **DEG / RAD / GRA** (tap `DRG`)
- `log`, `ln`, `10ˣ`, `eˣ`, `π`, `e`, `EXP` (× 10 to a power)
- `nPr`, `nCr`
- `Ans` and memory (`M+`, `M−`, `MR`, `MC`)
- Casio-style results: 10 significant digits, scientific notation for very large or small numbers, `Math ERROR` / `Syntax ERROR`
- Continue from the last answer: press `×` after `=` and it starts with `Ans`
- Keyboard support on a computer: digits, `+ - * /`, `^ ( ) ! %`, Enter, Backspace, Esc, arrows

## Exam mode (for schools)
Tap **Exam mode** (top right), then the invigilator enters the student's name, a PIN and the exam length.

- Leaving the calculator (another app, home button, locking the screen, pulling down the notification bar, reloading the page) is recorded with a time stamp.
- **1st time:** a warning on screen and a short vibration.
- **2nd time onwards:** the calculator locks, a loud alarm sounds and the phone vibrates until the invigilator enters the PIN.
- The invigilator can end the exam with the PIN and copy or save a time-stamped record.
- The record is kept on the phone, so reloading the page does not clear it.

**Limits, please read.** A web page cannot stop a phone from switching apps and cannot see which app was opened. It can only notice, make noise and keep a record. Vibration does not work on iPhones. For a real lock, also use the phone's own feature: Android **Screen pinning**, or iPhone **Guided Access**. Alerts are on the student's own phone: the invigilator hears or sees them in the room, they are not sent over the internet. Tell students before the exam that this is recorded.

## Project layout
| Folder | What it is |
| --- | --- |
| `src/SciCalc.Core` | The maths: parser/evaluator, number formatting, calculator state. No UI. |
| `src/SciCalc` | The Blazor WebAssembly app: keypad, display and styling. |
| `tests/SciCalc.Tests` | 128 checks of the maths and key handling. Plain console app, no packages needed. |

## Run it
Needs the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```
dotnet run --project tests/SciCalc.Tests     # run the tests
dotnet run --project src/SciCalc             # start the calculator, then open the address it prints
```

## Publish on GitHub Pages
1. Push this repo to GitHub.
2. In the repo go to **Settings → Pages** and set **Source** to **GitHub Actions**.
3. Push to `main`. The workflow in `.github/workflows/deploy.yml` runs the tests, publishes the app and deploys it to `https://<your-username>.github.io/<repo-name>/`.

## Notes
- Uses `double` (about 15 digits) and shows 10, like a handheld calculator. Trig results such as `sin(180)` are tidied to `0` instead of `1.2E-16`.
- `n!` works up to 69, as on a handheld.
