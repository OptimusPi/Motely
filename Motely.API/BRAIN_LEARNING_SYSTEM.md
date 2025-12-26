# Brain Learning System - Making JamlGenie Smarter Over Time

## Current Workflow (Manual)

**Current Process:**
1. User reports issue or you notice pattern
2. Manually edit `JAML_GENIE_BRAIN.md`
3. Rebuild and redeploy API
4. Brain document reloads on next request

**Limitations:**
- Requires manual intervention
- No automatic learning from failures
- Feedback data exists but isn't used effectively

---

## Proposed Auto-Learning System

### Phase 1: Real-Time Learning (Immediate)

**Use Existing Feedback in System Prompt:**
- Inject recent failures into AI context (already have `GetFailureContextForPrompt()`)
- Learn from mistakes in real-time without redeploying
- No code changes needed - just use existing feedback service

**Implementation:**
```csharp
// In GetSystemPrompt(), add:
var failureContext = _feedbackService?.GetFailureContextForPrompt(5) ?? "";
// Append to system prompt
```

**Benefits:**
- Immediate learning from failures
- No redeployment needed
- Works with existing infrastructure

---

### Phase 2: Pattern Analysis (Short-term)

**Analyze Failure Patterns:**
- Create admin endpoint to analyze `failures.jsonl`
- Identify common failure patterns:
  - Invalid item names
  - Impossible configs
  - Missing edge cases
  - Common typos/slang not handled

**Implementation:**
- Add `/admin/analyze-failures` endpoint
- Generate insights report
- Suggest brain document updates

**Benefits:**
- Data-driven improvements
- Identify knowledge gaps
- Prioritize updates

---

### Phase 3: Auto-Update Brain Document (Long-term)

**Automated Brain Updates:**
- Periodically analyze failures and successes
- Generate brain document updates automatically
- Create pull request or update file directly

**Components:**

1. **Failure Analyzer Service**
   - Reads `failures.jsonl` and `feedback.jsonl`
   - Identifies patterns:
     - Common invalid item names → Add to slang translations
     - Impossible configs → Add to impossible configs section
     - Missing synergies → Add to synergies section
     - Edge cases → Add to edge cases section

2. **Success Analyzer Service**
   - Reads successful searches
   - Identifies patterns:
     - Common successful patterns → Add to examples
     - Effective strategies → Add to strategy section
     - Good synergies → Add to synergies section

3. **Brain Document Generator**
   - Takes analyzed patterns
   - Generates markdown updates
   - Merges with existing brain document
   - Validates markdown syntax

4. **Update Scheduler**
   - Runs daily/weekly
   - Analyzes recent failures (last 7 days)
   - Generates updates
   - Optionally auto-commits or creates PR

**Implementation Options:**

**Option A: Manual Review (Safer)**
- Generate update suggestions
- Create markdown diff
- Admin reviews and approves
- Auto-applies approved updates

**Option B: Auto-Update (Faster)**
- Auto-generate and apply updates
- Log all changes
- Rollback capability
- Confidence threshold (only high-confidence updates)

**Option C: Hybrid**
- Auto-update low-risk sections (examples, patterns)
- Manual review for high-risk sections (impossible configs, mechanics)

---

## Recommended Implementation Plan

### Step 1: Use Existing Feedback (No Code Changes)
Add failure context to system prompt:
```csharp
private string GetSystemPrompt()
{
    var brainDoc = LoadBrainDocument();
    var failureContext = _feedbackService?.GetFailureContextForPrompt(5) ?? "";
    
    return $@"...{brainDoc}...
    
{failureContext}
    
...rest of prompt...";
}
```

### Step 2: Add Analysis Endpoint
Create `/admin/analyze-failures` endpoint:
- Analyzes recent failures
- Generates insights report
- Suggests brain updates
- Shows success/failure rates

### Step 3: Add Auto-Learning Service
Create `BrainLearningService`:
- Analyzes failures/successes
- Generates brain document updates
- Can be triggered manually or scheduled

### Step 4: Add Update Mechanism
- Option A: Manual review workflow
- Option B: Auto-update with confidence threshold
- Option C: Hybrid approach

---

## Example: Auto-Learning in Action

**Scenario:** Multiple failures with "blurry face" not being recognized

**Failure Pattern Detected:**
```
"blurry face" → Generated invalid JAML
Error: Unknown joker name
Count: 15 failures in last 7 days
```

**Auto-Generated Update:**
```markdown
### Slang Translations (Auto-added)
- "blurry face joker" → SmearedJoker
- "blurry face" → SmearedJoker
```

**Result:**
- Brain document automatically updated
- Future requests handle "blurry face" correctly
- No manual intervention needed

---

## Monitoring & Metrics

**Track Learning Effectiveness:**
- Failure rate over time (should decrease)
- Success rate over time (should increase)
- Common failure patterns (should diversify as fixes applied)
- Brain document size (should grow with knowledge)

**Metrics Dashboard:**
- Total failures logged
- Recent failure patterns
- Auto-updates applied
- Success rate improvement

---

## Best Practices

1. **Start Conservative**
   - Begin with manual review
   - Build confidence in system
   - Gradually increase automation

2. **Validate Updates**
   - Test brain updates before applying
   - Validate markdown syntax
   - Check for conflicts with existing knowledge

3. **Monitor Quality**
   - Track if auto-updates improve success rate
   - Rollback if quality degrades
   - Human review for high-impact changes

4. **Incremental Learning**
   - Small, frequent updates better than large batches
   - Learn from recent failures (last 7-30 days)
   - Don't overfit to old patterns

5. **Feedback Loop**
   - Users can report issues
   - System learns from reports
   - Continuous improvement cycle

---

## Future Enhancements

1. **A/B Testing**
   - Test different brain document versions
   - Compare success rates
   - Adopt best-performing version

2. **User Feedback Integration**
   - Allow users to rate results
   - Learn from positive/negative feedback
   - Improve based on user satisfaction

3. **Community Contributions**
   - Allow community to suggest brain updates
   - Review and merge community knowledge
   - Crowdsource improvements

4. **Multi-Version Brain**
   - Maintain multiple brain versions
   - Route requests to different versions
   - Compare performance
   - Gradually migrate to best version

---

## Quick Start: Use Existing Feedback Now

**Immediate improvement (5 minutes):**
1. Update `McpServer.cs` to inject failure context
2. No redeployment needed for feedback collection
3. System learns from failures in real-time

**Code change:**
```csharp
private string GetSystemPrompt()
{
    var jokerMapping = GetJokerNameMapping();
    var brainDoc = LoadBrainDocument();
    var failureContext = _feedbackService?.GetFailureContextForPrompt(5) ?? "";
    
    return $@"...existing prompt...
    
COMPREHENSIVE BALATRO KNOWLEDGE BASE:
{brainDoc}

{failureContext}

...rest of prompt...";
}
```

This immediately makes the AI learn from past failures without any infrastructure changes!

