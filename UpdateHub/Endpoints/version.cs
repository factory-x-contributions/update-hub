namespace UpdateHub.Endpoints;
using UpdateHub;

public static class Version
{
  internal record ServiceVersion(uint major, uint minor, uint patch, string hash)
  {
  }

  public static void VersionEndpoint(this WebApplication app)
  {
    app.MapGet("/version", () =>
      {
        var version = new ServiceVersion( GitHash.major, GitHash.minor, GitHash.patch, GitHash.Value);
        return Results.Json(version);
      })
      .WithName("version")
      .WithDescription("")
      .WithTags("")
      .WithSummary("Service version")
      .Produces<ServiceVersion>(StatusCodes.Status200OK)
      .WithOpenApi();
  }
}
