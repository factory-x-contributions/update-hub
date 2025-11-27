using System.Buffers.Text;
using System.Text;
using Serilog;

namespace UpdateHub.Endpoints;

using UpdateHub.Models;
using Service;

public static class UpdateAasql
{
  public static RouteGroupBuilder AasqlEndpoints(this RouteGroupBuilder group)
  {
    group.MapGet("/assql/update/{singleNameplateAttribute}",
        (
          HttpRequest request,
          string singleNameplateAttribute,
          IHttpClientFactory httpClientFactory,
          IAasService aasService
        ) =>
        {
          Console.WriteLine(singleNameplateAttribute);
          return Results.Ok("ASSQL_OK");
        })
      .WithName("ASSQL")
      .WithDescription("")
      .WithSummary("Resolves one nameplate attribute to PCNs")
      .WithTags("SoftwareUpdate")
      .Produces<UpdateInformation[]>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .ProducesProblem(StatusCodes.Status500InternalServerError)
      .WithOpenApi();

    return group;
  }
}
