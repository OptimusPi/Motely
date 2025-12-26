using Microsoft.AspNetCore.Mvc;

namespace Motely.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WordListsController : ControllerBase
{
    private const string WORD_LISTS_DIR = "WordLists";
    
    private readonly ILogger<WordListsController> _logger;

    public WordListsController(ILogger<WordListsController> logger, IWebHostEnvironment env)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get list of available word list files
    /// </summary>
    /// <returns>List of word list filenames</returns>
    [HttpGet]
    [ProducesResponseType(typeof(WordListsResponse), 200)]
    public ActionResult<WordListsResponse> GetWordLists()
    {
        try
        {
            if (!Directory.Exists(WORD_LISTS_DIR))
            {
                _logger.LogWarning("WordLists directory not found: {Path}", WORD_LISTS_DIR);
                return Ok(new WordListsResponse { WordLists = Array.Empty<string>() });
            }

            var wordLists = Directory
                .GetFiles(WORD_LISTS_DIR, "*.txt")
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Cast<string>()
                .OrderBy(f => f)
                .ToArray();

            _logger.LogInformation("Found {Count} word lists", wordLists.Length);
            return Ok(new WordListsResponse { WordLists = wordLists });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list word lists");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class WordListsResponse
{
    public string[] WordLists { get; set; } = Array.Empty<string>();
}
