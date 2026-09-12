using Microsoft.AspNetCore.Mvc;
using SD.LocalCoder.AI.Git.Interfaces;
using SD.LocalCoder.AI.Model.Request.AddLocalRepo;
using SD.LocalCoder.AI.Model.Request.CloneRepo;
using SD.LocalCoder.AI.Model.Request.Git;

namespace SD.LocalCoder.AI.Api.Controllers.Git
{
    [Route("api/[controller]")]
    [ApiController]
    public class GitController : ControllerBase
    {
        private readonly IGitService _gitService;

        public GitController(IGitService gitService)
        {
            _gitService = gitService;
        }

        /// <summary>List registered repository ids.</summary>
        [HttpGet]
        public IActionResult ListRepos()
        {
            var repos = _gitService.ListRepoIds();
            return Ok(new { total = repos.Count, repos });
        }

        /// <summary>Clone a public Git repository.</summary>
        [HttpPost("clone")]
        public async Task<IActionResult> CloneRepository([FromBody] CloneRepoRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.GitUrl))
                return BadRequest(new { error = "GitUrl is required" });

            var result = await _gitService.CloneRepositoryAsync(request.GitUrl);

            if (!result.Success)
                return BadRequest(new { error = result.Message });

            return Ok(new
            {
                message = result.Message,
                repoId = result.RepoId
            });
        }

        /// <summary>Add a local folder as repository.</summary>
        [HttpPost("local")]
        public async Task<IActionResult> AddLocalRepository([FromBody] AddLocalRepoRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.LocalPath))
                return BadRequest(new { error = "LocalPath is required" });

            var result = await _gitService.AddLocalRepositoryAsync(request.LocalPath);

            if (!result.Success)
                return BadRequest(new { error = result.Message });

            return Ok(new
            {
                message = result.Message,
                repoId = result.RepoId
            });
        }

        /// <summary>Get list of files from a repository.</summary>
        [HttpGet("{repoId}/files")]
        public IActionResult GetFiles(string repoId)
        {
            if (string.IsNullOrWhiteSpace(repoId))
                return BadRequest(new { error = "RepoId is required" });

            var files = _gitService.GetFileList(repoId);

            return Ok(new
            {
                repoId,
                totalFiles = files.Count,
                files
            });
        }

        /// <summary>Read content of a specific file.</summary>
        [HttpGet("{repoId}/file")]
        public IActionResult ReadFile(string repoId, [FromQuery] string path)
        {
            if (string.IsNullOrWhiteSpace(repoId) || string.IsNullOrWhiteSpace(path))
                return BadRequest(new { error = "RepoId and path are required" });

            var content = _gitService.ReadFileContent(repoId, path);

            if (content == null)
                return NotFound(new { error = "File not found" });

            return Ok(new
            {
                repoId,
                path,
                content
            });
        }

        /// <summary>
        /// Apply / write AI-generated code into the repository.
        /// Creates intermediate directories as needed.
        /// </summary>
        [HttpPut("{repoId}/file")]
        public IActionResult WriteFile(string repoId, [FromBody] WriteFileRequest request)
        {
            if (string.IsNullOrWhiteSpace(repoId))
                return BadRequest(new { error = "RepoId is required" });

            if (string.IsNullOrWhiteSpace(request?.Path))
                return BadRequest(new { error = "Path is required" });

            var result = _gitService.WriteFileContent(repoId, request.Path, request.Content ?? string.Empty);

            if (!result.Success)
                return BadRequest(new { error = result.Message });

            return Ok(new
            {
                message = result.Message,
                repoId,
                path = request.Path
            });
        }
    }
}
