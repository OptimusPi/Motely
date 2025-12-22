using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Motely.API;

/// <summary>
/// MCP (Model Context Protocol) Server for natural language to JAML translation and search execution
/// Uses Cloudflare Workers AI REST API to translate user prompts into executable JAML filters
/// Documentation: https://developers.cloudflare.com/workers-ai/get-started/rest-api/
/// </summary>
public class McpServer
{
    private readonly ILogger<McpServer> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly string _workerUrl;
    private readonly string _model;
    private readonly GenieFeedbackService? _feedbackService;

    public McpServer(ILogger<McpServer> logger, HttpClient httpClient, IConfiguration configuration, GenieFeedbackService? feedbackService = null)
    {
        _logger = logger;
        _httpClient = httpClient;
        _configuration = configuration;
        _feedbackService = feedbackService;
        
        var cfConfig = _configuration.GetSection("Cloudflare:WorkersAI");
        _workerUrl = cfConfig["WorkerUrl"] ?? "";
        _model = cfConfig["Model"] ?? "@cf/meta/llama-3.1-8b-instruct";
        
        if (string.IsNullOrEmpty(_workerUrl))
        {
            throw new InvalidOperationException("Cloudflare Worker URL not configured. Please set WorkerUrl in appsettings.json");
        }
    }
    
    /// <summary>
    /// Translates natural language prompt to JAML filter using Cloudflare Workers AI
    /// </summary>
    public async Task<McpResponse> ProcessPromptAsync(string prompt)
    {
        try
        {
            _logger.LogInformation($"Processing MCP prompt with AI: {prompt}");

            // Generate JAML filter using Cloudflare Workers AI
            var jamlFilter = await GenerateJamlWithAIAsync(prompt);
            
            if (string.IsNullOrWhiteSpace(jamlFilter))
            {
                throw new InvalidOperationException("AI failed to generate JAML filter");
            }

            // Extract deck/stake from prompt if specified
            var deck = ExtractDeck(prompt) ?? "Red";
            var stake = ExtractStake(prompt) ?? "White";
            var seedCount = ExtractSeedCount(prompt) ?? 1000;

            // Execute search via SearchManager
            var (results, searchId) = await SearchManager.Instance.StartSearchAsync(
                jamlFilter,
                deck: deck,
                stake: stake,
                seedCount: seedCount
            );

            var columns = SearchManager.Instance.GetColumnNames(searchId);

            return new McpResponse
            {
                Success = true,
                SearchId = searchId,
                JamlFilter = jamlFilter,
                Reasoning = $"AI-generated JAML filter for: {prompt}",
                Results = results,
                Columns = columns,
                Message = $"Generated JAML filter for: {prompt}. Search started with ID: {searchId}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing MCP prompt: {prompt}");
            return new McpResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Generates JAML filter using Cloudflare Worker endpoint
    /// Worker handles AI calls internally using Workers AI binding (best practice: no API tokens needed)
    /// </summary>
    private async Task<string> GenerateJamlWithAIAsync(string userPrompt)
    {
        if (string.IsNullOrEmpty(_workerUrl))
        {
            throw new InvalidOperationException("Cloudflare Worker URL not configured. Please set WorkerUrl in appsettings.json");
        }

        // Enhanced system prompt for better JAML generation
        var systemPrompt = @"You are a JAML (Joker Artifact Markup Language) filter generator for Balatro seed searching.

Your task is to convert natural language requests into valid JAML filter code.

JAML FORMAT RULES (CRITICAL):
1. The 'must:' section MUST be a YAML list (sequence) using '-' prefix
2. Each card requirement uses format: '- {type}: {CardName}'
3. Card types: joker, spectralCard, tarotCard, voucher, booster, planetCard, edition
4. Card names MUST be capitalized (e.g., Blueprint, Showman, Ankh)
5. For multiple copies, repeat the entry (e.g., '- joker: Blueprint' twice for 2 blueprints)

VALID EXAMPLE:
dateCreated: 2025-01-01
name: AI Generated Filter
author: JamlGenie MCP

must:
  - joker: Blueprint
  - joker: Blueprint

IMPORTANT:
- Output ONLY valid JAML code
- NO markdown code blocks (no `yaml or `)
- NO explanations or comments outside the JAML
- Start with dateCreated, name, author fields
- Then the must: section with list items";

        // Combine system prompt with user prompt
        var fullPrompt = $"{systemPrompt}\n\nUser request: {userPrompt}\n\nGenerate the JAML filter:";

        // Worker endpoint expects: { prompt, model }
        var requestBody = new
        {
            prompt = fullPrompt,
            model = _model
        };
        
        _logger.LogInformation($"Calling Cloudflare Worker: {_workerUrl}");

        try
        {
            var response = await _httpClient.PostAsJsonAsync(_workerUrl, requestBody);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Worker error: {response.StatusCode} - {errorContent}");
                throw new HttpRequestException($"Worker error: {response.StatusCode} - {errorContent}");
            }

            // Worker returns: { jaml: "..." } or just the JAML string
            var workerResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
            
            string generatedJaml;
            if (workerResponse.ValueKind == JsonValueKind.Object && workerResponse.TryGetProperty("jaml", out var jamlProperty))
            {
                generatedJaml = jamlProperty.GetString() ?? "";
            }
            else if (workerResponse.ValueKind == JsonValueKind.String)
            {
                generatedJaml = workerResponse.GetString() ?? "";
            }
            else
            {
                // Try as plain text
                generatedJaml = await response.Content.ReadAsStringAsync();
            }
            
            generatedJaml = generatedJaml.Trim();
            
            if (string.IsNullOrWhiteSpace(generatedJaml))
            {
                throw new InvalidOperationException("AI returned empty response");
            }

            // Clean up markdown code blocks if AI adds them
            generatedJaml = CleanMarkdown(generatedJaml);

            // Validate and ensure proper JAML structure
            generatedJaml = EnsureJamlHeader(generatedJaml);

            _logger.LogInformation($"Generated JAML:\n{generatedJaml}");
            
            return generatedJaml;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, $"HTTP error calling Cloudflare Worker");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error calling Cloudflare Worker");
            throw;
        }
    }

    private string CleanMarkdown(string text)
    {
        // Remove markdown code blocks
        text = System.Text.RegularExpressions.Regex.Replace(text, @"^`(?:yaml|yml|jaml)?\s*\n", "", System.Text.RegularExpressions.RegexOptions.Multiline);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\n`\s*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);
        text = text.Trim();
        return text;
    }

    private string EnsureJamlHeader(string jaml)
    {
        // If header is missing, add it
        if (!jaml.Contains("dateCreated:"))
        {
            var header = $@"dateCreated: {DateTime.UtcNow:yyyy-MM-dd}
name: AI Generated Filter
author: JamlGenie MCP

";
            jaml = header + jaml;
        }
        return jaml;
    }

    private string? ExtractDeck(string prompt)
    {
        var match = System.Text.RegularExpressions.Regex.Match(prompt.ToLowerInvariant(), @"deck\s+([a-z]+)");
        if (match.Success)
        {
            var deckName = match.Groups[1].Value;
            return char.ToUpper(deckName[0]) + deckName.Substring(1);
        }
        return null;
    }

    private string? ExtractStake(string prompt)
    {
        var match = System.Text.RegularExpressions.Regex.Match(prompt.ToLowerInvariant(), @"stake\s+([a-z]+)");
        if (match.Success)
        {
            var stakeName = match.Groups[1].Value;
            return char.ToUpper(stakeName[0]) + stakeName.Substring(1);
        }
        return null;
    }

    private int? ExtractSeedCount(string prompt)
    {
        var match = System.Text.RegularExpressions.Regex.Match(prompt.ToLowerInvariant(), @"(\d+)\s+seeds?");
        if (match.Success)
        {
            return int.Parse(match.Groups[1].Value);
        }
        return null;
    }
}

/// <summary>
/// Cloudflare Workers AI API response structure
/// Based on: https://developers.cloudflare.com/workers-ai/get-started/rest-api/
/// </summary>
public class CloudflareAIResponse
{
    [JsonPropertyName("result")]
    public CloudflareAIResult? Result { get; set; }
    
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("errors")]
    public JsonElement[]? Errors { get; set; }
    
    [JsonPropertyName("messages")]
    public JsonElement[]? Messages { get; set; }
}

public class CloudflareAIResult
{
    [JsonPropertyName("response")]
    public string? Response { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// MCP response containing search results and generated JAML
/// </summary>
public class McpResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? SearchId { get; set; }
    public string? JamlFilter { get; set; }
    public string? Reasoning { get; set; }
    public List<SearchResult>? Results { get; set; }
    public List<string>? Columns { get; set; }
}
