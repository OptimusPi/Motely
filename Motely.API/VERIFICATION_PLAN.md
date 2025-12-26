# Game Mechanics Verification Plan

## Immediate Actions

### 1. Check Balatro Source Code (Lua Files)
**Location:** `X:\BalatroSeedOracle\external\Balatro\*.lua`

**Search Commands:**
```bash
# Search for Lucky Cat unlock logic
grep -r "luckycat\|LuckyCat" X:\BalatroSeedOracle\external\Balatro\*.lua

# Search for unlock conditions
grep -r "unlock\|locked" X:\BalatroSeedOracle\external\Balatro\*.lua -i

# Search for magic card references
grep -r "magic\|gold_seal\|lucky_seal" X:\BalatroSeedOracle\external\Balatro\*.lua -i
```

**What to Document:**
- Lucky Cat unlock requirement (if found)
- Magic standard card definition
- Other locked items and their requirements
- Code file names and line numbers

### 2. Search Balatro Wiki
**Search Terms:**
- "Lucky Cat unlock"
- "Lucky Cat magic card"
- "locked jokers"
- "unlock conditions"

**What to Document:**
- Wiki URLs
- Exact unlock requirements
- Other locked items mentioned

### 3. Analyze Test Seeds
**Pattern Check:**
- Search all test seeds for "Lucky Cat" in Ante 1
- If none found → supports unlock theory
- Check correlation with magic cards in earlier antes

**Command:**
```bash
# Search test seeds
grep -r "Lucky Cat" Motely.Tests/seeds/*.txt | grep "ANTE 1"
```

## Verification Checklist

- [ ] Lucky Cat unlock logic found in Lua source
- [ ] Magic standard card definition verified
- [ ] Other locked items documented
- [ ] Wiki information cross-referenced
- [ ] Test seed patterns analyzed
- [ ] `GAME_MECHANICS_MASTER.md` updated with verified info
- [ ] Unverified items marked with ⚠️

## Next Steps After Verification

1. Update `GAME_MECHANICS_MASTER.md` with verified mechanics
2. Add code references (file names, line numbers)
3. Mark unverified items clearly
4. Add to AI system prompt for better accuracy

