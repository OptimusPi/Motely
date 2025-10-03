using System;
using System.Collections.Generic;
using Xunit;
using Motely.Filters;

namespace Motely.Tests;

/// <summary>
/// Unit tests that verify clause conversion pre-calculates optimization data.
/// These tests ensure the hot path doesn't recalculate on every filter invocation.
/// </summary>
public class MotelyJsonFilterClauseTests
{
    [Theory]
    [InlineData(new[] { 0, 5, 9 }, 10)]  // Highest slot 9 → need 10
    [InlineData(new[] { 0, 1, 2 }, 3)]   // Highest slot 2 → need 3
    [InlineData(new[] { 0 }, 1)]         // Highest slot 0 → need 1
    [InlineData(new int[] { }, 8)]       // No slots → default 8
    public void JokerClause_PreCalculatesMaxShopSlotsNeeded(int[] wantedSlots, int expected)
    {
        // Build a JSON clause with specified shop slots
        var jsonClause = new MotelyJsonConfig.MotleyJsonFilterClause
        {
            Type = "Joker",
            Value = "Blueprint",
            Sources = new MotelyJsonConfig.SourcesConfig
            {
                ShopSlots = wantedSlots
            },
            Antes = new[] { 1 }
        };
        jsonClause.InitializeParsedEnums();

        // Convert to specialized clause - THIS is where pre-calculation happens
        var clause = MotelyJsonJokerFilterClause.FromJsonClause(jsonClause);

        // CRITICAL: MaxShopSlotsNeeded must be pre-calculated during conversion!
        // If this fails, the hot path is recalculating on every filter call!
        Assert.Equal(expected, clause.MaxShopSlotsNeeded);
    }

    [Fact]
    public void JokerClause_PreCalculatesAnteArray()
    {
        // Build clause with specific antes
        var jsonClause = new MotelyJsonConfig.MotleyJsonFilterClause
        {
            Type = "Joker",
            Value = "Blueprint",
            Antes = new[] { 1, 3, 7 }
        };
        jsonClause.InitializeParsedEnums();

        var clause = MotelyJsonJokerFilterClause.FromJsonClause(jsonClause);

        // Verify antes are in bool array format for fast SIMD checks
        Assert.False(clause.WantedAntes[0]);
        Assert.True(clause.WantedAntes[1]);
        Assert.False(clause.WantedAntes[2]);
        Assert.True(clause.WantedAntes[3]);
        Assert.False(clause.WantedAntes[4]);
        Assert.False(clause.WantedAntes[5]);
        Assert.False(clause.WantedAntes[6]);
        Assert.True(clause.WantedAntes[7]);
    }

    [Fact]
    public void JokerClause_PreCalculatesShopSlotArray()
    {
        // Build clause with specific shop slots
        var jsonClause = new MotelyJsonConfig.MotleyJsonFilterClause
        {
            Type = "Joker",
            Value = "Joker",
            Sources = new MotelyJsonConfig.SourcesConfig
            {
                ShopSlots = new[] { 0, 2, 5 }
            },
            Antes = new[] { 1 }
        };
        jsonClause.InitializeParsedEnums();

        var clause = MotelyJsonJokerFilterClause.FromJsonClause(jsonClause);

        // Verify shop slots are in bool array format for fast checks
        Assert.True(clause.WantedShopSlots[0]);
        Assert.False(clause.WantedShopSlots[1]);
        Assert.True(clause.WantedShopSlots[2]);
        Assert.False(clause.WantedShopSlots[3]);
        Assert.False(clause.WantedShopSlots[4]);
        Assert.True(clause.WantedShopSlots[5]);

        // Also verify MaxShopSlotsNeeded is correct
        Assert.Equal(6, clause.MaxShopSlotsNeeded); // Highest slot 5 → need 6
    }

    [Fact]
    public void JokerClause_MultipleValues_StoresAsList()
    {
        // Build clause with multiple joker values
        var jsonClause = new MotelyJsonConfig.MotleyJsonFilterClause
        {
            Type = "Joker",
            Values = new[] { "Blueprint", "Brainstorm", "Showman" },
            Antes = new[] { 1 }
        };
        jsonClause.InitializeParsedEnums();

        var clause = MotelyJsonJokerFilterClause.FromJsonClause(jsonClause);

        // Verify JokerTypes list is populated
        Assert.NotNull(clause.JokerTypes);
        Assert.Equal(3, clause.JokerTypes.Count);
        Assert.Contains(MotelyJoker.Blueprint, clause.JokerTypes);
        Assert.Contains(MotelyJoker.Brainstorm, clause.JokerTypes);
        Assert.Contains(MotelyJoker.Showman, clause.JokerTypes);

        // Single JokerType should be null when using multi-value
        Assert.Null(clause.JokerType);
    }

    [Fact]
    public void JokerClause_SingleValue_StoresAsSingle()
    {
        // Build clause with single joker value
        var jsonClause = new MotelyJsonConfig.MotleyJsonFilterClause
        {
            Type = "Joker",
            Value = "Blueprint",
            Antes = new[] { 1 }
        };
        jsonClause.InitializeParsedEnums();

        var clause = MotelyJsonJokerFilterClause.FromJsonClause(jsonClause);

        // Verify single JokerType is populated
        Assert.NotNull(clause.JokerType);
        Assert.Equal(MotelyJoker.Blueprint, clause.JokerType.Value);

        // JokerTypes list should be null for single value
        Assert.Null(clause.JokerTypes);
    }

    [Fact]
    public void JokerClause_EditionEnum_StoredForFastComparison()
    {
        // Build clause with edition requirement - use Blueprint (a regular joker, not soul joker)
        var jsonClause = new MotelyJsonConfig.MotleyJsonFilterClause
        {
            Type = "Joker",
            Value = "Blueprint",
            Edition = "Negative",
            Antes = new[] { 1 }
        };
        jsonClause.InitializeParsedEnums();

        var clause = MotelyJsonJokerFilterClause.FromJsonClause(jsonClause);

        // Verify edition is stored as enum for SIMD comparison
        Assert.NotNull(clause.EditionEnum);
        Assert.Equal(MotelyItemEdition.Negative, clause.EditionEnum.Value);
    }

    [Theory]
    [InlineData(new[] { 0, 1, 8 }, 9)]   // Highest slot 8 → need 9
    [InlineData(new[] { 0, 1, 2 }, 3)]   // Highest slot 2 → need 3
    [InlineData(new[] { 0 }, 1)]         // Highest slot 0 → need 1
    [InlineData(new int[] { }, 16)]      // No slots → default 16 for Tarot
    public void TarotCriteria_PreCalculatesMaxShopSlotsNeeded(int[] wantedSlots, int expected)
    {
        // Build a JSON clause with specified shop slots
        var jsonClause = new MotelyJsonConfig.MotleyJsonFilterClause
        {
            Type = "TarotCard",
            Value = "The Fool",
            Sources = new MotelyJsonConfig.SourcesConfig
            {
                ShopSlots = wantedSlots
            },
            Antes = new[] { 1 }
        };
        jsonClause.InitializeParsedEnums();

        // Convert to specialized clause - THIS is where pre-calculation happens
        var clauses = MotelyJsonTarotFilterClause.ConvertClauses(new List<MotelyJsonConfig.MotleyJsonFilterClause> { jsonClause });
        var criteria = MotelyJsonTarotFilterClause.CreateCriteria(clauses);

        // CRITICAL: MaxShopSlotsNeeded must be pre-calculated in criteria during CreateCriteria!
        Assert.Equal(expected, criteria.MaxShopSlotsNeeded);
    }

    [Theory]
    [InlineData(new[] { 0, 3 }, 4)]      // Highest slot 3 → need 4
    [InlineData(new[] { 0, 1, 2 }, 3)]   // Highest slot 2 → need 3
    [InlineData(new[] { 0 }, 1)]         // Highest slot 0 → need 1
    [InlineData(new int[] { }, 6)]       // No slots → default 6 for Spectral
    public void SpectralCriteria_PreCalculatesMaxShopSlotsNeeded(int[] wantedSlots, int expected)
    {
        // Build a JSON clause with specified shop slots
        var jsonClause = new MotelyJsonConfig.MotleyJsonFilterClause
        {
            Type = "SpectralCard",
            Value = "Ankh",
            Sources = new MotelyJsonConfig.SourcesConfig
            {
                ShopSlots = wantedSlots
            },
            Antes = new[] { 1 }
        };
        jsonClause.InitializeParsedEnums();

        var clauses = MotelyJsonSpectralFilterClause.ConvertClauses(new List<MotelyJsonConfig.MotleyJsonFilterClause> { jsonClause });
        var criteria = MotelyJsonSpectralFilterClause.CreateCriteria(clauses);

        Assert.Equal(expected, criteria.MaxShopSlotsNeeded);
    }

    [Theory]
    [InlineData(new[] { 2, 4, 6 }, 7)]   // Highest slot 6 → need 7
    [InlineData(new[] { 0, 1, 2 }, 3)]   // Highest slot 2 → need 3
    [InlineData(new[] { 0 }, 1)]         // Highest slot 0 → need 1
    [InlineData(new int[] { }, 16)]      // No slots → default 16 for Planet
    public void PlanetCriteria_PreCalculatesMaxShopSlotsNeeded(int[] wantedSlots, int expected)
    {
        // Build a JSON clause with specified shop slots
        var jsonClause = new MotelyJsonConfig.MotleyJsonFilterClause
        {
            Type = "PlanetCard",
            Value = "Pluto",
            Sources = new MotelyJsonConfig.SourcesConfig
            {
                ShopSlots = wantedSlots
            },
            Antes = new[] { 1 }
        };
        jsonClause.InitializeParsedEnums();

        var clauses = MotelyJsonPlanetFilterClause.ConvertClauses(new List<MotelyJsonConfig.MotleyJsonFilterClause> { jsonClause });
        var criteria = MotelyJsonPlanetFilterClause.CreateCriteria(clauses);

        Assert.Equal(expected, criteria.MaxShopSlotsNeeded);
    }

    [Fact]
    public void JokerClause_MinShopSlot_WithoutPackSlots_ShouldNotSearchPacks()
    {
        // Build clause with minShopSlot/maxShopSlot but NO packSlots
        var jsonClause = new MotelyJsonConfig.MotleyJsonFilterClause
        {
            Type = "Joker",
            Values = new[] { "Blueprint", "InvisibleJoker" },
            Antes = new[] { 5 },
            MinShopSlot = 2,  // Start at slot 2, NOT slot 0 or 1
            MaxShopSlot = 10
        };
        jsonClause.InitializeParsedEnums();

        var clause = MotelyJsonJokerFilterClause.FromJsonClause(jsonClause);

        // Verify shop slots 2-9 are wanted
        Assert.False(clause.WantedShopSlots[0]);
        Assert.False(clause.WantedShopSlots[1]);
        Assert.True(clause.WantedShopSlots[2]);
        Assert.True(clause.WantedShopSlots[9]);
        Assert.False(clause.WantedShopSlots[10]);

        // CRITICAL: Pack slots should NOT be wanted when only shop slots specified
        Assert.False(clause.WantedPackSlots[0]);
        Assert.False(clause.WantedPackSlots[1]);
        Assert.False(clause.WantedPackSlots[2]);
        Assert.False(clause.WantedPackSlots[3]);
        Assert.False(clause.WantedPackSlots[4]);
        Assert.False(clause.WantedPackSlots[5]);
    }
}
