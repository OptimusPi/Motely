# Jupyter Notebooks for Motely/JAML Learning

## The Problem

Users on GitHub are confused about:
- How to use Motely
- How to write JAML filters
- How to use Balatro seed searching

**Solution:** Interactive Jupyter notebooks with C# code examples they can run and see results immediately.

---

## What We Need

### Option 1: .NET Interactive Notebooks (Recommended)
**Format:** `.ipynb` files with C# cells  
**Tool:** VS Code with .NET Interactive extension  
**Why:** Native C# support, can run Motely code directly

### Option 2: Polyglot Notebooks
**Format:** `.ipynb` files with multiple languages  
**Tool:** VS Code with Polyglot Notebooks extension  
**Why:** Can mix C#, Markdown, and other languages

---

## Notebook Ideas

### 1. `01-Getting-Started.ipynb`
**Purpose:** First-time user introduction

**Content:**
- What is Motely?
- What is JAML?
- Basic setup (clone repo, build)
- First simple search example
- Run code → see results

**Example Code:**
```csharp
// Load a simple JAML filter
var jaml = @"
must:
  - type: Joker
    value: Blueprint
    antes: [1]
";

// Search for seeds
var (results, searchId) = await SearchManager.Instance.StartSearchAsync(
    jaml,
    deck: "Red",
    stake: "White",
    seedCount: 1000
);

// Display results
results.Take(5).Select(r => r.Seed).ToList()
```

---

### 2. `02-JAML-Basics.ipynb`
**Purpose:** Learn JAML syntax

**Content:**
- JAML structure (must, should, mustNot)
- Type enums (Joker, Voucher, Tag, etc.)
- Ante ranges
- Editions (Foil, Holographic, etc.)
- Examples with explanations

**Example Code:**
```csharp
// Example 1: Must have Blueprint
var jaml1 = @"
must:
  - type: Joker
    value: Blueprint
    antes: [1]
";

// Example 2: Should have economy items
var jaml2 = @"
should:
  - type: Tarot
    value: Fool
    antes: [1,2,3]
  - type: StandardCard
    enhancement: Gold
    antes: [1]
";

// Run both and compare
```

---

### 3. `03-Advanced-JAML.ipynb`
**Purpose:** Complex JAML patterns

**Content:**
- Multiple conditions
- MustNot (excluding items)
- Score requirements
- Deck/stake combinations
- Real-world examples

**Example Code:**
```csharp
// Complex example: Perkeo + Observatory + no Negative tag
var complexJaml = @"
must:
  - type: Joker
    value: Perkeo
    antes: [1]
  - type: Voucher
    value: Observatory
    antes: [1]
mustNot:
  - type: Tag
    value: NegativeTag
    antes: [1]
";

// Search and analyze results
```

---

### 4. `04-Using-MCP-Server.ipynb`
**Purpose:** How to use the MCP server programmatically

**Content:**
- What is MCP?
- Calling generate_jaml_filter
- Calling search_seeds
- Checking search status
- Full workflow example

**Example Code:**
```csharp
// Use MCP server to generate JAML from natural language
var mcpServer = new McpServer(...);
var (jaml, reasoning, error) = await mcpServer.GenerateJamlOnlyAsync(
    "Find me a seed with Blueprint and Brainstorm"
);

// Then search with the generated JAML
var (results, searchId) = await SearchManager.Instance.StartSearchAsync(jaml, ...);
```

---

### 5. `05-Seed-Analysis.ipynb`
**Purpose:** Analyze found seeds

**Content:**
- Using MotelySeedAnalyzer
- Understanding analysis output
- Verifying seeds match requirements
- Ante-by-ante breakdown

**Example Code:**
```csharp
// Analyze a specific seed
var analysis = MotelySeedAnalyzer.Analyze(
    new MotelySeedAnalysisConfig("ALEEB", MotelyDeck.Red, MotelyStake.White)
);

// Display formatted analysis
Console.WriteLine(analysis.ToString());
```

---

## Implementation Steps

### Step 1: Install .NET Interactive
```bash
dotnet tool install -g Microsoft.dotnet-interactive
```

### Step 2: Install VS Code Extension
- Install "Polyglot Notebooks" extension
- Or "Jupyter" extension + .NET Interactive

### Step 3: Create Notebooks Directory
```
Motely/
  notebooks/
    01-Getting-Started.ipynb
    02-JAML-Basics.ipynb
    03-Advanced-JAML.ipynb
    04-Using-MCP-Server.ipynb
    05-Seed-Analysis.ipynb
    README.md
```

### Step 4: Add NuGet References
Each notebook needs to reference:
- `Motely` project (local)
- `DuckDB` (for SearchManager)
- Other dependencies

**In notebook:**
```csharp
#r "nuget: DuckDB, 0.9.0"
#r "../Motely/bin/Debug/net8.0/Motely.dll"
```

---

## Benefits

✅ **Interactive Learning:** Users can run code and see results  
✅ **Copy-Paste Friendly:** Easy to copy examples  
✅ **Visual:** Markdown cells for explanations  
✅ **Self-Contained:** Each notebook is a complete tutorial  
✅ **GitHub-Friendly:** .ipynb files render on GitHub  

---

## Example Notebook Structure

```markdown
# Cell 1: Markdown
## Introduction
This notebook teaches you how to use Motely to search for Balatro seeds.

# Cell 2: C#
// Setup code
using Motely;
using Motely.Filters;

# Cell 3: Markdown
## Example 1: Simple Search

# Cell 4: C#
// Example code with comments
var jaml = "...";
// Run search...

# Cell 5: Markdown
## Results
The search found X seeds matching your criteria.

# Cell 6: C#
// Display results
results.Select(r => r.Seed).ToList()
```

---

## Next Steps

1. Create `notebooks/` directory
2. Create first notebook (`01-Getting-Started.ipynb`)
3. Test in VS Code
4. Add to GitHub
5. Link from README.md

---

**This will help users learn Motely and JAML interactively!** 🎓
