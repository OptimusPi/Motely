using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Motely.API;

/// <summary>
/// Feedback and learning system for JamlGenie
/// Stores failed attempts and user feedback for continuous improvement
/// </summary>
public class GenieFeedbackService
{
    private readonly ILogger<GenieFeedbackService> _logger;
    private readonly string _feedbackDir;
    private readonly string _failuresFile;
    private readonly string _feedbackFile;

    public GenieFeedbackService(ILogger<GenieFeedbackService> logger)
    {
        _logger = logger;
        _feedbackDir = "GenieFeedback";
        Directory.CreateDirectory(_feedbackDir);
        _failuresFile = Path.Combine(_feedbackDir, "failures.jsonl");
        _feedbackFile = Path.Combine(_feedbackDir, "feedback.jsonl");
    }

    public void LogFailure(
        string prompt,
        string generatedJaml,
        string aiReasoning,
        string error,
        object? context = null
    )
    {
        var failure = new
        {
            timestamp = DateTime.UtcNow,
            prompt = prompt,
            generatedJaml = generatedJaml,
            aiReasoning = aiReasoning,
            error = error,
            context = context,
        };

        var json = JsonSerializer.Serialize(failure);
        File.AppendAllText(_failuresFile, json + Environment.NewLine);
        _logger.LogWarning($"Genie failure logged: {error}");
    }

    public void LogFeedback(string prompt, string searchId, bool success, string? feedback = null)
    {
        var feedbackEntry = new
        {
            timestamp = DateTime.UtcNow,
            prompt = prompt,
            searchId = searchId,
            success = success,
            feedback = feedback,
        };

        var json = JsonSerializer.Serialize(feedbackEntry);
        File.AppendAllText(_feedbackFile, json + Environment.NewLine);
        _logger.LogInformation($"Genie feedback logged: {(success ? "success" : "failure")}");
    }

    public List<GenieFailure> GetRecentFailures(int count = 50)
    {
        var failures = new List<GenieFailure>();

        if (!File.Exists(_failuresFile))
            return failures;

        var lines = File.ReadAllLines(_failuresFile);
        var recentLines = lines.TakeLast(count).Reverse();

        foreach (var line in recentLines)
        {
            try
            {
                var failure = JsonSerializer.Deserialize<GenieFailure>(line);
                if (failure != null)
                    failures.Add(failure);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to parse failure entry: {line}");
            }
        }

        return failures;
    }

    public List<GenieFeedback> GetRecentFeedback(int count = 50)
    {
        var feedbacks = new List<GenieFeedback>();

        if (!File.Exists(_feedbackFile))
            return feedbacks;

        var lines = File.ReadAllLines(_feedbackFile);
        var recentLines = lines.TakeLast(count).Reverse();

        foreach (var line in recentLines)
        {
            try
            {
                var feedback = JsonSerializer.Deserialize<GenieFeedback>(line);
                if (feedback != null)
                    feedbacks.Add(feedback);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to parse feedback entry: {line}");
            }
        }

        return feedbacks;
    }

    public string GetFailureContextForPrompt(int recentFailureCount = 5)
    {
        var failures = GetRecentFailures(recentFailureCount);
        if (failures.Count == 0)
            return string.Empty;

        var context = new System.Text.StringBuilder();
        context.AppendLine("\n--- PAST FAILURES TO LEARN FROM ---");

        foreach (var failure in failures)
        {
            context.AppendLine($"\nFAILED REQUEST: {failure.Prompt}");
            context.AppendLine($"ERROR: {failure.Error}");
            context.AppendLine($"GENERATED JAML (WRONG): {failure.GeneratedJaml}");
            if (!string.IsNullOrEmpty(failure.AiReasoning))
                context.AppendLine($"AI REASONING: {failure.AiReasoning}");
            context.AppendLine("---");
        }

        context.AppendLine(
            "\nDO NOT repeat these mistakes. Analyze what went wrong and generate correct JAML."
        );
        return context.ToString();
    }
}

public class GenieFailure
{
    public DateTime Timestamp { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string GeneratedJaml { get; set; } = string.Empty;
    public string AiReasoning { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public object? Context { get; set; }
}

public class GenieFeedback
{
    public DateTime Timestamp { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string SearchId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Feedback { get; set; }
}
