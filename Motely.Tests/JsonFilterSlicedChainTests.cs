using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;
using Motely.Filters;
using Motely.Utils;
using Motely.Executors;

namespace Motely.Tests;

public sealed class JsonFilterSlicedChainTests
{
    [Fact]
    public void SlicedFilterChain_RealJsonFile_VerifiesALEEBBlueprint()
    {
        // Load the real JSON configuration file - EXACTLY like JsonSearchExecutor.LoadConfig()
        var configFileName = "test-aleeb-unit";
        var configPath = Path.Combine("JsonItemFilters", configFileName + ".json");
        var fullConfigPath = Path.Combine("..", "..", "..", "..", "Motely", configPath);
        
        Assert.True(File.Exists(fullConfigPath), $"JSON config file not found at: {fullConfigPath}");
        
        var loadSuccess = MotelyJsonConfig.TryLoadFromJsonFile(fullConfigPath, out var config, out var error);
        Assert.True(loadSuccess, $"Failed to load config: {error}");
        Assert.NotNull(config);
        
        // Verify the config contains the expected Blueprint clause
        Assert.NotNull(config.Must);
        Assert.Single(config.Must);
        var blueprintClause = config.Must[0];
        Assert.Equal("joker", blueprintClause.Type);
        Assert.Equal("Blueprint", blueprintClause.Value);
        
        // EXACTLY replicate JsonSearchExecutor.CreateSearch() logic
        // Step 1: Group clauses by category (PROPER SLICING)
        var mustClauses = config.Must?.ToList() ?? new List<MotelyJsonConfig.MotleyJsonFilterClause>();
        var clausesByCategory = FilterCategoryMapper.GroupClausesByCategory(mustClauses);
        
        Assert.Single(clausesByCategory);
        Assert.True(clausesByCategory.ContainsKey(FilterCategory.Joker));
        
        // Step 2: Create base filter with first category
        var categories = clausesByCategory.Keys.ToList();
        var primaryCategory = categories[0];
        var primaryClauses = clausesByCategory[primaryCategory];
        
        // Step 3: Create specialized filter based on category (matching switch statement)
        IMotelySeedFilterDesc filterDesc = primaryCategory switch
        {
            FilterCategory.Joker => CreateJokerFilterDesc(primaryClauses),
            _ => throw new ArgumentException($"Specialized filter not implemented: {primaryCategory}")
        };
        
        // Step 4: Create search settings with explicit typing
        dynamic searchSettings = primaryCategory switch
        {
            FilterCategory.Joker => new MotelySearchSettings<MotelyJsonJokerFilterDesc.MotelyJsonJokerFilter>((MotelyJsonJokerFilterDesc)filterDesc),
            _ => throw new ArgumentException($"Search settings not implemented: {primaryCategory}")
        };
        
        // Step 5: Chain additional filters (none in this case since we only have one category)
        for (int i = 1; i < categories.Count; i++)
        {
            var category = categories[i];
            var clauses = clausesByCategory[category];
            var additionalFilter = CreateFilterForCategory(category, clauses);
            searchSettings = searchSettings.WithAdditionalFilter(additionalFilter); // FIX: Capture return value
        }
        
        // Apply deck and stake
        searchSettings = searchSettings.WithDeck(MotelyDeck.Red);
        searchSettings = searchSettings.WithStake(MotelyStake.White);
        
        // TEST THE FILTER AGAINST ALEEB - not analyze ALEEB!
        // Create a list search with just ALEEB
        var seedsToTest = new List<string> { "ALEEB" };
        IMotelySearch search = searchSettings.WithListSearch(seedsToTest).Start();
        
        // Wait for search to complete
        search.AwaitCompletion();
        
        // Verify ALEEB matched the filter
        Assert.Equal(MotelySearchStatus.Completed, search.Status);
        Assert.Equal(1, search.TotalSeedsSearched);
        Assert.Equal(1, search.MatchingSeeds);
        
        // Verify the converted clause has the correct properties
        var convertedClauses = MotelyJsonJokerFilterClause.ConvertClauses(primaryClauses);
        Assert.Single(convertedClauses);
        var jokerClause = convertedClauses[0];
        Assert.Equal(MotelyJoker.Blueprint, jokerClause.JokerType);
        Assert.Equal(0b00000010UL, jokerClause.AnteBitmask); // Ante 2 only
    }
    
    // Helper method matching JsonSearchExecutor.CreateJokerFilterDesc()
    private static MotelyJsonJokerFilterDesc CreateJokerFilterDesc(List<MotelyJsonConfig.MotleyJsonFilterClause> clauses)
    {
        var typedClauses = MotelyJsonJokerFilterClause.ConvertClauses(clauses);
        return new MotelyJsonJokerFilterDesc(typedClauses);
    }
    
    // Helper method for creating filters for additional categories
    private static IMotelySeedFilterDesc CreateFilterForCategory(FilterCategory category, List<MotelyJsonConfig.MotleyJsonFilterClause> clauses)
    {
        return category switch
        {
            FilterCategory.SoulJoker => new MotelyJsonSoulJokerFilterDesc(MotelyJsonSoulJokerFilterClause.ConvertClauses(clauses)),
            FilterCategory.Joker => CreateJokerFilterDesc(clauses),
            FilterCategory.Voucher => new MotelyJsonVoucherFilterDesc(MotelyJsonVoucherFilterClause.ConvertClauses(clauses)),
            FilterCategory.PlanetCard => new MotelyJsonPlanetFilterDesc(MotelyJsonPlanetFilterClause.ConvertClauses(clauses)),
            FilterCategory.TarotCard => new MotelyJsonTarotCardFilterDesc(MotelyJsonTarotFilterClause.ConvertClauses(clauses)),
            FilterCategory.SpectralCard => new MotelyJsonSpectralCardFilterDesc(MotelyJsonSpectralFilterClause.ConvertClauses(clauses)),
            FilterCategory.PlayingCard => new MotelyJsonPlayingCardFilterDesc(clauses),
            FilterCategory.Boss => new MotelyJsonBossFilterDesc(clauses),
            FilterCategory.Tag => new MotelyJsonTagFilterDesc(clauses),
            _ => throw new ArgumentException($"Additional filter not implemented: {category}")
        };
    }
    
    [Fact]
    public void SlicedFilterChain_MultipleCategories_ProperlyChains()
    {
        // Create a config with multiple filter categories
        var config = new MotelyJsonConfig
        {
            Name = "Multi-Category Test",
            Must = new List<MotelyJsonConfig.MotleyJsonFilterClause>
            {
                // Joker clause
                new MotelyJsonConfig.MotleyJsonFilterClause
                {
                    Type = "Joker",
                    Value = "Perkeo",
                    Antes = new int[] { 3 },
                    Sources = new MotelyJsonConfig.SourcesConfig
                    {
                        ShopSlots = new int[] { 0, 1, 2, 3, 4 }
                    }
                },
                // Voucher clause
                new MotelyJsonConfig.MotleyJsonFilterClause
                {
                    Type = "Voucher",
                    Value = "Observatory",
                    Antes = new int[] { 2 }
                },
                // Tarot clause
                new MotelyJsonConfig.MotleyJsonFilterClause
                {
                    Type = "TarotCard",
                    Value = "The Fool",
                    Antes = new int[] { 1 },
                    Sources = new MotelyJsonConfig.SourcesConfig
                    {
                        PackSlots = new int[] { 0, 1, 2 }
                    }
                }
            },
            Should = new List<MotelyJsonConfig.MotleyJsonFilterClause>(),
            MustNot = new List<MotelyJsonConfig.MotleyJsonFilterClause>()
        };
        
        // Initialize parsed enums for all clauses
        foreach (var filterClause in config.Must)
        {
            filterClause.InitializeParsedEnums();
        }
        
        // Test the slicing mechanism groups correctly
        var clausesByCategory = FilterCategoryMapper.GroupClausesByCategory(config.Must);
        
        Assert.Equal(3, clausesByCategory.Count);
        Assert.True(clausesByCategory.ContainsKey(FilterCategory.Joker));
        Assert.True(clausesByCategory.ContainsKey(FilterCategory.Voucher));
        Assert.True(clausesByCategory.ContainsKey(FilterCategory.TarotCard));
        
        // Verify each category has the correct number of clauses
        Assert.Single(clausesByCategory[FilterCategory.Joker]);
        Assert.Single(clausesByCategory[FilterCategory.Voucher]);
        Assert.Single(clausesByCategory[FilterCategory.TarotCard]);
        
        // Create specialized filters for each category
        var jokerFilterDesc = new MotelyJsonJokerFilterDesc(
            MotelyJsonJokerFilterClause.ConvertClauses(clausesByCategory[FilterCategory.Joker]));
        
        var voucherFilterDesc = new MotelyJsonVoucherFilterDesc(
            MotelyJsonVoucherFilterClause.ConvertClauses(clausesByCategory[FilterCategory.Voucher]));
        
        var tarotFilterDesc = new MotelyJsonTarotCardFilterDesc(
            MotelyJsonTarotFilterClause.ConvertClauses(clausesByCategory[FilterCategory.TarotCard]));
        
        // Create filter contexts
        var searchParams = new MotelySearchParameters { Deck = MotelyDeck.Red, Stake = MotelyStake.White };
        var ctx1 = new MotelyFilterCreationContext(in searchParams);
        var ctx2 = new MotelyFilterCreationContext(in searchParams);
        var ctx3 = new MotelyFilterCreationContext(in searchParams);
        
        // Verify filters can be created
        var jokerFilter = jokerFilterDesc.CreateFilter(ref ctx1);
        var voucherFilter = voucherFilterDesc.CreateFilter(ref ctx2);
        var tarotFilter = tarotFilterDesc.CreateFilter(ref ctx3);
        
        // All filters created successfully (structs, so no null check needed)
    }
    
    [Fact]
    public void SlicedFilterChain_OptimizedAnteExtraction()
    {
        // Test that ante bitmasks are correctly extracted for optimization
        var config = new MotelyJsonConfig
        {
            Name = "Ante Optimization Test",
            Must = new List<MotelyJsonConfig.MotleyJsonFilterClause>
            {
                new MotelyJsonConfig.MotleyJsonFilterClause
                {
                    Type = "Joker",
                    Value = "Any",
                    Antes = new int[] { 2, 4, 7 } // Sparse antes for optimization test
                }
            },
            Should = new List<MotelyJsonConfig.MotleyJsonFilterClause>(),
            MustNot = new List<MotelyJsonConfig.MotleyJsonFilterClause>()
        };
        
        // Initialize parsed enums for all clauses
        foreach (var filterClause in config.Must)
        {
            filterClause.InitializeParsedEnums();
        }
        
        var clausesByCategory = FilterCategoryMapper.GroupClausesByCategory(config.Must);
        var jokerClauses = clausesByCategory[FilterCategory.Joker];
        var convertedClauses = MotelyJsonJokerFilterClause.ConvertClauses(jokerClauses);
        
        var clause = convertedClauses[0];
        
        // Calculate expected bitmask: antes 2, 4, 7
        // Ante 2 = bit 1, Ante 4 = bit 3, Ante 7 = bit 6
        ulong expectedMask = (1UL << 1) | (1UL << 3) | (1UL << 6); // 0b01001010
        Assert.Equal(expectedMask, clause.AnteBitmask);
        
        // Extract antes from bitmask to verify
        var extractedAntes = new List<int>();
        for (int ante = 1; ante <= 8; ante++)
        {
            if ((clause.AnteBitmask & (1UL << (ante - 1))) != 0)
            {
                extractedAntes.Add(ante);
            }
        }
        
        Assert.Equal(3, extractedAntes.Count);
        Assert.Contains(2, extractedAntes);
        Assert.Contains(4, extractedAntes);
        Assert.Contains(7, extractedAntes);
    }
    
    [Fact]
    public void SlicedFilterChain_SoulJokerFiltering()
    {
        // Test the specialized SoulJoker filter in the sliced chain
        var config = new MotelyJsonConfig
        {
            Name = "Soul Joker Test",
            Must = new List<MotelyJsonConfig.MotleyJsonFilterClause>
            {
                new MotelyJsonConfig.MotleyJsonFilterClause
                {
                    Type = "SoulJoker",
                    Value = "Perkeo",
                    Antes = new int[] { 3 }
                }
            },
            Should = new List<MotelyJsonConfig.MotleyJsonFilterClause>(),
            MustNot = new List<MotelyJsonConfig.MotleyJsonFilterClause>()
        };
        
        // Initialize parsed enums for all clauses
        foreach (var filterClause in config.Must)
        {
            filterClause.InitializeParsedEnums();
        }
        
        var clausesByCategory = FilterCategoryMapper.GroupClausesByCategory(config.Must);
        
        Assert.Single(clausesByCategory);
        Assert.True(clausesByCategory.ContainsKey(FilterCategory.SoulJoker));
        
        var soulJokerClauses = clausesByCategory[FilterCategory.SoulJoker];
        var convertedClauses = MotelyJsonSoulJokerFilterClause.ConvertClauses(soulJokerClauses);
        
        Assert.Single(convertedClauses);
        var clause = convertedClauses[0];
        Assert.Equal(MotelyJoker.Perkeo, clause.JokerType);
        Assert.Equal(0b00000100UL, clause.AnteBitmask); // Ante 3
        
        // Create the filter
        var filterDesc = new MotelyJsonSoulJokerFilterDesc(convertedClauses);
        var searchParams = new MotelySearchParameters { Deck = MotelyDeck.Red, Stake = MotelyStake.White };
        var ctx = new MotelyFilterCreationContext(in searchParams);
        var filter = filterDesc.CreateFilter(ref ctx);
        
        // Filter created successfully
    }
}