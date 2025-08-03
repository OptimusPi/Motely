using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Motely.Filters;

public static class StrictJsonValidator
{
    public static void ValidateJson(string json, string configPath)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        var errors = new List<string>();
        
        var validRootProps = new HashSet<string> { "name", "author", "description", "deck", "stake", "must", "should", "mustNot", "filter" };
        foreach (var prop in root.EnumerateObject())
        {
            if (!validRootProps.Contains(prop.Name))
            {
                errors.Add($"Unknown root property '{prop.Name}'. Valid properties are: {string.Join(", ", validRootProps)}");
            }
        }
        
        // Validate filter items
        if (root.TryGetProperty("must", out var must))
            ValidateFilterItems(must, "must", errors);
        if (root.TryGetProperty("should", out var should))
            ValidateFilterItems(should, "should", errors);
        if (root.TryGetProperty("mustNot", out var mustNot))
            ValidateFilterItems(mustNot, "mustNot", errors);
        
        if (errors.Any())
        {
            throw new JsonException($"JSON validation failed for {configPath}:\n" + string.Join("\n", errors));
        }
    }
    
    private static void ValidateFilterItems(JsonElement items, string section, List<string> errors)
    {
        if (items.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"'{section}' must be an array");
            return;
        }
        
        int index = 0;
        foreach (var item in items.EnumerateArray())
        {
            ValidateFilterItem(item, $"{section}[{index}]", errors);
            index++;
        }
    }
    
    private static void ValidateFilterItem(JsonElement item, string path, List<string> errors)
    {
        var validProps = new HashSet<string> 
        { 
            "item", "type", "value", "antes", "sources", "score", 
            "edition", "stickers", "suit", "rank", "seal", "enhancement",
            "Type", "Value", "SearchAntes", "Score", "Edition", "Stickers",
            "Suit", "Rank", "Seal", "Enhancement"
        };
        
        foreach (var prop in item.EnumerateObject())
        {
            if (!validProps.Contains(prop.Name))
            {
                errors.Add($"Unknown property '{prop.Name}' in {path}. Valid properties are: {string.Join(", ", validProps.OrderBy(x => x))}");
            }
            
            // Validate nested item
            if (prop.Name == "item" && prop.Value.ValueKind == JsonValueKind.Object)
            {
                ValidateItemInfo(prop.Value, $"{path}.item", errors);
            }
            
            // Validate sources
            if (prop.Name == "sources" && prop.Value.ValueKind == JsonValueKind.Object)
            {
                ValidateSources(prop.Value, $"{path}.sources", errors);
            }
        }
    }
    
    private static void ValidateItemInfo(JsonElement item, string path, List<string> errors)
    {
        var validProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { 
            "type", "name", "edition", "rank", "suit", "enhancement", "seal", "value", "stickers"
        };
        
        foreach (var prop in item.EnumerateObject())
        {
            if (!validProps.Contains(prop.Name))
            {
                errors.Add($"Unknown property '{prop.Name}' in {path}. Valid properties are: {string.Join(", ", validProps.OrderBy(x => x))}");
            }
        }
        
        // Type-specific validation
        if (item.TryGetProperty("type", out var typeElem) && typeElem.ValueKind == JsonValueKind.String)
        {
            var type = typeElem.GetString()?.ToLower();
            
            if (type == "playingcard")
            {
                // Playing cards should use rank, not name
                if (item.TryGetProperty("name", out _))
                {
                    errors.Add($"{path}: Playing cards should use 'rank' not 'name' property");
                }
                
                // Validate rank values
                if (item.TryGetProperty("rank", out var rankElem) && rankElem.ValueKind == JsonValueKind.String)
                {
                    var rank = rankElem.GetString();
                    var validRanks = new[] { 
                        "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K",
                        "Ace", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Jack", "Queen", "King",
                        "ace", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "jack", "queen", "king"
                    };
                    if (!string.IsNullOrEmpty(rank) && !validRanks.Contains(rank))
                    {
                        errors.Add($"{path}: Invalid rank '{rank}'. Valid ranks are: A, 2-10, J, Q, K (or spelled out like Two, Three, etc.)");
                    }
                }
            }
            else if (type == "joker" || type == "souljoker")
            {
                // Jokers should use name, not rank
                if (item.TryGetProperty("rank", out _))
                {
                    errors.Add($"{path}: Jokers should use 'name' not 'rank' property");
                }
            }
        }
    }
    
    private static void ValidateSources(JsonElement sources, string path, List<string> errors)
    {
        var validProps = new HashSet<string> { "shopSlots", "packSlots", "tags", "requireMega" };
        
        foreach (var prop in sources.EnumerateObject())
        {
            if (!validProps.Contains(prop.Name))
            {
                errors.Add($"Unknown property '{prop.Name}' in {path}. Valid properties are: {string.Join(", ", validProps)}");
            }
            
            // Validate shopSlots array
            if (prop.Name == "shopSlots" && prop.Value.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (var slot in prop.Value.EnumerateArray())
                {
                    if (slot.ValueKind != JsonValueKind.Number || !slot.TryGetInt32(out var slotNum))
                    {
                        errors.Add($"{path}.shopSlots[{index}]: Must be an integer");
                    }
                    else if (slotNum < 0 || slotNum > 999)
                    {
                        errors.Add($"{path}.shopSlots[{index}]: Slot {slotNum} out of range. Valid slots are 0-999");
                    }
                    index++;
                }
            }
            
            // Validate packSlots array
            if (prop.Name == "packSlots" && prop.Value.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (var slot in prop.Value.EnumerateArray())
                {
                    if (slot.ValueKind != JsonValueKind.Number || !slot.TryGetInt32(out var slotNum))
                    {
                        errors.Add($"{path}.packSlots[{index}]: Must be an integer");
                    }
                    else if (slotNum < 0 || slotNum > 5)
                    {
                        errors.Add($"{path}.packSlots[{index}]: Slot {slotNum} out of range. Valid slots are 0-5");
                    }
                    index++;
                }
            }
            
            // Validate tags
            if (prop.Name == "tags" && prop.Value.ValueKind != JsonValueKind.True && prop.Value.ValueKind != JsonValueKind.False)
            {
                errors.Add($"{path}.tags: Must be a boolean (true/false)");
            }
            
            // Validate requireMega
            if (prop.Name == "requireMega" && prop.Value.ValueKind != JsonValueKind.True && prop.Value.ValueKind != JsonValueKind.False)
            {
                errors.Add($"{path}.requireMega: Must be a boolean (true/false)");
            }
        }
    }
}