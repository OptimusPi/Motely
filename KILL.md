# Kill stuck stuff (keyboard only)

## Task Manager

- **Ctrl+Shift+Esc**
- Or **Win**, type `taskmgr`, **Enter**
- Or **Ctrl+Alt+Del** → Task Manager (arrow keys)

Navigate with **Tab** / arrows; **Delete** or **Shift+F10** → end task.

Common locks on Motely builds: **testhost**, **dotnet**, **MotelyCLI**.

## PowerShell

```powershell
taskkill /F /IM testhost.exe 2>$null
taskkill /F /IM MotelyCLI.exe 2>$null
```

All dotnet (heavy-handed):

```powershell
Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force
```
