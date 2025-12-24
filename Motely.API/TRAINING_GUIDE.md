# Training JamlGenie to Work Better

## Current Setup

**Seed Count:** JamlGenie searches **1 million random seeds** by default (`random:1000000`). This is hardcoded for security reasons (prevents abuse like "search 2 trillion seeds").

## How to Train It Better

### 1. **Automatic Learning from Failures** (Now Enabled!)

The system now automatically learns from past failures. Every time JamlGenie generates invalid JAML, it:
- Logs the failure to `GenieFeedback/failures.jsonl`
- Injects the last 5 failures into the AI's context
- The AI learns from these mistakes in real-time

**No action needed** - this is now enabled automatically!

### 2. **Manual Brain Document Updates**

For deeper knowledge improvements, edit `JAML_GENIE_BRAIN.md`:

```bash
# Add new synergies, mechanics, edge cases, etc.
# Then redeploy the Cloudflare Worker to update the system prompt
cd Motely.API/cloudflare-worker-jamlgenie
npx wrangler deploy
```

**What to add:**
- New synergies (e.g., "DNA + Baron combo")
- Edge cases (e.g., "Erratic deck special mechanics")
- Common mistakes (e.g., "Don't put vouchers in Ante 1 slot 0")
- Slang translations (e.g., "blurry face" → SmearedJoker)

### 3. **Review Failure Logs**

Check what's failing:

```bash
# View recent failures
cat GenieFeedback/failures.jsonl | tail -20

# Or use jq for formatted output
cat GenieFeedback/failures.jsonl | jq -r '.prompt, .error' | head -40
```

**Common failure patterns:**
- Invalid item names → Add to slang translations
- Impossible configs → Add to impossible configs section
- Missing edge cases → Add to edge cases section

### 4. **Adjust Seed Count** (Advanced)

If you want to search more seeds (for testing), modify `McpServer.cs`:

```csharp
// Line ~147
var seedSource = "random:10000000"; // 10 million instead of 1 million
```

**Warning:** More seeds = slower searches. 1 million is usually enough for most filters.

## How It Works

1. **User makes a wish** → JamlGenie generates JAML
2. **If JAML is invalid** → Logged to `failures.jsonl`
3. **Next request** → Last 5 failures are injected into AI context
4. **AI learns** → Avoids repeating the same mistakes

## Monitoring

Check if learning is working:

```bash
# Count failures
wc -l GenieFeedback/failures.jsonl

# See if same errors are repeating
cat GenieFeedback/failures.jsonl | jq -r '.error' | sort | uniq -c | sort -rn
```

If the same errors keep appearing, add them to `JAML_GENIE_BRAIN.md` manually.

## Future Improvements

- **Pattern Analysis:** Automatically identify common failure patterns
- **Success Learning:** Learn from successful searches too
- **Community Feedback:** Allow users to rate results
- **A/B Testing:** Test different brain document versions

See `BRAIN_LEARNING_SYSTEM.md` for more details.


