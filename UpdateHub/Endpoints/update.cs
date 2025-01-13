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
using UpdateHub.Models;

public static class UpdateEndpointsExt
{
  internal record UpdateInformationOld(List<JsonNode> ProductChangeNotifications, List<JsonNode> SoftwareNamePlates)
  {
  }

  public static void IdLinkEndpoint(this WebApplication app)
  {
    app.MapGet("/update/{IdLink}",
        (
          HttpRequest request,
          string IdLink,
          IHttpClientFactory httpClientFactory, AasServerRepository aasServerRepository
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

          var encodedIdLink = Uri.UnescapeDataString(IdLink);
          var aasServer = aasServerRepository.GetByIdLink(encodedIdLink);
          if (aasServer == null)
            return Results.Problem("No AAS Server for given IDLink found", statusCode: StatusCodes.Status404NotFound);
          //var httpClient = httpClientFactory.CreateClient(aasServer.Name);
          var httpClient = httpClientFactory.CreateClient();


          // TODO: use proper caching, instead of doing it for every curl.
          // May, https://github.com/TurnerSoftware/CacheTower is an option
          if (aasServer.Auth != null)
          {
            if (!aasServer.Auth.Authenticate(httpClient))
              return Results.Problem("Error while executing authentication", statusCode: StatusCodes.Status401Unauthorized);
          }
          httpClient.BaseAddress = new Uri(aasServer.Url);
          var _restApiService = RestService.For<IAasApi>(httpClient);

          try
          {

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
              statusCode: StatusCodes.Status422UnprocessableEntity);
          if (shellIds.Content.Count == 0 )
            return Results.Problem("No shells found for given IdLink",
              statusCode: StatusCodes.Status404NotFound);

          Dictionary<string, JsonNode> receivedPcns = new();
          Dictionary<string, JsonNode> receivedSoftwareNameplates = new();

          //
          // Shell Descriptors
          //

          foreach (var shellId in shellIds.Content)
          {
            var shellIdEncoded = Base64UrlOwnImplementation.Encode(shellId);
            var response = _restApiService.GetShellDescriptors(shellIdEncoded).Result;
            if (!response.IsSuccessful)
              return Results.Problem("Error while fetching Shell Descriptors from AAS server",
                statusCode: StatusCodes.Status500InternalServerError);


            foreach (var d in response.Content.SubmodelDescriptors)
            {
              if (d.idShort.Contains("ProductChangeNotifications"))
              {
                var pcn = _restApiService.GetSubmodelsFromShell(Base64UrlOwnImplementation.Encode(shellId), Base64UrlOwnImplementation.Encode(d.id))
                  .Result;
                if (!pcn.IsSuccessful)
                  return Results.Problem("Error while fetching PCN from AAS server",
                    statusCode: StatusCodes.Status500InternalServerError);

                receivedPcns[pcn.Content["id"].AsValue().ToString()] = pcn.Content;
              }

              if (d.idShort.Contains("SoftwareNameplate"))
              {
                var nameplate = _restApiService
                  .GetSubmodelsFromShell(Base64UrlOwnImplementation.Encode(shellId), Base64UrlOwnImplementation.Encode(d.id)).Result;
                if (!nameplate.IsSuccessful)
                  return Results.Problem("Error while fetching Software Nameplate from AAS server",
                    statusCode: StatusCodes.Status500InternalServerError);

                receivedSoftwareNameplates[nameplate.Content["id"].AsValue().ToString()] = nameplate.Content;
              }
            }
          }

					if (!featureFlagSkipParseAAS) {
          	return Results.Json(PcnParser.parsePcnAndSoftwareNameplateSubmodels(receivedPcns.Values.ToList(), receivedSoftwareNameplates.Values.ToList()));
					}
					else {
          	return Results.Json(new UpdateInformationOld(receivedPcns.Values.ToList(), receivedSoftwareNameplates.Values.ToList()));
					}

          }
          catch (System.Net.Sockets.SocketException e)
          {
            return Results.Problem("AAS Server could not be reached.");
          }
          catch (Exception e)
          {
            Console.WriteLine(e);
            return Results.Problem("Unknown exception", e.ToString());
          }
        })
      .WithName("update")
      .WithDescription("No fully implemented, yet.\n")
      .WithSummary("Resolves a IdLink to PCNs")
      .WithTags("SoftwareUpdate")
      .Produces<UpdateInformation[]>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .ProducesProblem(StatusCodes.Status500InternalServerError)
      .WithOpenApi();
  }
}
