using System.Buffers.Text;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using Refit;
using Serilog;
using UpdateHub.Domain;
using UpdateHub.Models;

namespace UpdateHub.Service;

public partial class AasService
{

  private List<UpdateInformation> getSoftwareUpdateV3(string idLink, AasServer aasServer, bool featureFlagSkipParseAAS)
  {
    var httpClient = _httpClientFactory.CreateClient();
    if (aasServer.Auth != null)
      if (!aasServer.Auth.Authenticate(httpClient, _httpClientFactory))
        throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while executing authentication");

    httpClient.BaseAddress = new Uri(aasServer.Url);
    var _restApiService = RestService.For<IAasApi>(httpClient);

    // Temporary, till Discovery Endpoints differs from other ones...
    var httpClientDiscovery = _httpClientFactory.CreateClient();
    if (aasServer.Auth != null)
      if (!aasServer.Auth.Authenticate(httpClientDiscovery, _httpClientFactory))
        throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while executing authentication");

    // TODO: Flexible Discovery URL
    httpClientDiscovery.BaseAddress = new Uri(aasServer.DiscoveryUrl);
    var _restApiServiceDiscovery = RestService.For<IAasApi>(httpClientDiscovery);
    try
    {
      //
      // Get assetID from IdLink
      //
      // @TODO: Detect a missing authorization. Currently, the AAS redirects ...
      var shellIds = _restApiServiceDiscovery.LookupShellsByAssetIds(new IAasApi.LookupShellsRequest(idLink).ToString()).Result;
      //var shellIds = _restApiService.LookupShellsByAssetId(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(idLink))).Result;
      // Check if endpoint is authorized
      // @TODO: Detect a missing authorization. Currently, the AAS redirects to an IDM, and response with a certain media
      // type. But, why does the AAS server does not issue a proper HTTP status code?
      // Sick workaround with check for MediaType "text/html"
      if (shellIds.StatusCode == HttpStatusCode.Unauthorized ||
          shellIds.ContentHeaders.ContentType.MediaType == "text/html")
        throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while access AAS server");

      if (!shellIds.IsSuccessful)
        throw new HttpProblemResponseException(StatusCodes.Status422UnprocessableEntity, "Error while fetching IdLink from AAS server");

      if (shellIds.Content.Result.Count == 0)
      {
        AasNotFound.Add(1, new KeyValuePair<string, object>("IdLink", idLink.ToString()));
        throw new HttpProblemResponseException(StatusCodes.Status404NotFound, "No shells found for given IdLink");
      }

      //
      // Shells
      //
      Dictionary<string, JsonNode> receivedPcns = new();
      Dictionary<string, JsonNode> receivedSoftwareNameplates = new();

      foreach (var shellId in shellIds.Content.Result) //TODO Result
      {
        var shellIdEncoded = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(shellId));
        var response = _restApiService.GetShell(shellIdEncoded).Result;
        if (!response.IsSuccessful)
          throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Error while fetching Shells from AAS server");

          foreach (var r in response.Content?["submodels"].AsArray())
          {
            foreach (var rr in r?["keys"].AsArray())
            {
              if (rr["type"].ToString().ToUpper().Equals("SUBMODEL"))
              {
                var id = rr?["value"].ToString();
                var shell = _restApiService
                  .GetSubmodelsFromShell(shellIdEncoded, Base64Url.EncodeToString(Encoding.UTF8.GetBytes(id))).Result;

                if (!shell.IsSuccessful)
                  throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError,
                    "Error while fetching Shell from AAS server");

                if (shell.Content["idShort"].ToString().Contains("ProductChangeNotifications"))
                {
                  var smId = shell.Content?["id"].ToString();

                  var pcn = _restApiService
                    .GetSubmodelsFromShell(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(shellId)),
                      Base64Url.EncodeToString(Encoding.UTF8.GetBytes(smId))).Result;
                  if (!pcn.IsSuccessful)
                    throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Error while fetching Software Nameplate from AAS server");

                  receivedPcns[pcn.Content["id"].AsValue().ToString()] = pcn.Content;
                }


                if (shell.Content["idShort"].ToString().Contains("SoftwareNameplate"))
                {
                  var smId = shell.Content?["id"].ToString();

                  var nameplate = _restApiService
                    .GetSubmodelsFromShell(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(shellId)),
                      Base64Url.EncodeToString(Encoding.UTF8.GetBytes(smId))).Result;
                  if (!nameplate.IsSuccessful)
                    throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Error while fetching Software Nameplate from AAS server");

                  receivedSoftwareNameplates[nameplate.Content["id"].AsValue().ToString()] = nameplate.Content;
                }
              }
            }
          }
      }

      if (!featureFlagSkipParseAAS)
        return PcnParser.parsePcnAndSoftwareNameplateSubmodels(receivedPcns.Values.ToList(),
          receivedSoftwareNameplates.Values.ToList());

      // Fallback, since the AAS libary does not work on arm64
      JsonObject pcnJsonObject = null;
      JsonObject softwareNameplateJsonObject = null;
      foreach (var l in receivedSoftwareNameplates)
      {
        softwareNameplateJsonObject = l.Value.AsObject();
      }
      foreach (var l in receivedPcns)
      {
        pcnJsonObject = l.Value.AsObject();
      }
      var updates = new List<UpdateInformation>();
      var update = new UpdateInformation("","", "", "", "", softwareNameplateJsonObject, pcnJsonObject);
      updates.Add(update);

      AasFound.Add(1, new KeyValuePair<string, object>("IdLink",idLink.ToString()));
      return updates;
    }
    catch (HttpProblemResponseException e)
    {
      throw;
    }
    catch (Exception e)
    {
      Console.WriteLine(e);
      throw new Exception("Unknown exception" + e);
    }
  }
}
