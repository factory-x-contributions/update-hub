using System.Buffers.Text;
using System.Diagnostics.Metrics;
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
using Clients;
using Service;

public static class AssetEndpointsExt
{
  private static readonly System.Diagnostics.Metrics.Meter meter = new Meter("IndustrialAssetHub", "1.0.0");
  private static readonly Gauge<int> CounterAASVsIrs = meter.CreateGauge<int>("aas_vs_irs", "gauge", "Counts the number of found assets");
  public static RouteGroupBuilder AssetIdEndpoint(this RouteGroupBuilder group)
  {
    group.MapGet("/asset/{AssetId}",
        (
          HttpRequest request,
          string AssetId,
          IHttpClientFactory httpClientFactory,
          IAasService aasService,
          IInventoryService inventoryService,
          IIrsService irsService
        ) =>
        {
          try
          {
            var idLink = inventoryService.GetIdLinkFromAsset(AssetId);
            var encodedIdLink = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(idLink));

            var update = aasService.GetSoftwareUpdate(idLink, request);

            var updateFromIrs = new List<UpdateInformation>();
            try
            {
              updateFromIrs = irsService.GetSoftwareUpdate(encodedIdLink, request);
              //return Results.Ok(updateFromIrs);
            }
            catch
            {
            }
            finally
            {
              CounterAASVsIrs.Record(update.Count - updateFromIrs.Count,new KeyValuePair<string, object>("IdLink",idLink.ToString()));
              Log.Debug("Update from AAS {0} / IRS: {1}", update.Count, updateFromIrs.Count);
              Log.Debug((update?.Count == updateFromIrs?.Count).ToString());
            }

            return Results.Ok(update);
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

    return group;
  }
}
