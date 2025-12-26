using Microsoft.AspNetCore.Mvc;
using Motely.API.Models;
using Motely.API.Services;
using Motely.Filters;

namespace Motely.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly SearchService _searchService;
    private readonly ILogger<SearchController> _logger;

    public SearchController(SearchService searchService, ILogger<SearchController> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    /// <summary>
    /// Start a new search with MotelyJsonConfig
    /// </summary>
    /// <param name="request">Search request containing MotelyJsonConfig and search criteria</param>
    /// <returns>Search ID and status</returns>
    /// <response code="200">Search started successfully</response>
    /// <response code="400">Invalid request or failed to start search</response>
    [HttpPost]
    [ProducesResponseType(typeof(SearchResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<SearchResponse>> StartSearch([FromBody] SearchRequest request)
    {
        try
        {
            _logger.LogInformation("Starting search: {Name}", request.Config?.Name);

            var searchId = await _searchService.StartSearchAsync(request.Config!, request.Criteria);

            return Ok(new SearchResponse
            {
                SearchId = searchId,
                Status = "running",
                Message = "Search started successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start search");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get search status and results
    /// </summary>
    /// <param name="id">Search ID returned from POST /api/search</param>
    /// <returns>Search status, progress, and results</returns>
    /// <response code="200">Search status retrieved</response>
    /// <response code="404">Search not found</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SearchStatusResponse), 200)]
    [ProducesResponseType(404)]
    public ActionResult<SearchStatusResponse> GetSearchStatus(string id)
    {
        var status = _searchService.GetSearchStatus(id);
        if (status == null)
        {
            return NotFound(new { error = "Search not found" });
        }

        return Ok(status);
    }

    /// <summary>
    /// Cancel a running search
    /// </summary>
    /// <param name="id">Search ID to cancel</param>
    /// <returns>Success message</returns>
    /// <response code="200">Search cancelled</response>
    /// <response code="404">Search not found</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public ActionResult CancelSearch(string id)
    {
        var cancelled = _searchService.CancelSearch(id);
        if (!cancelled)
        {
            return NotFound(new { error = "Search not found" });
        }

        return Ok(new { message = "Search cancelled" });
    }

    /// <summary>
    /// List all searches (active and completed)
    /// </summary>
    /// <returns>List of all searches with their status</returns>
    /// <response code="200">List of searches</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SearchStatusResponse>), 200)]
    public ActionResult<IEnumerable<SearchStatusResponse>> ListSearches()
    {
        var searches = _searchService.ListSearches();
        return Ok(searches);
    }
}
