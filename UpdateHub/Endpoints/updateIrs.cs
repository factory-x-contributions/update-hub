using System.Buffers.Text;
using System.Text;
using Serilog;

namespace UpdateHub.Endpoints;

using UpdateHub.Models;
using Service;

public static class UpdateIrs
{
  public static RouteGroupBuilder IdLinkIrsEndpoint(this RouteGroupBuilder group)
  {
    group.MapGet("/irs/update/{idLinkBase64Encoded}",
        (
          HttpRequest request,
          string idLinkBase64Encoded,
          IHttpClientFactory httpClientFactory,
          IIrsService irsService
        ) =>
        {
          var encodedIdLink = "";

          // TODO: Make input validation readable
          try
          {
            return Results.Ok(irsService.GetSoftwareUpdate(idLinkBase64Encoded, request));
          }
          catch (HttpProblemResponseException e)
          {
            return Results.Problem(e.Value.ToString(), statusCode: e.StatusCode);
          }
          // Unknown exception handled by dotnet itself, with
          // UseExceptionHandler and UseDeveloperExceptionPage
          catch (Exception e)
          {
            Log.Information(e.ToString());
            return Results.Problem(e.Message, statusCode: StatusCodes.Status500InternalServerError);
          }
        })
      .WithName("Update IRS")
      .WithDescription("")
      .WithSummary("Resolves a IdLink to update information via IRS")
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
