# Motely Filter Implementation Bugs

## 🐛 BUG #1: Joker Filter - Limited Shop Slot Range

**File:** `Motely/filters/MotelyJson/MotelyJsonJokerFilterDesc.cs`

**Problem:** Default shop slots for Ante 2+ don't include slots 7+. Blueprint at slot 7 in ALEEB is not found unless extended slots are specified.

**Test Case:** 
```json
{
  "type": "Joker",
  "value": "Blueprint",
  "antes": [2],
  "sources": {
    "shopSlots": [7]  // FAILS - slot 7 not checked by default
  }
}
```

**Fix Required:** Extend default shop slot range or respect the specified slots in the JSON config.

---

## 🐛 BUG #2: Boss Filter - No State Tracking Across Antes

**File:** `Motely/filters/MotelyJson/MotelyJsonBossFilterDesc.cs`

**Problem:** Creates a new boss stream for each ante (line 62: `var bossStream = ctx.CreateBossStream(ante);`) instead of maintaining state across antes like the Analyzer does.

**Code Issue (line 60-66):**
```csharp
foreach (var ante in clause.EffectiveAntes)
{
    var bossStream = ctx.CreateBossStream(ante);  // ❌ WRONG - creates new stream each time
    var boss = ctx.GetNextBoss(ref bossStream);
    // ...
}
```

**Correct Pattern (from MotelyAnalyzerFilterDesc.cs):**
```csharp
MotelySingleBossStream bossStream = ctx.CreateBossStream();  // ✅ Create ONCE
for (int ante = 1; ante <= 8; ante++)
{
    MotelyBossBlind boss = ctx.GetBossForAnte(ref bossStream, ante, ref voucherState);
    // ...
}
```

**Test Case:** TheArm in Ante 2 for ALEEB fails to match

---

## 🐛 BUG #3: Planet Filter - Doesn't Check Shop Consumables

**File:** `Motely/filters/MotelyJson/MotelyJsonPlanetFilterDesc.cs`

**Problem:** Only checks Celestial booster packs (line 64-65), not shop consumables where planets commonly appear.

**Code Issue:**
```csharp
// Only checking packs, not shop consumables
var packStream = ctx.CreateBoosterPackStream(ante);
var planetStream = ctx.CreateCelestialPackPlanetStream(ante);
```

**Test Case:** Saturn appears as shop consumable at slot 0 in ALEEB Ante 2 - NOT FOUND

**Fix Required:** Add shop consumable checking similar to how Jokers check shop items.

---

## 🐛 BUG #4: Tarot Filter - Doesn't Check Shop Consumables

**File:** `Motely/filters/MotelyJson/MotelyJsonTarotCardFilterDesc.cs`

**Problem:** Similar to Planet filter - only checks Arcana packs, not shop consumables.

**Test Case:** The Empress appears in shop slot 2 in ALEEB Ante 1 - NOT FOUND

**Fix Required:** Add shop consumable checking.

---

## 🐛 BUG #5: Filter Creation Context Crash

**File:** `Motely/MotelyFilterCreationContext.cs`

**Problem:** NullReferenceException at line 34 in `CachePseudoHash` when `_cachedStreams` is null

**Stack Trace:**
```
System.NullReferenceException : Object reference not set to an instance of an object.
  at Motely.MotelyFilterCreationContext.CachePseudoHash(Int32 keyLength, Boolean force) line 34
```

**Fix Required:** Initialize `_cachedStreams` properly or add null check.

---

## Test Commands to Reproduce

```bash
# Bug #1 - Joker slot 7
cd Motely && dotnet run -c Release -- --seed ALEEB --json test-blueprint-slot7

# Bug #2 - Boss state
cd Motely && dotnet run -c Release -- --seed ALEEB --json test-boss

# Bug #3 - Planet in shop
cd Motely && dotnet run -c Release -- --seed ALEEB --json test-planet-shop

# Bug #4 - Tarot in shop  
cd Motely && dotnet run -c Release -- --seed ALEEB --json test-tarot-shop
```

## Impact

These bugs prevent accurate filtering of seeds when items appear in:
- Later shop slots (7+)
- Shop consumables vs booster packs
- Sequences requiring state (bosses)

The sliced filter chain architecture is sound, but these implementation details need fixing for complete functionality.