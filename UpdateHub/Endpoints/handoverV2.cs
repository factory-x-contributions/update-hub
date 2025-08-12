using System.Buffers.Text;
using System.Text;
using Serilog;

namespace UpdateHub.Endpoints;

using UpdateHub.Models;
using Service;

public static class HandoverV2EndpointsExt
{
    public static RouteGroupBuilder IdLinkV2EndpointHandover(this RouteGroupBuilder group)
    {
        group.MapGet("/handover/{assetIdBase64Encoded}",
            (
              HttpRequest request,
              string assetIdBase64Encoded,
              IHttpClientFactory httpClientFactory,
              IAasService aasService
            ) =>
            {
                var encodedIdLink = "";

                try
                {
                    // Decode the base64-encoded IdLink
                    encodedIdLink = Encoding.UTF8.GetString(Base64Url.DecodeFromUtf8(Encoding.UTF8.GetBytes(assetIdBase64Encoded)));

                    // Fetch handover documentation using the service
                    var handoverDocs = aasService.GetHandoverDocumentation(encodedIdLink, request);

                    return Results.Ok(handoverDocs);
                }
                catch (HttpProblemResponseException e)
                {
                    return Results.Problem(e.Value.ToString(), statusCode: e.StatusCode);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error resolving IdLink to Handover Documentation");
                    return Results.Problem(e.Message, statusCode: StatusCodes.Status500InternalServerError);
                }
            })
          .WithName("Handover V2")
          .WithSummary("Resolves a IdLink to Handover Documentation")
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
