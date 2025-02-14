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
using Clients;
using Service;


public static class AssetEndpointsExt
{
  
  public static void AssetIdEndpoint(this WebApplication app)
  {
    app.MapGet("/asset/{AssetId}",
        (
          HttpRequest request,
          string AssetId,
          IHttpClientFactory httpClientFactory,
          IAasService aasService,
          IInventoryService inventoryService
        ) =>
        {
          //Second use AasClient with the IDLink to get update information
          var featureFlagSkipParseAAS = false;
          bool.TryParse(request.Headers["SKIP_PARSE_AAS"].ToString().ToLower(), out featureFlagSkipParseAAS);
          try
          {
            var idLink = inventoryService.GetIdLinkFromAsset(AssetId);
            return Results.Ok(aasService.GetSoftwareUpdate(idLink, featureFlagSkipParseAAS));
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
      .WithName("asset")
      .WithDescription("")
      .WithSummary("Lists software updates for assets")
      .WithTags("IAH")
      .Produces<UpdateInformation[]>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .ProducesProblem(StatusCodes.Status500InternalServerError)
      .WithOpenApi();
  }
}
