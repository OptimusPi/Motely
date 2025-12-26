# Motely Interactive Notebooks

Interactive .NET notebooks for learning Motely, JAML, and JamlGenie.

## What Are These?

These are **Polyglot Notebooks** (`.ipynb` files) that you can open in VS Code to:
- ✅ Run C# code interactively
- ✅ See step-by-step how things work
- ✅ Experiment with examples
- ✅ Learn the codebase

## Prerequisites

### 1. Install .NET Interactive

```bash
dotnet tool install -g Microsoft.dotnet-interactive
```

### 2. Install VS Code Extension

Install the **"Polyglot Notebooks"** extension in VS Code:
- Open VS Code
- Go to Extensions (Ctrl+Shift+X)
- Search for "Polyglot Notebooks"
- Install by Microsoft

### 3. Build the Project

```bash
cd Motely.API
dotnet build
```

## Notebooks

### 01-JamlGenie-Workflow.ipynb

**Purpose:** Understand how JamlGenie processes a wish and finds seeds

**Shows:**
- Step-by-step refinement pipeline (Steps 1-4)
- AI generation process
- JAML validation
- Seed search flow
- MCP protocol flow

**Perfect for:** Developers who want to understand the internals

## Opening a Notebook

1. Open VS Code
2. File → Open File
3. Select `notebooks/01-JamlGenie-Workflow.ipynb`
4. Click "Run All" or run cells individually

## Running Code

Each notebook cell can be:
- **Markdown** - Documentation and explanations
- **C#** - Executable code

Click the "▶ Run" button on each cell, or press `Shift+Enter`.

## Adding Project References

To use actual Motely code in notebooks, add this to a C# cell:

```csharp
#r "../Motely.API/bin/Debug/net10.0/Motely.API.dll"
#r "../Motely/bin/Debug/net10.0/Motely.dll"

using Motely.API;
using Motely.Filters;
using Motely;
```

## Troubleshooting

### "Cannot find type or namespace"

- Make sure you've built the project first
- Check the path in `#r` directives
- Try using absolute paths

### "Extension not found"

- Make sure "Polyglot Notebooks" extension is installed
- Restart VS Code
- Check that .NET Interactive is installed globally

### "Cannot execute code"

- Make sure you're in a C# cell (not Markdown)
- Check that the kernel is set to ".NET (C#)"

## Future Notebooks

Planned notebooks:
- `02-JAML-Basics.ipynb` - Learn JAML syntax
- `03-Advanced-JAML.ipynb` - Complex JAML patterns
- `04-Using-MCP-Server.ipynb` - MCP server usage
- `05-Seed-Analysis.ipynb` - Analyzing seeds

## Contributing

Want to add a notebook? Great! Just:
1. Create a new `.ipynb` file
2. Follow the existing structure
3. Add it to this README
4. Submit a PR!

---

**Happy notebooking!** 📓

