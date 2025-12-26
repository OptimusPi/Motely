# Motely VSCode Tasks

This folder contains VSCode tasks for running Motely with split output streams (CSV data + progress monitoring).

## Available Tasks

### 1. **Motely: Run JSON Search with Split Output**
Run a search and see both stdout (CSV) and stderr (progress) in the same terminal.

**Usage:**
- Press `Ctrl+Shift+P` → "Tasks: Run Task"
- Select "Motely: Run JSON Search with Split Output"
- Enter config name (e.g., `nate`)
- Enter end batch number (e.g., `100000`)

**Output:**
- CSV data and progress comments interleaved in the terminal
- Progress lines prefixed with `#` (stderr)

---

### 2. **Motely: Run with CSV Output to File**
Redirect CSV to `results.csv` and progress to `progress.log`.

**Usage:**
- Press `Ctrl+Shift+P` → "Tasks: Run Task"
- Select "Motely: Run with CSV Output to File"
- Enter config name and end batch

**Output Files:**
- `Motely/results.csv` - Clean CSV data (stdout)
- `Motely/progress.log` - Progress updates (stderr)

---

### 3. **Motely: Watch Progress Log**
Tail the `progress.log` file in real-time (works with Task #2).

**Usage:**
- First run Task #2 to create `progress.log`
- Then run this task to monitor progress in a separate terminal

---

### 4. **Motely: Watch CSV Output**
Tail the `results.csv` file in real-time (works with Task #2).

**Usage:**
- First run Task #2 to create `results.csv`
- Then run this task to monitor CSV output in a separate terminal

---

### 5. **Motely: Run and Monitor (All Streams)** ⭐ **RECOMMENDED**
Run the search AND automatically open watchers for both streams in separate terminals.

**Usage:**
- Press `Ctrl+Shift+P` → "Tasks: Run Task"
- Select "Motely: Run and Monitor (All Streams)"
- Enter config name and end batch

**Result:**
Opens 3 terminals:
1. **Main** - Search running (saves to files)
2. **Progress** - Live progress updates (`tail -f progress.log`)
3. **CSV** - Live CSV results (`tail -f results.csv`)

---

## Keyboard Shortcuts (Optional)

Add to your `keybindings.json`:

```json
[
    {
        "key": "ctrl+shift+m",
        "command": "workbench.action.tasks.runTask",
        "args": "Motely: Run and Monitor (All Streams)"
    }
]
```

Then press `Ctrl+Shift+M` to instantly run the search with split monitoring!

---

## Output Stream Architecture

### stdout (CSV Data)
```csv
Seed,TotalScore,Paintbrush,Paintbrush,...
51A11111,2,0,0,0,1,0,1,0
41611111,0,0,0,0,0,0,0,0
```

### stderr (Progress Updates)
```
# Progress updates will appear here every 2 seconds...
# Progress: 0.15% ~00:03:42 remaining (1450 seeds/ms)
# Progress: 0.31% ~00:03:38 remaining (1465 seeds/ms)
```

### Combined (Default Terminal)
Both streams appear together - CSV lines and progress comments interleaved.

---

## Tips

1. **For long searches (hours)**: Use Task #5 to monitor both streams in real-time
2. **For quick searches (< 2 sec)**: Use Task #1 - progress won't update before completion
3. **For data analysis**: Use Task #2, then analyze `results.csv` without progress noise
4. **Progress interval**: Updates every 2 seconds (configurable in `MotelySearch.cs` line 334)

---

## Troubleshooting

**Q: Progress lines not showing?**
A: Search completes too fast (< 2 sec). Increase `--endBatch` to see progress.

**Q: Can't see the task in VSCode?**
A: Press `Ctrl+Shift+P` → "Tasks: Run Task" → Should list all Motely tasks.

**Q: Want to change config without prompts?**
A: Edit `tasks.json` and replace `${input:configName}` with hardcoded value like `"nate"`.

**Q: PowerShell errors?**
A: Tasks use `cmd.exe` to launch PowerShell. Ensure PowerShell is in your PATH.
