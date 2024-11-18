
#if false
app.MapGet("/version", () =>
  {
    using var repo = new LibGit2Sharp.Repository(".");
    var commit = repo.Head.Tip;
    var version = new ServiceVersion(0, 0, 0, commit.Sha);
    return Results.Json(version);
  })
  .WithName("version")
  .WithDescription("")
  .WithTags("")
  .WithSummary("Service Version")
  .Produces<ServiceVersion>(StatusCodes.Status200OK)
  .WithOpenApi();
#endif
using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace UpdateHub.Controllers;

[ApiController]
[Route("[controller]")]
public class VersionController : ControllerBase
{
  private readonly ILogger<VersionController> _logger;

  public VersionController(ILogger<VersionController> logger)
  {
    _logger = logger;
  }

  /// <summary>
  /// Get version of the service
  /// </summary>
  /// <returns></returns>
  [HttpGet(Name = "version")]
  [ProducesResponseType<Version>(StatusCodes.Status200OK)]
  [Produces("application/json")]
  public async Task<IResult> GetVersion()
  {
    var version = new Version(0, 0, 0, GitHash.Value);
    return Results.Json(version);
  }

  record Version(uint major, uint minor, uint patch, string hash) { }
}
