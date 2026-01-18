using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Motely;
using Motely.Filters;
using GenieFeedbackService = global::Motely.API.GenieFeedbackService;
using SearchManager = global::Motely.API.SearchManager;
using SearchResult = global::Motely.API.SearchResult;

namespace Motely.MCP;

public class McpServer
{
    private readonly ILogger<McpServer> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly string _workerUrl;
    private readonly string _model;
    private readonly GenieFeedbackService? _feedbackService;

    public McpServer(
        ILogger<McpServer> logger,
        HttpClient httpClient,
        IConfiguration configuration,
        GenieFeedbackService? feedbackService = null
    )
    {
        _logger = logger;
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _configuration = configuration;
        _feedbackService = feedbackService;

        var cfConfig = _configuration.GetSection("Cloudflare:WorkersAI");
        _workerUrl = cfConfig["WorkerUrl"] ?? "";
        _model = cfConfig["Model"] ?? "@cf/meta/llama-3.1-8b-instruct-fp8";

        if (string.IsNullOrEmpty(_workerUrl))
        {
            throw new InvalidOperationException(
                "Cloudflare Worker URL not configured. Please set WorkerUrl in appsettings.json"
            );
        }
    }

    public async Task<(string jaml, string reasoning, string? error)> GenerateJamlOnlyAsync(
        string prompt
    )
    {
        return await GenerateJamlOnlyAsyncInternal(prompt, prompt);
    }

    private async Task<(
        string jaml,
        string reasoning,
        string? error,
        string? rawJaml,
        string? cleanedJaml,
        string? finalJaml,
        string? validationError
    )> GenerateJamlOnlyAsyncInternalWithSteps(string userPrompt, string originalPrompt)
    {
        string? rawJaml = null;
        string? cleanedJaml = null;
        string? finalJaml = null;
        string? validationError = null;

        try
        {
            _logger.LogInformation($"Generating JAML only (no search): '{originalPrompt}'");

            // Ensure prompt is not empty
            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                _logger.LogWarning($"Prompt is empty. Original: '{originalPrompt}'");
                userPrompt = originalPrompt;
            }

            // Generate JAML filter using Cloudflare Workers AI
            // LLM handles typos, slang, fuzzy matching via system prompt - no regex needed
            var jamlFilter = await GenerateJamlWithAIAsync(
                userPrompt,
                originalPrompt: originalPrompt
            );
            rawJaml = jamlFilter;

            if (string.IsNullOrWhiteSpace(jamlFilter))
            {
                return (
                    string.Empty,
                    string.Empty,
                    "AI failed to generate JAML filter",
                    rawJaml,
                    null,
                    null,
                    null
                );
            }

            cleanedJaml = CleanMarkdown(jamlFilter);
            finalJaml = EnsureJamlHeader(cleanedJaml, originalPrompt);
            jamlFilter = finalJaml;

            if (
                !JamlConfigLoader.TryLoadFromJamlString(
                    jamlFilter,
                    out var validatedConfig,
                    out validationError
                )
                || validatedConfig == null
            )
            {
                _feedbackService?.LogFailure(
                    prompt: originalPrompt,
                    generatedJaml: jamlFilter,
                    aiReasoning: $"AI-generated JAML that failed validation",
                    error: validationError ?? "Unknown validation error",
                    context: new
                    {
                        validationError = validationError,
                        rawJaml = rawJaml,
                        cleanedJaml = cleanedJaml,
                        withHeaderJaml = finalJaml,
                    }
                );

                // Include the generated JAML in error message for debugging
                var errorMsg =
                    $"JAML validation failed: {validationError}\n\n=== TRANSFORMATION PIPELINE ===\n"
                    + $"Raw from AI:\n{rawJaml}\n\n"
                    + $"After CleanMarkdown:\n{cleanedJaml}\n\n"
                    + $"After EnsureJamlHeader:\n{finalJaml}\n\n"
                    + $"Validation Error: {validationError}";
                _logger.LogError(
                    $"JAML validation failed for prompt: '{originalPrompt}'\nError: {validationError}\nGenerated JAML:\n{jamlFilter}"
                );

                return (
                    jamlFilter,
                    string.Empty,
                    errorMsg,
                    rawJaml,
                    cleanedJaml,
                    finalJaml,
                    validationError
                );
            }

            var reasoning = $"AI-generated JAML filter for: {originalPrompt}";
            return (jamlFilter, reasoning, null, rawJaml, cleanedJaml, finalJaml, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error generating JAML: {originalPrompt}");
            return (
                string.Empty,
                string.Empty,
                $"Error: {ex.Message}",
                rawJaml,
                cleanedJaml,
                finalJaml,
                validationError
            );
        }
    }

    private async Task<(
        string jaml,
        string reasoning,
        string? error
    )> GenerateJamlOnlyAsyncInternal(string userPrompt, string originalPrompt)
    {
        var (jaml, reasoning, error, _, _, _, _) = await GenerateJamlOnlyAsyncInternalWithSteps(
            userPrompt,
            originalPrompt
        );
        return (jaml, reasoning, error);
    }

    public async Task<McpResponse> ProcessPromptAsync(string prompt)
    {
        try
        {
            var (jamlFilter, reasoning, error) = await GenerateJamlOnlyAsyncInternal(
                prompt,
                prompt
            );

            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException(error);
            }

            if (string.IsNullOrWhiteSpace(jamlFilter))
            {
                throw new InvalidOperationException("AI failed to generate JAML filter");
            }

            // Load config to extract deck/stake
            if (
                !JamlConfigLoader.TryLoadFromJamlString(jamlFilter, out var validatedConfig, out _)
                || validatedConfig == null
            )
            {
                throw new InvalidOperationException("Failed to load validated config");
            }

            // Extract deck/stake from validated config or prompt
            var deck = validatedConfig.Deck ?? ExtractDeck(prompt) ?? "Red";
            var stake = validatedConfig.Stake ?? ExtractStake(prompt) ?? "White";
            // Default to 1 million random seeds (not forever search, not user-configurable for security)
            var seedSource = "random:1000000";

            // Execute search via SearchManager (validation already passed)
            var (results, searchId) = await SearchManager.Instance.StartSearchAsync(
                jamlFilter,
                deck: deck,
                stake: stake,
                seedCount: 0, // Not used when seedSource is set
                seedSource: seedSource
            );

            var columns = SearchManager.Instance.GetColumnNames(searchId);

            // Generate search URL for linking to JAML UI
            var searchUrl = $"/JAML/?search={Uri.EscapeDataString(searchId)}";

            return new McpResponse
            {
                Success = true,
                SearchId = searchId,
                JamlFilter = jamlFilter,
                Reasoning = reasoning,
                Results = results,
                Columns = columns,
                Message =
                    $"Generated JAML filter for: {prompt}. Search started with ID: {searchId}",
                SearchUrl = searchUrl,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing MCP prompt: {prompt}");

            // Only log unexpected errors (validation errors are already handled above)
            // Don't use exception message parsing - that's an anti-pattern
            return new McpResponse { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    public (string final, RefinementSteps steps) RefinePromptWithSteps(string rawPrompt)
    {
        var steps = new RefinementSteps
        {
            Original = rawPrompt,
            AfterStep1 = string.Empty,
            AfterStep2 = string.Empty,
            AfterStep3 = string.Empty,
            Final = string.Empty,
        };

        var prompt = rawPrompt;

        // STEP 1: Typo Detection & Fixing
        prompt = RefineStep1_TypoFix(prompt);
        steps.AfterStep1 = prompt;
        _logger.LogDebug("After Step 1 (Typo Fix): {Prompt}", prompt);

        // STEP 2: Trim Salutations & Frustrations
        prompt = RefineStep2_TrimFluff(prompt);
        steps.AfterStep2 = prompt;
        _logger.LogDebug("After Step 2 (Trim Fluff): {Prompt}", prompt);

        // STEP 3: Sensibility Check - Remove incomplete thoughts
        prompt = RefineStep3_SensibilityCheck(prompt);
        steps.AfterStep3 = prompt;
        _logger.LogDebug("After Step 3 (Sensibility): {Prompt}", prompt);

        steps.Final = prompt;

        // STEP 4: Ensure completeness (add defaults if needed)
        // NOTE: Step 4 happens AFTER AI generates the config, in RefineStep4_EnsureCompleteness()

        return (prompt, steps);
    }

    private string RefinePrompt(string rawPrompt)
    {
        var (final, _) = RefinePromptWithSteps(rawPrompt);
        return final;
    }

    public string RefineStep1_TypoFix(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return prompt;

        var processed = prompt;

        // Fix "Auntie One" → "Ante 1"
        processed = System.Text.RegularExpressions.Regex.Replace(
            processed,
            @"\bAuntie\s+One\b",
            "Ante 1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // Fix "Auntie [number]" → "Ante [number]" (but preserve "Antimatter")
        processed = System.Text.RegularExpressions.Regex.Replace(
            processed,
            @"\bAuntie\s+(\d+)\b",
            "Ante $1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // Fix standalone "Auntie" → "Ante" (but not if followed by "matter")
        processed = System.Text.RegularExpressions.Regex.Replace(
            processed,
            @"\bAuntie\b(?!\s*matter)",
            "Ante",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // Fix "Anti-[number]" → "Ante [number]" (but preserve "Antimatter" and "anti-one" for exclusions)
        processed = System.Text.RegularExpressions.Regex.Replace(
            processed,
            @"\bAnti-\s*(\d+)\b",
            "Ante $1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
        processed = System.Text.RegularExpressions.Regex.Replace(
            processed,
            @"\bAnti\s+(\d+)\b",
            "Ante $1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // "anti-one" or "anti one" for exclusions - keep as is (handled by system prompt)
        // But "Anti-" followed by number without "one" → "Ante"
        processed = System.Text.RegularExpressions.Regex.Replace(
            processed,
            @"\bAnti-\b(?!\s*one|\s*matter)",
            "Ante",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // Slang translations
        var slangMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "blurry face joker", "SmearedJoker" },
            { "blurry face", "SmearedJoker" },
            { "face chad", "HangingChad Photograph" }, // Both jokers
            { "dice", "OopsAll6s" },
            { "dice joker", "OopsAll6s" },
            { "wee", "WeeJoker" },
            { "bus", "RideTheBus" },
            { "blueprint", "Blueprint" },
            { "brain", "Brainstorm" },
        };

        foreach (var slang in slangMap)
        {
            processed = System.Text.RegularExpressions.Regex.Replace(
                processed,
                $@"\b{System.Text.RegularExpressions.Regex.Escape(slang.Key)}\b",
                slang.Value,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
        }

        // Normalize card names
        var cardNameFixes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Lucky Cat", "LuckyCat" },
            { "Oops All Six", "OopsAll6s" },
            { "Oops All 6s", "OopsAll6s" },
            { "Oops All 6", "OopsAll6s" },
            { "Oopsall6s", "OopsAll6s" },
            { "Oopsall 6s", "OopsAll6s" },
            { "Score by Chad", "ScoreByChad" },
        };

        foreach (var fix in cardNameFixes)
        {
            processed = System.Text.RegularExpressions.Regex.Replace(
                processed,
                $@"\b{System.Text.RegularExpressions.Regex.Escape(fix.Key)}\b",
                fix.Value,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
        }

        return processed;
    }

    public string RefineStep2_TrimFluff(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return prompt;

        var trimmed = prompt.Trim();

        // Remove common frustration patterns at the end
        var frustrationPatterns = new[]
        {
            @"\s+and\s+ummm?\s+wait\s+shit.*$",
            @"\s+and\s+ummm?.*$",
            @"\s+wait\s+shit.*$",
            @"\s+fuck.*$",
            @"\s+oh\s+well.*$",
            @"\s+\.\.\..*$", // trailing ellipsis
            @"\s+and\s+\.\.\..*$", // "and ..."
        };

        foreach (var pattern in frustrationPatterns)
        {
            trimmed = System.Text.RegularExpressions.Regex.Replace(
                trimmed,
                pattern,
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
        }

        return trimmed.Trim();
    }

    public string RefineStep3_SensibilityCheck(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return prompt;

        // Only remove obvious incomplete thoughts (trailing fragments)
        // Don't split and filter - that's too aggressive and removes valid prompts

        // Remove trailing incomplete fragments like "and umm", "wait shit", etc.
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            prompt,
            @"\s+(and\s+)?(um+|uh+|er+|ah+)\s*$",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"\s+(wait\s+)?(shit|fuck|damn|darn)\s*$",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // Remove trailing ellipsis or incomplete sentences
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+\.\.\.\s*$", "");

        return cleaned.Trim();
    }

    private async Task<string> RefinePromptWithLLMAsync(string rawPrompt)
    {
        if (string.IsNullOrWhiteSpace(rawPrompt))
            return rawPrompt;

        if (string.IsNullOrEmpty(_workerUrl))
        {
            _logger.LogWarning("Worker URL not configured, skipping LLM refinement");
            return rawPrompt;
        }

        try
        {
            // Use LLM to extract core intent - simple refinement prompt
            var refinementPrompt =
                $@"Extract the core search intent from this user prompt. Remove filler words, fix obvious typos, but keep ALL meaningful search terms and requirements.

User prompt: ""{rawPrompt}""

Return ONLY the cleaned, refined prompt with no explanations or markdown:";

            var response = await _httpClient.PostAsJsonAsync(
                _workerUrl,
                new { prompt = refinementPrompt }
            );

            if (response.IsSuccessStatusCode)
            {
                var rawContent = await response.Content.ReadAsStringAsync();

                // Worker might return JSON or plain text
                try
                {
                    using var stream = new System.IO.MemoryStream(
                        System.Text.Encoding.UTF8.GetBytes(rawContent)
                    );
                    var json = await System.Text.Json.JsonSerializer.DeserializeAsync<JsonElement>(
                        stream
                    );

                    if (
                        json.TryGetProperty("jaml", out var jaml)
                        && jaml.ValueKind == JsonValueKind.String
                    )
                    {
                        var refined = jaml.GetString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(refined))
                        {
                            _logger.LogDebug($"LLM refined prompt: '{rawPrompt}' → '{refined}'");
                            return refined;
                        }
                    }
                }
                catch
                {
                    // Not JSON, treat as plain text
                    var refined = rawContent.Trim();
                    if (!string.IsNullOrWhiteSpace(refined))
                    {
                        _logger.LogDebug(
                            $"LLM refined prompt (plain text): '{rawPrompt}' → '{refined}'"
                        );
                        return refined;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM refinement failed, using original prompt");
        }

        // Fallback to original if refinement fails
        return rawPrompt;
    }

    private async Task<string> GenerateJamlWithAIAsync(
        string userPrompt,
        string? originalPrompt = null
    )
    {
        if (string.IsNullOrEmpty(_workerUrl))
        {
            throw new InvalidOperationException(
                "Cloudflare Worker URL not configured. Please set WorkerUrl in appsettings.json"
            );
        }

        // Ensure prompt is not empty
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            throw new ArgumentException("Prompt cannot be empty", nameof(userPrompt));
        }

        // Build request - Worker should have system prompt hardcoded (security best practice)
        // Only send user prompt - Worker will add its own system prompt
        var requestBody = new { prompt = userPrompt };

        _logger.LogInformation($"Calling Cloudflare Worker: {_workerUrl}");
        _logger.LogDebug($"Request body: {System.Text.Json.JsonSerializer.Serialize(requestBody)}");
        _logger.LogInformation(
            $"NOTE: Worker should have system prompt hardcoded. Current system prompt length: {GetSystemPrompt().Length} characters"
        );

        try
        {
            var response = await _httpClient.PostAsJsonAsync(_workerUrl, requestBody);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Worker error: {response.StatusCode} - {errorContent}");
                throw new HttpRequestException(
                    $"Worker error: {response.StatusCode} - {errorContent}"
                );
            }

            // Read raw response content before parsing
            var rawResponseContent = await response.Content.ReadAsStringAsync();

            // Worker returns: { jaml: "..." } or just the JAML string
            JsonElement workerResponse;
            string generatedJaml;

            try
            {
                // Try to parse as JSON - need to recreate stream since we already read it
                using var stream = new System.IO.MemoryStream(
                    System.Text.Encoding.UTF8.GetBytes(rawResponseContent)
                );
                workerResponse =
                    await System.Text.Json.JsonSerializer.DeserializeAsync<JsonElement>(stream);

                if (workerResponse.ValueKind == JsonValueKind.Object)
                {
                    // Check for error response first
                    if (
                        workerResponse.TryGetProperty("success", out var successProp)
                        && successProp.ValueKind == JsonValueKind.False
                    )
                    {
                        var errorMsg = "Unknown error";
                        if (workerResponse.TryGetProperty("error", out var errorProp))
                        {
                            errorMsg = errorProp.GetString() ?? errorMsg;
                        }
                        throw new HttpRequestException(
                            $"Worker error: InternalServerError - {rawResponseContent}"
                        );
                    }

                    // Check for jaml property
                    if (workerResponse.TryGetProperty("jaml", out var jamlProperty))
                    {
                        generatedJaml = jamlProperty.GetString() ?? "";
                    }
                    // Check for config property (JSON format) - convert to JAML
                    else if (workerResponse.TryGetProperty("config", out var configProperty))
                    {
                        string configJson = configProperty.GetRawText();
                        try
                        {
                            // Deserialize the config JSON
                            var config = ConfigFormatConverter.LoadFromJsonString(configJson);

                            if (config != null)
                            {
                                // Set author and description from prompt
                                config.Author = "JamlGenie";
                                if (string.IsNullOrWhiteSpace(config.Description))
                                {
                                    config.Description = originalPrompt ?? userPrompt;
                                }

                                // STEP 4: Ensure completeness - add defaults if needed
                                RefineStep4_EnsureCompleteness(
                                    config,
                                    originalPrompt ?? userPrompt
                                );

                                // Fix invalid edition values and normalize card names
                                FixInvalidEditions(config);
                                NormalizeCardNames(config);

                                // Convert config to JAML
                                generatedJaml = config.SaveAsJaml();
                            }
                            else
                            {
                                throw new InvalidOperationException(
                                    "Failed to deserialize config from worker response"
                                );
                            }
                        }
                        catch (Exception configEx)
                        {
                            // Save invalid config to failures bucket
                            _feedbackService?.LogFailure(
                                prompt: originalPrompt ?? userPrompt,
                                generatedJaml: $"<JSON Config>\n{configJson}",
                                aiReasoning: "AI returned JSON config that failed to deserialize/convert",
                                error: configEx.Message,
                                context: new
                                {
                                    configJson = configJson.Length > 1000
                                        ? configJson.Substring(0, 1000) + "..."
                                        : configJson,
                                }
                            );

                            // Include the raw config JSON in error for debugging
                            var errorPreview =
                                configJson.Length > 500
                                    ? configJson.Substring(0, 500) + "..."
                                    : configJson;
                            throw new InvalidOperationException(
                                $"Failed to convert worker config to JAML: {configEx.Message}\n\nWorker returned config JSON:\n{errorPreview}",
                                configEx
                            );
                        }
                    }
                    else
                    {
                        // Object but no jaml or config property - try as plain text
                        generatedJaml = rawResponseContent;
                    }
                }
                else if (workerResponse.ValueKind == JsonValueKind.String)
                {
                    generatedJaml = workerResponse.GetString() ?? "";
                }
                else
                {
                    // Try as plain text
                    generatedJaml = rawResponseContent;
                }
            }
            catch (JsonException)
            {
                // Response is not valid JSON, treat as plain text JAML
                generatedJaml = rawResponseContent;
            }

            generatedJaml = generatedJaml.Trim();

            if (string.IsNullOrWhiteSpace(generatedJaml))
            {
                throw new InvalidOperationException("AI returned empty response");
            }

            // Track raw response for debugging
            var rawResponse = generatedJaml;
            _logger.LogDebug($"Raw AI response:\n{rawResponse}");

            // Clean up markdown code blocks if AI adds them
            var cleaned = CleanMarkdown(generatedJaml);
            _logger.LogDebug($"After CleanMarkdown:\n{cleaned}");

            // Validate and ensure proper JAML structure
            generatedJaml = EnsureJamlHeader(cleaned, originalPrompt);
            _logger.LogDebug($"After EnsureJamlHeader:\n{generatedJaml}");

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
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // Remove markdown code blocks (```yaml ... ``` or ``` ... ```)
        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"^```(?:yaml|yml|jaml)?\s*\n?",
            "",
            System.Text.RegularExpressions.RegexOptions.Multiline
        );
        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"\n?```\s*$",
            "",
            System.Text.RegularExpressions.RegexOptions.Multiline
        );

        // Remove JSON wrapper if AI returned {"success":true,"jaml":"..."}
        if (text.TrimStart().StartsWith("{") && text.Contains("\"jaml\""))
        {
            try
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(text);
                if (
                    parsed.TryGetProperty("jaml", out var jamlProp)
                    && jamlProp.ValueKind == JsonValueKind.String
                )
                {
                    text = jamlProp.GetString() ?? text;
                }
            }
            catch
            {
                // Not valid JSON, continue with original
            }
        }

        // Find YAML content (starts with "name:" or "deck:" or "must:")
        var yamlMatch = System.Text.RegularExpressions.Regex.Match(
            text,
            @"(?:^|\n)(name:|deck:|must:|should:|mustNot:)",
            System.Text.RegularExpressions.RegexOptions.Multiline
        );
        if (yamlMatch.Success && yamlMatch.Index > 0)
        {
            text = text.Substring(yamlMatch.Index);
        }

        // Remove trailing explanation text (anything after blank line + non-YAML)
        var lines = text.Split('\n');
        int yamlEnd = lines.Length;
        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]) && i > 0)
            {
                // Check if next line looks like YAML
                if (i + 1 < lines.Length)
                {
                    var nextLine = lines[i + 1];
                    if (
                        !string.IsNullOrWhiteSpace(nextLine)
                        && !System.Text.RegularExpressions.Regex.IsMatch(
                            nextLine,
                            @"^[\w-]+:|^[\s]*-[\s]*(joker|voucher|tarot|planet|spectral|soulJoker|tag|boss|playingCard|event|and|or):",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase
                        )
                    )
                    {
                        yamlEnd = i;
                        break;
                    }
                }
            }
        }
        text = string.Join("\n", lines.Take(yamlEnd)).Trim();

        return text;
    }

    public string GetSystemPrompt()
    {
        var itemCatalog = GetCompleteItemCatalog();
        var jokerMapping = GetJokerNameMapping();

        // Inject failure context to learn from past mistakes
        var failureContext = _feedbackService?.GetFailureContextForPrompt(5) ?? "";

        return $@"You are a JAML (Joker Artifact Markup Language) filter generator for Balatro seed searching.

CRITICAL RULES:
1. Output ONLY valid JAML (YAML format) - no markdown code blocks, no explanations, no comments. Return {{ success: true, jaml: ""..."" }} where the jaml value is the complete JAML filter as a YAML string.
2. Handle typos: ""anti-one""/""anti one""/""anti-1"" = exclude (use mustNot:)
3. Valid editions ONLY: None, Foil, Holographic, Polychrome, Negative
4. Card names: Use EXACT enum names (see COMPLETE ITEM CATALOG below)
5. Score must be integer, not string
6. VALID TYPES (case-sensitive, use EXACTLY these strings):
   - ""Joker"" - Regular joker cards (Blueprint, Brainstorm, HangingChad, LuckyCat, etc.)
   - ""SoulJoker"" - Soul jokers (Perkeo, Triboulet, Canio, Chicot, etc.)
   - ""Voucher"" - Shop vouchers (Telescope, Observatory, Hieroglyph, Overstock, etc.)
   - ""TarotCard"" - Tarot cards (TheFool, TheMagician, Temperance, TheHermit, etc.)
   - ""PlanetCard"" - Planet cards (Jupiter, Mars, Venus, Mercury, Earth, etc.)
   - ""SpectralCard"" - Spectral cards (Ankh, Soul, Wraith, Familiar, Grim, etc.)
   - ""Tag"" - Matches EITHER small blind tag OR big blind tag (NegativeTag, StandardTag, BossTag, etc.)
   - ""SmallBlindTag"" - Small blind tags only (NegativeTag, StandardTag, MeteorTag, etc.)
   - ""BigBlindTag"" - Big blind tags only (BossTag, etc.)
   - ""Boss"" - Boss blinds (TheGoad, CeruleanBell, TheOx, etc.)
   - ""PlayingCard"" - Playing cards (use suit/rank properties, not value)
   - ""Event"" - Random events (Lucky, WheelOfFortune, Bananas, Misprint)
   - ""ErraticRank"" - Erratic Deck starting composition - rank filter (only for Erratic deck)
   - ""ErraticSuit"" - Erratic Deck starting composition - suit filter (only for Erratic deck)
   - ""And"" - Logical AND - all nested clauses must match (use clauses: array)
   - ""Or"" - Logical OR - at least one nested clause must match (use clauses: array)

ITEM TYPE CLASSIFICATION (CRITICAL - Classify items correctly):
- JOKERS: All regular joker cards (Blueprint, HangingChad, LuckyCat, Photograph, etc.) → type: ""Joker""
- SOUL JOKERS: Soul jokers (Perkeo, Triboulet, Canio, Chicot) → type: ""SoulJoker""
- VOUCHERS: Shop vouchers (Overstock, Telescope, Observatory, Hieroglyph, etc.) → type: ""Voucher""
- TAROT: Tarot cards (TheFool, TheMagician, Temperance, TheHermit, etc.) → type: ""TarotCard""
- PLANET: Planet cards (Mercury, Venus, Earth, Jupiter, Mars, etc.) → type: ""PlanetCard""
- SPECTRAL: Spectral cards (Familiar, Grim, Soul, Wraith, Ankh, etc.) → type: ""SpectralCard""
- BOSS: Boss blinds (TheGoad, CeruleanBell, TheOx, etc.) → type: ""Boss""
- TAGS: Use ""Tag"" to match either small blind OR big blind tag (NegativeTag, StandardTag, BossTag, etc.). Use ""SmallBlindTag"" for small blind only, ""BigBlindTag"" for big blind only.

FUZZY MATCHING: If user says ""hanging chad"", ""hangingchad"", ""hangingChad"", etc., find the closest match:
- ""hanging chad"" → HangingChad (JOKER, not voucher!)
- ""hangingchad"" → HangingChad (JOKER)
- ""face chad"" → HangingChad (JOKER) + Photograph (JOKER)
- Use case-insensitive matching and ignore spaces/hyphens

IMPOSSIBLE CONFIGS (NEVER generate these - they will never return seeds):
- ❌ Non-joker items in Ante 1 pack slot 0 (first pack is always 2-joker Buffoon pack, costs $4)
- ❌ Skip tags (NegativeTag, StandardTag, etc.) in Ante 3 (Ante 3 is always Boss Blind with BossTag only)
- ❌ These tags in Ante 1: NegativeTag, StandardTag, MeteorTag, BuffoonTag, HandyTag, GarbageTag, EtherealTag, TopupTag, OrbitalTag
- ❌ LuckyCat in Ante 1 WITHOUT Lucky enhancement first (LuckyCat is locked until player gets Lucky enhancement card)
- ✅ Valid: Jokers in Ante 1 pack slot 0, Skip tags in Antes 2/4-8, EtherealTag in Ante 2+
- ✅ Valid: LuckyCat in Ante 1 IF Lucky enhancement standard card also in Ante 1 (unlocks LuckyCat)

COMPLETE ITEM CATALOG (Use this to find items and their types):
{itemCatalog}

JOKER NAME MAPPING (Game Display Name → Config Enum Name):
{jokerMapping}

SLANG TRANSLATIONS:
- ""blurry face joker"" → SmearedJoker (JOKER)
- ""face chad"" → HangingChad (JOKER) + Photograph (JOKER) - BOTH are JOKERS, not vouchers!
- ""hanging chad"" → HangingChad (JOKER) - This is a JOKER, NOT a voucher!
- ""econ""/""economy"" → Look for money sources (see ECONOMY HANDLING below)
- ""dice"" → OopsAll6s (JOKER)
- ""wee"" → WeeJoker (JOKER)
- ""bus"" → RideTheBus (JOKER)
- ""blueprint"" → Blueprint (JOKER)
- ""brain"" → Brainstorm (JOKER)

ECONOMY HANDLING:
If user requests ""econ""/""economy"", add money sources to should: array:
- Tarot cards: Temperance (sell value of Jokers, max $50), The Fool (creates last Tarot/Planet), The Hermit (doubles money, max $20)
- Standard cards with Gold Seal (+$3 when scored)
- Economy jokers: GoldenTicket, BusinessCard, CouponBook, Rocket
- Focus on early antes (1-3) for these items

JAML FORMAT (YAML-based):
Use clean type-as-key syntax when possible:
- ""joker: Blueprint"" instead of ""type: Joker, value: Blueprint""
- ""voucher: Telescope"" instead of ""type: Voucher, value: Telescope""
- ""soulJoker: Perkeo"" instead of ""type: SoulJoker, value: Perkeo""

JAML STRUCTURE:
```yaml
name: Filter Name
description: Optional description
author: AI Generated
deck: Red
stake: White
must:
  - joker: Blueprint
    antes: [1, 2, 3]
should:
  - joker: LuckyCat
    score: 1
mustNot:
  - joker: Showman
```

EXAMPLES:
Input: ""One Blueprint and anti-one Showman""
Output: {{""success"":true,""jaml"":""name: Blueprint No Showman\ndeck: Red\nstake: White\nmust:\n  - joker: Blueprint\n    antes: [1, 2, 3, 4]\nmustNot:\n  - joker: Showman\nshould: []\n""}}

Input: ""Faceless Joker with Negative edition""
Output: {{""success"":true,""jaml"":""name: Faceless Joker Negative\ndeck: Red\nstake: White\nmust:\n  - joker: FacelessJoker\n    edition: Negative\nshould: []\nmustNot: []\n""}}

Input: ""hanging chad""
Output: {{""success"":true,""jaml"":""name: Hanging Chad\ndeck: Red\nstake: White\nmust:\n  - joker: HangingChad\n    antes: [1, 2, 3, 4]\nshould: []\nmustNot: []\n""}}
NOTE: HangingChad is a JOKER (Common), NOT a voucher! Always check the catalog above.

Input: ""Telescope voucher""
Output: {{""success"":true,""jaml"":""name: Telescope\ndeck: Red\nstake: White\nmust:\n  - voucher: Telescope\n    antes: [1, 2, 3, 4]\nshould: []\nmustNot: []\n""}}
NOTE: Telescope is a VOUCHER, not a joker. Check catalog to confirm type.

OUTPUT FORMAT:
Return JSON with success: true and jaml: ""<YAML string>"". The jaml value should be a complete, valid JAML filter as a YAML-formatted string. Use newlines (\n) to separate YAML lines. Do NOT use markdown code blocks - just the raw YAML string.

{failureContext}";
    }

    private string GetCompleteItemCatalog()
    {
        try
        {
            var catalogPath = Path.Combine("Knowledge", "item-catalog.json");
            if (!File.Exists(catalogPath))
            {
                _logger.LogWarning("item-catalog.json not found, using hardcoded catalog");
                return GetHardcodedItemCatalog();
            }

            var catalogJson = File.ReadAllText(catalogPath);
            var catalog = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(catalogJson);

            var catalogText = new System.Text.StringBuilder();
            catalogText.AppendLine("=== COMPLETE ITEM CATALOG ===");
            catalogText.AppendLine();

            // JOKERS (type: "Joker")
            catalogText.AppendLine("JOKERS (type: \"Joker\"):");
            if (catalog.TryGetProperty("jokers", out var jokers))
            {
                if (jokers.TryGetProperty("common", out var common))
                {
                    var commonList = common
                        .EnumerateArray()
                        .Select(j => j.GetString())
                        .Where(s => !string.IsNullOrEmpty(s));
                    catalogText.AppendLine(
                        $"  Common ({commonList.Count()}): {string.Join(", ", commonList)}"
                    );
                }
                if (jokers.TryGetProperty("uncommon", out var uncommon))
                {
                    var uncommonList = uncommon
                        .EnumerateArray()
                        .Select(j => j.GetString())
                        .Where(s => !string.IsNullOrEmpty(s));
                    catalogText.AppendLine(
                        $"  Uncommon ({uncommonList.Count()}): {string.Join(", ", uncommonList)}"
                    );
                }
                if (jokers.TryGetProperty("rare", out var rare))
                {
                    var rareList = rare.EnumerateArray()
                        .Select(j => j.GetString())
                        .Where(s => !string.IsNullOrEmpty(s));
                    catalogText.AppendLine(
                        $"  Rare ({rareList.Count()}): {string.Join(", ", rareList)}"
                    );
                }
                if (jokers.TryGetProperty("legendary", out var legendary))
                {
                    var legendaryList = legendary
                        .EnumerateArray()
                        .Select(j => j.GetString())
                        .Where(s => !string.IsNullOrEmpty(s));
                    catalogText.AppendLine(
                        $"  Legendary ({legendaryList.Count()}): {string.Join(", ", legendaryList)}"
                    );
                }
            }
            catalogText.AppendLine();

            // VOUCHERS (type: "Voucher")
            catalogText.AppendLine("VOUCHERS (type: \"Voucher\"):");
            if (catalog.TryGetProperty("vouchers", out var vouchers))
            {
                var voucherList = vouchers
                    .EnumerateArray()
                    .Select(v => v.GetString())
                    .Where(s => !string.IsNullOrEmpty(s));
                catalogText.AppendLine($"  {string.Join(", ", voucherList)}");
            }
            catalogText.AppendLine();

            // TAROT CARDS (type: "Tarot")
            catalogText.AppendLine("TAROT CARDS (type: \"Tarot\"):");
            if (catalog.TryGetProperty("tarotCards", out var tarotCards))
            {
                var tarotList = tarotCards
                    .EnumerateArray()
                    .Select(t => t.GetString())
                    .Where(s => !string.IsNullOrEmpty(s));
                catalogText.AppendLine($"  {string.Join(", ", tarotList)}");
            }
            catalogText.AppendLine();

            // PLANET CARDS (type: "Planet")
            catalogText.AppendLine("PLANET CARDS (type: \"Planet\"):");
            if (catalog.TryGetProperty("planetCards", out var planetCards))
            {
                var planetList = planetCards
                    .EnumerateArray()
                    .Select(p => p.GetString())
                    .Where(s => !string.IsNullOrEmpty(s));
                catalogText.AppendLine($"  {string.Join(", ", planetList)}");
            }
            catalogText.AppendLine();

            // SPECTRAL CARDS (type: "Spectral")
            catalogText.AppendLine("SPECTRAL CARDS (type: \"Spectral\"):");
            if (catalog.TryGetProperty("spectralCards", out var spectralCards))
            {
                var spectralList = spectralCards
                    .EnumerateArray()
                    .Select(s => s.GetString())
                    .Where(s => !string.IsNullOrEmpty(s));
                catalogText.AppendLine($"  {string.Join(", ", spectralList)}");
            }
            catalogText.AppendLine();

            // CRITICAL REMINDERS
            catalogText.AppendLine("CRITICAL REMINDERS:");
            catalogText.AppendLine("  - HangingChad is a JOKER (Common), NOT a voucher!");
            catalogText.AppendLine(
                "  - When user says \"hanging chad\" or \"hangingchad\", use type: \"Joker\", value: \"HangingChad\""
            );
            catalogText.AppendLine(
                "  - Always check the catalog above to determine the correct type!"
            );
            catalogText.AppendLine(
                "  - Use fuzzy matching: ignore spaces, hyphens, case differences"
            );

            return catalogText.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load item catalog, using hardcoded fallback");
            return GetHardcodedItemCatalog();
        }
    }

    private string GetHardcodedItemCatalog()
    {
        return @"=== COMPLETE ITEM CATALOG ===

JOKERS (type: ""Joker""):
  Common: Joker, GreedyJoker, LustyJoker, WrathfulJoker, GluttonousJoker, JollyJoker, ZanyJoker, MadJoker, CrazyJoker, DrollJoker, SlyJoker, WilyJoker, CleverJoker, DeviousJoker, CraftyJoker, HalfJoker, CreditCard, Banner, MysticSummit, EightBall, Misprint, RaisedFist, ChaostheClown, ScaryFace, AbstractJoker, DelayedGratification, GrosMichel, EvenSteven, OddTodd, Scholar, BusinessCard, Supernova, RideTheBus, Egg, Runner, IceCream, Splash, BlueJoker, FacelessJoker, GreenJoker, Superposition, ToDoList, Cavendish, RedCard, SquareJoker, RiffRaff, Photograph, ReservedParking, MailInRebate, Hallucination, FortuneTeller, Juggler, Drunkard, GoldenJoker, Popcorn, WalkieTalkie, SmileyFace, GoldenTicket, Swashbuckler, HangingChad, ShootTheMoon
  Uncommon: JokerStencil, FourFingers, Mime, CeremonialDagger, MarbleJoker, LoyaltyCard, Dusk, Fibonacci, SteelJoker, Hack, Pareidolia, SpaceJoker, Burglar, Blackboard, SixthSense, Constellation, Hiker, CardSharp, Madness, Seance, Vampire, Shortcut, Hologram, Cloud9, Rocket, MidasMask, Luchador, GiftCard, TurtleBean, Erosion, ToTheMoon, StoneJoker, LuckyCat, Bull, DietCola, TradingCard, FlashCard, SpareTrousers, Ramen, Seltzer, Castle, MrBones, Acrobat, SockAndBuskin, Troubadour, Certificate, SmearedJoker, Throwback, RoughGem, Bloodstone, Arrowhead, OnyxAgate, GlassJoker, Showman, FlowerPot, MerryAndy, OopsAll6s, TheIdol, SeeingDouble, Matador, Satellite, Cartomancer, Astronomer, Bootstraps
  Rare: DNA, Vagabond, Baron, Obelisk, BaseballCard, AncientJoker, Campfire, Blueprint, WeeJoker, HitTheRoad, TheDuo, TheTrio, TheFamily, TheOrder, TheTribe, Stuntman, InvisibleJoker, Brainstorm, DriversLicense, BurntJoker
  Legendary: Canio, Triboulet, Yorick, Chicot, Perkeo

VOUCHERS (type: ""Voucher""):
  Overstock, OverstockPlus, ClearanceSale, Liquidation, Hone, GlowUp, RerollSurplus, RerollGlut, CrystalBall, OmenGlobe, Telescope, Observatory, Grabber, NachoTong, Wasteful, Recyclomancy, TarotMerchant, TarotTycoon, PlanetMerchant, PlanetTycoon, SeedMoney, MoneyTree, Blank, Antimatter, MagicTrick, Illusion, Hieroglyph, Petroglyph, DirectorsCut, Retcon, PaintBrush, Palette

TAROT CARDS (type: ""Tarot""):
  TheFool, TheMagician, TheHighPriestess, TheEmpress, TheEmperor, TheHierophant, TheLovers, TheChariot, Justice, TheHermit, TheWheelOfFortune, Strength, TheHangedMan, Death, Temperance, TheDevil, TheTower, TheStar, TheMoon, TheSun, Judgement, TheWorld

PLANET CARDS (type: ""Planet""):
  Mercury, Venus, Earth, Mars, Jupiter, Saturn, Uranus, Neptune, Pluto, PlanetX, Ceres, Eris

SPECTRAL CARDS (type: ""Spectral""):
  Familiar, Grim, Incantation, Talisman, Aura, Wraith, Sigil, Ouija, Ectoplasm, Immolate, Ankh, DejaVu, Hex, Trance, Medium, Cryptid, Soul, BlackHole

CRITICAL REMINDERS:
  - HangingChad is a JOKER (Common), NOT a voucher!
  - When user says ""hanging chad"" or ""hangingchad"", use type: ""Joker"", value: ""HangingChad""
  - Always check the catalog above to determine the correct type!
  - Use fuzzy matching: ignore spaces, hyphens, case differences";
    }

    private string GetJokerNameMapping()
    {
        var mapping = new List<string>();

        // Common jokers
        var commonJokers = new[]
        {
            "Joker",
            "GreedyJoker",
            "LustyJoker",
            "WrathfulJoker",
            "GluttonousJoker",
            "JollyJoker",
            "ZanyJoker",
            "MadJoker",
            "CrazyJoker",
            "DrollJoker",
            "SlyJoker",
            "WilyJoker",
            "CleverJoker",
            "DeviousJoker",
            "CraftyJoker",
            "HalfJoker",
            "CreditCard",
            "Banner",
            "MysticSummit",
            "EightBall",
            "Misprint",
            "RaisedFist",
            "ChaostheClown",
            "ScaryFace",
            "AbstractJoker",
            "DelayedGratification",
            "GrosMichel",
            "EvenSteven",
            "OddTodd",
            "Scholar",
            "BusinessCard",
            "Supernova",
            "RideTheBus",
            "Egg",
            "Runner",
            "IceCream",
            "Splash",
            "BlueJoker",
            "FacelessJoker",
            "GreenJoker",
            "Superposition",
            "ToDoList",
            "Cavendish",
            "RedCard",
            "SquareJoker",
            "RiffRaff",
            "Photograph",
            "ReservedParking",
            "MailInRebate",
            "Hallucination",
            "FortuneTeller",
            "Juggler",
            "Drunkard",
            "GoldenJoker",
            "Popcorn",
            "WalkieTalkie",
            "SmileyFace",
            "GoldenTicket",
            "Swashbuckler",
            "HangingChad",
            "ShootTheMoon",
        };

        // Uncommon jokers
        var uncommonJokers = new[]
        {
            "JokerStencil",
            "FourFingers",
            "Mime",
            "CeremonialDagger",
            "MarbleJoker",
            "LoyaltyCard",
            "Dusk",
            "Fibonacci",
            "SteelJoker",
            "Hack",
            "Pareidolia",
            "SpaceJoker",
            "Burglar",
            "Blackboard",
            "SixthSense",
            "Constellation",
            "Hiker",
            "CardSharp",
            "Madness",
            "Seance",
            "Vampire",
            "Shortcut",
            "Hologram",
            "Cloud9",
            "Rocket",
            "MidasMask",
            "Luchador",
            "GiftCard",
            "TurtleBean",
            "Erosion",
            "ToTheMoon",
            "StoneJoker",
            "LuckyCat",
            "Bull",
            "DietCola",
            "TradingCard",
            "FlashCard",
            "SpareTrousers",
            "Ramen",
            "Seltzer",
            "Castle",
            "MrBones",
            "Acrobat",
            "SockAndBuskin",
            "Troubadour",
            "Certificate",
            "SmearedJoker",
            "Throwback",
            "RoughGem",
            "Bloodstone",
            "Arrowhead",
            "OnyxAgate",
            "GlassJoker",
            "Showman",
            "FlowerPot",
            "MerryAndy",
            "OopsAll6s",
            "TheIdol",
            "SeeingDouble",
            "Matador",
            "Satellite",
            "Cartomancer",
            "Astronomer",
            "Bootstraps",
        };

        // Rare jokers
        var rareJokers = new[]
        {
            "DNA",
            "Vagabond",
            "Baron",
            "Obelisk",
            "BaseballCard",
            "AncientJoker",
            "Campfire",
            "Blueprint",
            "WeeJoker",
            "HitTheRoad",
            "TheDuo",
            "TheTrio",
            "TheFamily",
            "TheOrder",
            "TheTribe",
            "Stuntman",
            "InvisibleJoker",
            "Brainstorm",
            "DriversLicense",
            "BurntJoker",
        };

        // Legendary jokers
        var legendaryJokers = new[] { "Canio", "Triboulet", "Yorick", "Chicot", "Perkeo" };

        // Helper to convert PascalCase to "Display Name"
        string PascalToDisplay(string pascal)
        {
            // Insert space before capital letters (except first)
            var result = System.Text.RegularExpressions.Regex.Replace(
                pascal,
                @"([a-z])([A-Z])",
                "$1 $2"
            );
            return result;
        }

        foreach (
            var joker in commonJokers
                .Concat(uncommonJokers)
                .Concat(rareJokers)
                .Concat(legendaryJokers)
        )
        {
            var displayName = PascalToDisplay(joker);
            mapping.Add($"  \"{displayName}\" → {joker}");
        }

        return string.Join("\n", mapping);
    }

    private string EnsureJamlHeader(string jaml, string? userPrompt = null)
    {
        // If header is missing, add it
        if (!jaml.Contains("dateCreated:"))
        {
            // Generate smart name from prompt or use default
            var filterName = GenerateFilterNameFromPrompt(userPrompt);

            var header =
                $@"dateCreated: {DateTime.UtcNow:yyyy-MM-dd}
name: {filterName}
author: JamlGenie

";
            jaml = header + jaml;
        }
        else if (!jaml.Contains("name:") || jaml.Contains("name: AI Generated Filter"))
        {
            // Update existing header if it has the bad default name
            var filterName = GenerateFilterNameFromPrompt(userPrompt);
            jaml = System.Text.RegularExpressions.Regex.Replace(
                jaml,
                @"name:\s*AI Generated Filter",
                $"name: {filterName}",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
        }
        return jaml;
    }

    private string GenerateFilterNameFromPrompt(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return "JamlGenie Filter";

        // Clean up the prompt
        var clean = prompt.Trim();

        // Remove common prefixes/suffixes
        clean = System.Text.RegularExpressions.Regex.Replace(
            clean,
            @"^(find|search|get|show|give me|i want|i need)\s+",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
        clean = System.Text.RegularExpressions.Regex.Replace(
            clean,
            @"\s+(please|pls|thx|thanks)$",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // Capitalize first letter
        if (clean.Length > 0)
        {
            clean = char.ToUpperInvariant(clean[0]) + (clean.Length > 1 ? clean.Substring(1) : "");
        }

        // Limit length
        if (clean.Length > 60)
        {
            clean = clean.Substring(0, 57) + "...";
        }

        return string.IsNullOrWhiteSpace(clean) ? "JamlGenie Filter" : clean;
    }

    private void RefineStep4_EnsureCompleteness(MotelyJsonConfig config, string originalPrompt)
    {
        // Check if user requested economy
        bool wantsEconomy = System.Text.RegularExpressions.Regex.IsMatch(
            originalPrompt,
            @"\b(econ|economy)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        if (wantsEconomy)
        {
            _logger.LogDebug("User requested economy - adding money sources to should array");

            if (config.Should == null)
            {
                config.Should = new List<MotelyJsonConfig.MotelyJsonFilterClause>();
            }

            // Add economy jokers (early antes 1-3)
            var economyJokers = new[] { "GoldenTicket", "BusinessCard", "CouponBook", "Rocket" };
            foreach (var joker in economyJokers)
            {
                config.Should.Add(
                    new MotelyJsonConfig.MotelyJsonFilterClause
                    {
                        Type = "Joker",
                        Value = joker,
                        Score = 2,
                        Antes = new[] { 1, 2, 3 },
                        Label = $"{joker} (economy)",
                    }
                );
            }

            // Add money tarot cards (early antes 1-3)
            var moneyTarots = new[] { "Temperance", "TheFool", "TheHermit" };
            foreach (var tarot in moneyTarots)
            {
                config.Should.Add(
                    new MotelyJsonConfig.MotelyJsonFilterClause
                    {
                        Type = "Tarot",
                        Value = tarot,
                        Score = 2,
                        Antes = new[] { 1, 2, 3 },
                        Label = $"{tarot} (money)",
                    }
                );
            }

            // Add Gold Seal on standard cards (early antes 1-3)
            config.Should.Add(
                new MotelyJsonConfig.MotelyJsonFilterClause
                {
                    Type = "PlayingCard",
                    Seal = "Gold",
                    Score = 1,
                    Antes = new[] { 1, 2, 3 },
                    Label = "Gold Seal (money)",
                }
            );
        }

        // Ensure Should array exists and has at least one item for scoring columns
        if (config.Should == null || config.Should.Count == 0)
        {
            _logger.LogDebug(
                "Adding default Should clause (Egg joker) to ensure scoring columns exist"
            );

            if (config.Should == null)
            {
                config.Should = new List<MotelyJsonConfig.MotelyJsonFilterClause>();
            }

            // Add the "prank" default: Egg joker
            config.Should.Add(
                new MotelyJsonConfig.MotelyJsonFilterClause
                {
                    Type = "Joker",
                    Value = "Egg",
                    Score = 1,
                    Label = "Egg (default)",
                }
            );
        }

        // Ensure Must array exists (even if empty)
        if (config.Must == null)
        {
            config.Must = new List<MotelyJsonConfig.MotelyJsonFilterClause>();
        }

        // Ensure MustNot array exists (even if empty)
        if (config.MustNot == null)
        {
            config.MustNot = new List<MotelyJsonConfig.MotelyJsonFilterClause>();
        }
    }

    private void NormalizeCardNames(MotelyJsonConfig config)
    {
        var cardNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Lucky Cat", "LuckyCat" },
            { "LuckyCat", "LuckyCat" },
            { "Oops All Six", "OopsAll6s" },
            { "Oops All 6s", "OopsAll6s" },
            { "Oops All 6", "OopsAll6s" },
            { "Oopsall6s", "OopsAll6s" },
            { "Oopsall 6s", "OopsAll6s" },
            { "Score by Chad", "ScoreByChad" },
            { "ScoreByChad", "ScoreByChad" },
        };

        void NormalizeClause(MotelyJsonConfig.MotelyJsonFilterClause clause)
        {
            // Normalize Value
            if (
                !string.IsNullOrEmpty(clause.Value)
                && cardNameMap.TryGetValue(clause.Value, out var normalizedValue)
            )
            {
                clause.Value = normalizedValue;
            }

            // Normalize Values array
            if (clause.Values != null && clause.Values.Length > 0)
            {
                for (int i = 0; i < clause.Values.Length; i++)
                {
                    if (cardNameMap.TryGetValue(clause.Values[i], out var normalized))
                    {
                        clause.Values[i] = normalized;
                    }
                }
            }

            // Normalize nested clauses
            if (clause.Clauses != null)
            {
                foreach (var nestedClause in clause.Clauses)
                {
                    NormalizeClause(nestedClause);
                }
            }
        }

        if (config.Must != null)
        {
            foreach (var clause in config.Must)
            {
                NormalizeClause(clause);
            }
        }

        if (config.Should != null)
        {
            foreach (var clause in config.Should)
            {
                NormalizeClause(clause);
            }
        }

        if (config.MustNot != null)
        {
            foreach (var clause in config.MustNot)
            {
                NormalizeClause(clause);
            }
        }
    }

    private void FixInvalidEditions(MotelyJsonConfig config)
    {
        var validEditions = new[] { "None", "Foil", "Holographic", "Polychrome", "Negative" };

        void FixClause(MotelyJsonConfig.MotelyJsonFilterClause clause)
        {
            if (!string.IsNullOrEmpty(clause.Edition))
            {
                // Check if edition is valid
                if (!Enum.TryParse<MotelyItemEdition>(clause.Edition, true, out _))
                {
                    // Remove invalid edition
                    clause.Edition = null;
                }
            }

            // Fix nested clauses (And/Or)
            if (clause.Clauses != null)
            {
                foreach (var nestedClause in clause.Clauses)
                {
                    FixClause(nestedClause);
                }
            }
        }

        // Fix all clauses in must, should, mustNot
        if (config.Must != null)
        {
            foreach (var clause in config.Must)
            {
                FixClause(clause);
            }
        }

        if (config.Should != null)
        {
            foreach (var clause in config.Should)
            {
                FixClause(clause);
            }
        }

        if (config.MustNot != null)
        {
            foreach (var clause in config.MustNot)
            {
                FixClause(clause);
            }
        }
    }

    private string? ExtractDeck(string prompt)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            prompt.ToLowerInvariant(),
            @"deck\s+([a-z]+)"
        );
        if (match.Success)
        {
            var deckName = match.Groups[1].Value;
            return char.ToUpper(deckName[0]) + deckName.Substring(1);
        }
        return null;
    }

    private string? ExtractStake(string prompt)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            prompt.ToLowerInvariant(),
            @"stake\s+([a-z]+)"
        );
        if (match.Success)
        {
            var stakeName = match.Groups[1].Value;
            return char.ToUpper(stakeName[0]) + stakeName.Substring(1);
        }
        return null;
    }

    // Removed ExtractSeedCount - users cannot specify seed count (security: prevents abuse like "2 trillion seeds")
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

public class RefinementSteps
{
    public string Original { get; set; } = string.Empty;
    public string AfterStep1 { get; set; } = string.Empty;
    public string AfterStep2 { get; set; } = string.Empty;
    public string AfterStep3 { get; set; } = string.Empty;
    public string Final { get; set; } = string.Empty;

    // JAML transformation pipeline (for debugging)
    public string? RawJamlFromAI { get; set; }
    public string? CleanedJaml { get; set; }
    public string? FinalJaml { get; set; }
    public string? ValidationError { get; set; }
}

public class McpResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? SearchId { get; set; }
    public string? JamlFilter { get; set; }
    public string? Reasoning { get; set; }
    public List<SearchResult>? Results { get; set; }
    public List<string>? Columns { get; set; }
    public string? SearchUrl { get; set; } // URL to view full search results in JAML UI
    public RefinementSteps? RefinementSteps { get; set; } // Show prompt pipeline transformations
}
