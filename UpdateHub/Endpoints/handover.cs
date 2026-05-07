using System.Buffers.Text;
using System.Net;
using System.Net.Mime;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace UpdateHub.Endpoints;

using Refit;
using UpdateHub;
using Domain;
using Configuration;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Text.Json;
using UpdateHub.Models;
using Service;

public static class HandoverEndpointsExt
{
    public static RouteGroupBuilder IdLinkEndpointHandover(this RouteGroupBuilder group)
    {
        group.MapGet("/handover/{IdLink}",
            (
              HttpRequest request,
              string idLink,
              IHttpClientFactory httpClientFactory,
              IAasService aasService
            ) =>
            {
                var encodedIdLink = Uri.UnescapeDataString(idLink);
                try
                {
                    return Results.Ok(aasService.GetHandoverDocumentation(encodedIdLink, request));
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
          .WithName("handover")
          .WithDescription("")
          .WithSummary("Resolves a IdLink to Handover Documentation")
          .WithTags("HandoverDocumentation")
          .Produces<UpdateInformation[]>(StatusCodes.Status200OK)
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status500InternalServerError)
          .WithOpenApi();

        return group;
    }
}
