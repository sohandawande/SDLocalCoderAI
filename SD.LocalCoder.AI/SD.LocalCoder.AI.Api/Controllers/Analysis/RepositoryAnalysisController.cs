using Microsoft.AspNetCore.Mvc;
using SD.LocalCoder.AI.Core.Interfaces;
using SD.LocalCoder.AI.Model.Request.Repository;

namespace SD.LocalCoder.AI.Api.Controllers.Analysis;

[ApiController]
[Route("api/repository-analysis")]
public sealed class RepositoryAnalysisController : ControllerBase
{
    private readonly IProjectIntelligenceService _intelligence;

    public RepositoryAnalysisController(IProjectIntelligenceService intelligence)
    {
        _intelligence = intelligence;
    }

    [HttpGet("{repoId}")]
    public async Task<IActionResult> Analyze(
        string repoId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repoId))
            return BadRequest(new { error = "RepoId is required" });

        var result = await _intelligence.AnalyzeAsync(repoId, cancellationToken);

        return result is null
            ? NotFound(new { error = "Repository not found or contains no analyzable files" })
            : Ok(result);
    }

    [HttpPost("{repoId}/search")]
    public async Task<IActionResult> Search(
        string repoId,
        [FromBody] SearchRepositoryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repoId))
            return BadRequest(new { error = "RepoId is required" });

        if (string.IsNullOrWhiteSpace(request?.Query))
            return BadRequest(new { error = "Query is required" });

        var results = await _intelligence.SearchAsync(
            repoId,
            request.Query,
            request.MaxResults,
            cancellationToken);

        return Ok(new
        {
            repoId,
            query = request.Query,
            total = results.Count,
            results
        });
    }
}
