# VS Code Notebook Workflow

This repo now has a real `.ipynb` workflow centered on the VS Code Jupyter stack, not the deprecated Polyglot Notebooks path.

## What to use

Use these two workflows on purpose:

1. `.ipynb` notebooks for data exploration, quick CSV inspection, plots, and write-up-heavy work.
2. `.cs` file-based apps for C# scratch work, experiments, and quick automation.

That split is the least fragile option in 2026.

## Recommended VS Code extensions

Workspace recommendations live in `.vscode/extensions.json`:

- `ms-toolsai.jupyter`
- `ms-python.python`
- `ms-toolsai.datawrangler`
- `ms-dotnettools.csdevkit`

## Notebook quick start

1. Open `notebooks/funney-seance-sixtid.ipynb`.
2. When prompted, choose a Python kernel.
3. If no Python/Jupyter kernel exists yet, create one in any Python environment:

```powershell
py -m pip install jupyter ipykernel
```

4. In the notebook, update `CSV_PATH` in Cell 2 if your CSV is not next to the notebook.
5. Run cells top to bottom.

## C# kernel options, honestly

### Option 1: Jupyter + a C# kernelspec

This is possible, but the usual route has been `.NET Interactive` / Polyglot Notebooks, and that stack is deprecated in 2026. If you already have it installed and it works, you can keep using it locally. It is not the path to bet future workflow stability on.

Use this only if all of the following are true:

- you already rely on existing C# notebooks
- you accept that future VS Code or SDK changes may break it
- you do not want to invest more migration effort right now

### Option 2: C# Dev Kit

`C# Dev Kit` is excellent for C# editing, solution navigation, tests, refactors, and debugging. It is not a notebook kernel.

### Option 3: .NET 10 file-based apps

This is the supported C# scratchpad replacement Microsoft is actively pointing people toward.

Use:

```powershell
dotnet .\scratch\csharp-scratch.cs
```

This gives you:

- one-file C# experiments
- NuGet package directives with `#:package`
- easy promotion into a real project later
- much lower risk than betting on a deprecated notebook runtime

## Recommendation for this repo

- Use Python notebooks for interactive data work.
- Use `scratch/csharp-scratch.cs` for C# experimentation.
- Keep C# notebook usage in the "legacy but still runnable if you already have it" bucket.