namespace UpdateHub.Endpoints;

public static class Version
{
  internal record ServiceVersion(uint major, uint minor, uint patch, string hash)
  {
  }

  public static void VersionEndpoint(this WebApplication app)
  {
    app.MapGet("/version", () =>
      {
        var version = new ServiceVersion( UpdateHub.Version.ServiceVersion.Major(),
          UpdateHub.Version.ServiceVersion.Minor(),
          UpdateHub.Version.ServiceVersion.Patch(),
          UpdateHub.Version.ServiceVersion.Commit());
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
