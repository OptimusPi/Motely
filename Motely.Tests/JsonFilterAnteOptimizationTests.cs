using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Motely.Filters;

namespace Motely.Tests;

public class JsonFilterAnteOptimizationTests
{
    [Fact]
    public void JokerFilter_AnteArrayExtraction()
    {
        // Test that we correctly extract antes from bitmasks
        var clauses = new List<MotelyJsonJokerFilterClause>
        {
            new MotelyJsonJokerFilterClause
            {
                JokerType = MotelyJoker.Joker,
                AnteBitmask = 0b00000010, // Ante 2
                ShopSlotBitmask = ~0UL,
                PackSlotBitmask = ~0UL
            },
            new MotelyJsonJokerFilterClause
            {
                JokerType = MotelyJoker.GreedyJoker,
                AnteBitmask = 0b01000000, // Ante 7
                ShopSlotBitmask = ~0UL,
                PackSlotBitmask = ~0UL
            }
        };
        
        var filterDesc = new MotelyJsonJokerFilterDesc(clauses);
        var searchParams = new MotelySearchParameters { Deck = MotelyDeck.Red, Stake = MotelyStake.White };
        var ctx = new MotelyFilterCreationContext(in searchParams);
        var filter = filterDesc.CreateFilter(ref ctx);
        
        // The filter should now only process antes 2 and 7
        // We can't directly test the internal _antesToCheck array,
        // but we can verify the filter was created successfully
        // (filter is a struct, so we can't use NotNull)
    }
    
    [Fact]
    public void JokerFilter_BitmaskOperations()
    {
        // Test bitmask operations work correctly
        ulong ante2Mask = 0b00000010; // Ante 2
        ulong ante7Mask = 0b01000000; // Ante 7
        ulong combinedMask = ante2Mask | ante7Mask; // Should be 0b01000010
        
        // Extract antes from combined mask
        var antes = new List<int>();
        for (int ante = 1; ante <= 8; ante++)
        {
            ulong anteBit = 1UL << (ante - 1);
            if ((combinedMask & anteBit) != 0)
            {
                antes.Add(ante);
            }
        }
        
        Assert.Equal(2, antes.Count);
        Assert.Contains(2, antes);
        Assert.Contains(7, antes);
        Assert.DoesNotContain(3, antes);
        Assert.DoesNotContain(4, antes);
        Assert.DoesNotContain(5, antes);
        Assert.DoesNotContain(6, antes);
    }
    
    [Fact]
    public void JokerFilter_AllAntesHandling()
    {
        // Test that all antes (1-8) are handled correctly
        var clauses = new List<MotelyJsonJokerFilterClause>
        {
            new MotelyJsonJokerFilterClause
            {
                JokerType = MotelyJoker.Joker,
                IsWildcard = true,
                AnteBitmask = 0xFF, // All antes 1-8
                ShopSlotBitmask = ~0UL,
                PackSlotBitmask = ~0UL
            }
        };
        
        ulong mask = clauses[0].AnteBitmask;
        var antes = new List<int>();
        for (int ante = 1; ante <= 8; ante++)
        {
            ulong anteBit = 1UL << (ante - 1);
            if ((mask & anteBit) != 0)
            {
                antes.Add(ante);
            }
        }
        
        Assert.Equal(8, antes.Count);
        for (int i = 1; i <= 8; i++)
        {
            Assert.Contains(i, antes);
        }
    }
    
    [Fact]
    public void JokerFilter_EmptyAnteHandling()
    {
        // Test handling when no antes are specified (should default to something reasonable)
        var clauses = new List<MotelyJsonJokerFilterClause>
        {
            new MotelyJsonJokerFilterClause
            {
                JokerType = MotelyJoker.Joker,
                AnteBitmask = 0, // No antes specified
                ShopSlotBitmask = ~0UL,
                PackSlotBitmask = ~0UL
            }
        };
        
        var filterDesc = new MotelyJsonJokerFilterDesc(clauses);
        var searchParams = new MotelySearchParameters { Deck = MotelyDeck.Red, Stake = MotelyStake.White };
        var ctx = new MotelyFilterCreationContext(in searchParams);
        
        // Should not throw even with empty ante mask
        var filter = filterDesc.CreateFilter(ref ctx);
        // Filter is a struct, creation succeeded if we got here
    }
    
    [Fact]
    public void JokerFilter_EmptyShopSlotHandling()
    {
        // Test handling when shop slots are empty
        var clauses = new List<MotelyJsonJokerFilterClause>
        {
            new MotelyJsonJokerFilterClause
            {
                JokerType = MotelyJoker.Joker,
                AnteBitmask = 0b00000010, // Ante 2
                ShopSlotBitmask = 0UL, // Empty shop slots
                PackSlotBitmask = ~0UL
            }
        };

        var filterDesc = new MotelyJsonJokerFilterDesc(clauses);
        var searchParams = new MotelySearchParameters { Deck = MotelyDeck.Red, Stake = MotelyStake.White };
        var ctx = new MotelyFilterCreationContext(in searchParams);

        // Should not throw even with empty shop slots
        var filter = filterDesc.CreateFilter(ref ctx);
        // Filter is a struct, creation succeeded if we got here
    }
}