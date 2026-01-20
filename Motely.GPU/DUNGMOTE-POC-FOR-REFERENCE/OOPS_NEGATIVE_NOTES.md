## What’s “known-good” and how to sanity-check fast

### The working searcher
- **Binary**: `negative_tag_skipper.exe`
- **Build**: `.\build.ps1 negative_tag_skipper`
- **Filter**: **Ante N small-blind tag = Negative Tag** + **best-group of joker(s)** using sudden-death forgiveness (1 offender allowed).

### Minimal “OopsAll6s + Negative Tag (Ante 6)” command

```powershell
cd D:\dungmot
.\build.ps1 negative_tag_skipper
.\negative_tag_skipper.exe --start-batch 0 --end-batch 200000 --batch-chars 4 --joker OopsAll6s --min-hits 4 --ante 6 --joker-rolls 100
```

### Verify any printed seed with Motely

```powershell
cd "X:\BalatroSeedOracle\external\Motely"
dotnet run -c Release --project Motely.TUI -- --analyze <SEED> --deck Red --stake White | Select-String "==ANTE 6==" -Context 0,6
```

### Common gotcha
- **`O` vs `0`**: Balatro seeds allow **letter `O`** but not digit `0`. Typos here make it look “wrong” instantly.


