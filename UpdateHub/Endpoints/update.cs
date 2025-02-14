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

public static class UpdateEndpointsExt
{
  internal record UpdateInformationOld(List<JsonNode> ProductChangeNotifications, List<JsonNode> SoftwareNamePlates)
  {
  }

  public static RouteGroupBuilder IdLinkEndpoint(this RouteGroupBuilder group)
  {
    group.MapGet("/update/{IdLink}",
        (
          HttpRequest request,
          string idLink,
          IHttpClientFactory httpClientFactory,
          IAasService aasService
        ) =>
        {
          // Is a feature flag set?
          // HTTP header feature = true!
          /* var FeatureFlag = false;
          bool.TryParse(request.Headers["feature"].ToString().ToLower(), out FeatureFlag);
          if (!FeatureFlag)
            return Results.Problem("Feature Flag not set", statusCode: StatusCodes.Status501NotImplemented);
          */

          var featureFlagSkipParseAAS = false;
          bool.TryParse(request.Headers["SKIP_PARSE_AAS"].ToString().ToLower(), out featureFlagSkipParseAAS);
          var encodedIdLink = Uri.UnescapeDataString(idLink);
          try
          {
            return Results.Ok(aasService.GetSoftwareUpdate(encodedIdLink, featureFlagSkipParseAAS));
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
      .WithName("update")
      .WithDescription("")
      .WithSummary("Resolves a IdLink to PCNs")
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
