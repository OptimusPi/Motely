# TUI crash workaround (Terminal.Gui v2)

## Upstream issue

**GitHub:** [gui-cs/Terminal.Gui#3989 – InvalidOperationException when debugging from JetBrains Rider](https://github.com/gui-cs/Terminal.Gui/issues/3989)

That issue describes two failures:

1. **InvalidOperationException** when the console is not a real TTY (e.g. debugging from Rider):  
   `"Unable to initialize the console"` / `"V2 - This should never happen"` at `CursesDriver.Init()`.

2. **NullReferenceException** during the first draw (replicable outside Rider):  
   `View.DoDrawBorderAndPadding(Region originalClip)` — crash in the draw pipeline during `Application.LayoutAndDrawImpl` / `RunIteration`.

The second one can cause the TUI to “flash and exit” as soon as the first frame is drawn.

## What we did

- **Transparent viewport workaround:** In `MainMenuWindow`, `ViewportSettingsFlags.Transparent` is gated behind `UseTransparentViewport = false`. With it off, the first-draw path may avoid the code that triggers the NullRef. When Terminal.Gui fixes the bug, set `UseTransparentViewport = true` in `MainMenuWindow.cs` to restore the shader-show-through look.
- **Crash logging:** Unhandled exceptions and phase-specific failures are written to `motely-tui-crash.txt` next to the executable (see `Program.CrashLogPath`).
- **Defensive startup:** Shader and main menu creation are in try/catch; on failure we continue without shader or show an error fallback view.

## Version

We use **Terminal.Gui 2.0.0-develop.4971** (see `Directory.Packages.props`). Newer develop builds may already fix the draw crash; if the TUI runs reliably after an upgrade, re-enable `UseTransparentViewport`.
