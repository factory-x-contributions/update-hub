using System.Buffers.Text;
using System.Text;
using Serilog;

namespace UpdateHub.Endpoints;

using UpdateHub.Models;
using Service;

public static class HandoverIrs
{
    public static RouteGroupBuilder IdLinkIrsEndpointHandover(this RouteGroupBuilder group)
    {
        group.MapGet("/irs/handover/{idLinkBase64Encoded}",
            (
              HttpRequest request,
              string idLinkBase64Encoded,
              IHttpClientFactory httpClientFactory,
              IIrsService irsService
            ) =>
            {
                // var encodedIdLink = "";

                // TODO: Make input validation readable
                try
                {
                    return Results.Ok(irsService.GetHandoverDocumentation(idLinkBase64Encoded, request));
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
          .WithName("Handover IRS")
          .WithDescription("")
          .WithSummary("Resolves a IdLink to Handover Documentation via IRS")
          .WithTags("HandoverDocumentation")
          .Produces<HandoverDocumentation[]>(StatusCodes.Status200OK)
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status500InternalServerError)
          .WithOpenApi();

        return group;
    }
}
