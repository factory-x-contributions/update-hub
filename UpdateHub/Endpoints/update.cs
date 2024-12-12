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
using UpdateHub.Helper;

namespace UpdateHub.Endpoints;

using Refit;
using UpdateHub;
using Domain;
using Configuration;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Text.Json;

public static class UpdateEndpointsExt
{
  internal record UpdateInformation(List<string> ProductChangeNotifications, List<string> SoftwareNamePlates)
  {
  }

/*
  public static async Task<Results<Ok<String>, ProblemHttpResult>> Idlink1(
    string idLink,
    AasModelFetcherService aasModelFetcherService)
  {

      var result = await aasModelFetcherService.GetPcns(idLink);


    return TypedResults.Problem("Error Test",  statusCode: StatusCodes.Status405MethodNotAllowed);
  }
*/
  public static void Idlink(this WebApplication app)
  {
    /*
    app.MapGet("/update1/{idLink}", Idlink1)
    .WithName("update1")
      .WithDescription("No implemented, yet.\nReturns only HTTP status code 405")
      .WithSummary("Resolves a IdLink to PCNs")
      .WithTags("PCN")
      .Produces<string>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status405MethodNotAllowed)
      .WithOpenApi();
*/

    app.MapGet("/update/{IdLink}",
        (
          HttpRequest request,
          string IdLink,
          IHttpClientFactory httpClientFactory, AasServerService aasServerService
        ) =>
        {
          // Is a feature flag set?
          // HTTP header feature = true!
          /* var FeatureFlag = false;
          bool.TryParse(request.Headers["feature"].ToString().ToLower(), out FeatureFlag);
          if (!FeatureFlag)
            return Results.Problem("Feature Flag not set", statusCode: StatusCodes.Status501NotImplemented);
          */

          var encodedIdLink = Uri.UnescapeDataString(IdLink);
          var aasServer = aasServerService.GetAasRepository().GetByIdLink(encodedIdLink);
          if (aasServer == null)
            return Results.Problem("No AAS Server for given IDLink found", statusCode: StatusCodes.Status404NotFound);
          //var httpClient = httpClientFactory.CreateClient(aasServer.Name);
          var httpClient = httpClientFactory.CreateClient();

          if (aasServer.Auth != null)
          {
            if (!aasServer.Auth.Authenticate(httpClient))
              return Results.Problem("Error while executing authentifcation", statusCode: StatusCodes.Status401Unauthorized);
          }
          httpClient.BaseAddress = new Uri(aasServer.Url);
          var _restApiService = RestService.For<IAasApi>(httpClient);

          //
          // Get assetID from IdLink
          //
          // @TODO: Detect a missing authorization. Currently, the AAS redirects ...
          var shellIds = _restApiService.LookupShells(Base64UrlOwnImplementation.Encode(encodedIdLink)).Result;

          // Check if endpoint is authorized
          // @TODO: Detect a missing authorization. Currently, the AAS redirects to an IDM, and response with a certain media
          // type. But, why does the AAS server does not issue a proper HTTP status code?
          // Sick workaround with check for MediaType "text/html"
          if ((shellIds.StatusCode == HttpStatusCode.Unauthorized) ||  (shellIds.ContentHeaders.ContentType.MediaType == "text/html"))
            return Results.Problem("Error while access AAS server", statusCode: StatusCodes.Status401Unauthorized);

          if (!shellIds.IsSuccessful)
            return Results.Problem("Error while fetching IdLink from AAS server",
              statusCode: StatusCodes.Status500InternalServerError);
          if (shellIds.Content.Count != 1)
            return Results.Problem("No or multiple shells found for given IdLink",
              statusCode: StatusCodes.Status404NotFound);

          //
          // Shell Descriptors
          //
          var assetId = shellIds.Content[0];
          var response = _restApiService.GetShellDescriptors(Base64UrlOwnImplementation.Encode(assetId)).Result;
          if (!response.IsSuccessful)
            return Results.Problem("Error while fetching Shell Descriptors from AAS server",
              statusCode: StatusCodes.Status500InternalServerError);

          List<string> receivedPcns = new();
          List<string> receivedSoftwareNameplates = new();
          foreach (var d in response.Content.SubmodelDescriptors)
          {
            if (d.idShort == "ProductChangeNotifications")
            {
              var pcn = _restApiService.GetSubmodelsFromShell(Base64UrlOwnImplementation.Encode(assetId), Base64UrlOwnImplementation.Encode(d.id))
                .Result;
              if (!pcn.IsSuccessful)
                return Results.Problem("Error while fetching PCN from AAS server",
                  statusCode: StatusCodes.Status500InternalServerError);

              receivedPcns.Add(pcn.Content);
            }

            if (d.idShort == "SoftwareNameplate")
            {
              var nameplate = _restApiService
                .GetSubmodelsFromShell(Base64UrlOwnImplementation.Encode(assetId), Base64UrlOwnImplementation.Encode(d.id)).Result;
              if (!nameplate.IsSuccessful)
                return Results.Problem("Error while fetching Software Nameplate from AAS server",
                  statusCode: StatusCodes.Status500InternalServerError);

              receivedSoftwareNameplates.Add(nameplate.Content);
            }
          }


          var productChangeNofication = new UpdateInformation(receivedPcns, receivedSoftwareNameplates);
          return Results.Json(productChangeNofication);
        })
      .WithName("update")
      .WithDescription("No fully implemented, yet.\n")
      .WithSummary("Resolves a IdLink to PCNs")
      .WithTags("SoftwareUpdate")
      .Produces<UpdateInformation[]>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status405MethodNotAllowed)
      .WithOpenApi();
  }
}
