using Motely.Filters.Ouija.State;

namespace Motely.Filters.Ouija.ComplexFilters;

/// <summary>
/// Handles soul joker checking with proper duplicate prevention and pack consumption tracking.
/// Soul jokers are complex because:
/// 1. They need duplicate prevention (can't get same joker twice unless Showman)
/// 2. Each soul pack can only be consumed once
/// 3. Multiple clauses might want the same ante's souls
/// </summary>
public static class SoulJokerFilter
{
    /// <summary>
    /// Process all soul joker clauses for a specific ante.
    /// This ensures proper pack consumption - each soul can only be claimed once.
    /// </summary>
    public static int ProcessAnteWithAllClauses(
        ref MotelySingleSearchContext ctx,
        int ante,
        IEnumerable<OuijaConfig.FilterItem> soulClauses,
        ref OuijaRunState runState)
    {
        int totalMatches = 0;
        var packStream = ctx.CreateBoosterPackStream(ante, isCached: false);
        var soulStream = ctx.CreateSoulJokerStream(ante, MotelyJokerStreamFlags.Default);
        
        // Get all pack slots we need to check
        var relevantClauses = soulClauses.Where(c => c.EffectiveAntes?.Contains(ante) ?? false).ToList();
        if (!relevantClauses.Any()) return 0;
        
        // Process each pack in the ante
        int maxPackSlot = 6; // Default max packs
        foreach (var clause in relevantClauses)
        {
            if (clause.Sources?.PackSlots?.Length > 0)
            {
                maxPackSlot = Math.Max(maxPackSlot, clause.Sources.PackSlots.Max());
            }
        }
        
        for (int packSlot = 0; packSlot <= maxPackSlot; packSlot++)
        {
            var pack = ctx.GetNextBoosterPack(ref packStream);
            
            // Skip if not Arcana or Spectral
            if (pack.GetPackType() != MotelyBoosterPackType.Arcana && 
                pack.GetPackType() != MotelyBoosterPackType.Spectral)
                continue;
            
            // Skip if already consumed
            if (runState.IsSoulPackConsumed(ante, packSlot))
            {
                DebugLogger.Log($"[SoulJoker] Pack {packSlot} already consumed, skipping");
                continue;
            }
            
            // Check if this pack has a soul
            bool hasSoul = CheckPackHasSoul(ref ctx, pack, ante);
            if (!hasSoul) continue;
            
            // Get the soul joker with duplicate prevention
            var soulJoker = GetSoulJokerWithReroll(ref ctx, ref soulStream, ref runState.BaseState);
            
            // Find which clause(s) this joker matches
            foreach (var clause in relevantClauses)
            {
                // Check if this clause wants this pack slot
                if (clause.Sources?.PackSlots != null && 
                    !clause.Sources.PackSlots.Contains(packSlot))
                    continue;
                
                // Check mega requirement
                if (clause.Sources?.RequireMega == true && 
                    pack.GetPackSize() != MotelyBoosterPackSize.Mega)
                    continue;
                
                // Check if joker matches
                if (JokerMatchesClause(soulJoker, clause))
                {
                    // Claim this soul for this clause
                    runState.MarkSoulPackConsumed(ante, packSlot);
                    runState.AddOwnedJoker(soulJoker);
                    
                    // Track Showman if applicable
                    if (soulJoker.Type == MotelyItemType.Showman)
                    {
                        runState.ActivateShowman();
                        DebugLogger.Log("[SoulJoker] Activated Showman - duplicates now allowed!");
                    }
                    
                    totalMatches++;
                    DebugLogger.Log($"[SoulJoker] MATCH! {soulJoker.Type} claimed by clause");
                    break; // This soul is consumed
                }
            }
        }
        
        return totalMatches;
    }
    
    private static bool CheckPackHasSoul(
        ref MotelySingleSearchContext ctx,
        MotelyBoosterPack pack,
        int ante)
    {
        if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
        {
            var tarotStream = ctx.CreateArcanaPackTarotStream(ante, soulOnly: true);
            var contents = ctx.GetNextArcanaPackContents(ref tarotStream, pack.GetPackSize());
            
            for (int i = 0; i < contents.Length; i++)
            {
                if (contents[i] == MotelyItemType.Soul)
                    return true;
            }
        }
        else if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
        {
            var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: false);
            var contents = ctx.GetNextSpectralPackContents(ref spectralStream, pack.GetPackSize());
            
            for (int i = 0; i < contents.Length; i++)
            {
                if (contents[i] == MotelyItemType.Soul)
                    return true;
            }
        }
        
        return false;
    }
    
    private static MotelyItem GetSoulJokerWithReroll(
        ref MotelySingleSearchContext ctx,
        ref MotelySingleJokerFixedRarityStream soulStream,
        ref MotelyRunState runState)
    {
        MotelyItem soulJoker;
        int rerollCount = 0;
        const int maxRerolls = 100;
        
        do
        {
            soulJoker = ctx.GetNextJoker(ref soulStream);
            
            // Check if we can obtain this joker
            if (runState.CanObtainJoker(soulJoker))
            {
                break;
            }
            
            if (DebugLogger.IsEnabled)
            {
                DebugLogger.Log($"[SoulJoker] Re-roll #{rerollCount + 1}: {soulJoker.Type} already owned");
            }
            
            rerollCount++;
        } while (rerollCount < maxRerolls);
        
        if (rerollCount > 0)
        {
            DebugLogger.Log($"[SoulJoker] After {rerollCount} re-rolls, got {soulJoker.Type}");
        }
        
        return soulJoker;
    }
    
    private static bool JokerMatchesClause(MotelyItem joker, OuijaConfig.FilterItem clause)
    {
        // Check type match
        bool typeMatches = !clause.JokerEnum.HasValue || 
                          joker.Type == new MotelyItem(clause.JokerEnum.Value).Type;
        
        if (!typeMatches) return false;
        
        // Check edition/stickers if specified
        if (clause.EditionEnum.HasValue && joker.Edition != clause.EditionEnum.Value)
            return false;
        
        // TODO: Check sticker requirements
        
        return true;
    }
}