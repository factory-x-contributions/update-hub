using System.Buffers.Text;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Refit;
using UpdateHub.Domain;
using UpdateHub.Models;

namespace UpdateHub.Service;
public interface IAasService
{
  public List<UpdateInformation> GetSoftwareUpdate(string idLink, bool featureFlagSkipParseAAS);
}

public class AasService : IAasService
{
  private readonly AasServerRepository? _repository;
  private readonly IHttpClientFactory? _httpClientFactory;

  public AasService(AasServerRepository aasServerRepository, IHttpClientFactory httpClientFactory)
  {
    _repository = aasServerRepository;
    _httpClientFactory = httpClientFactory;
  }

  public List<UpdateInformation> GetSoftwareUpdate(string idLink, bool featureFlagSkipParseAAS)
  {
    var aasServer = _repository.GetByIdLink(idLink);
    if (aasServer == null)
      throw new HttpProblemResponseException(StatusCodes.Status404NotFound,"No AAS Server for given IDLink found");

    var httpClient = _httpClientFactory.CreateClient();
    if (aasServer.Auth != null)
      if (!aasServer.Auth.Authenticate(httpClient))
        throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while executing authentication");

    httpClient.BaseAddress = new Uri(aasServer.Url);
    var _restApiService = RestService.For<IAasApi>(httpClient);
    try
    {
      //
      // Get assetID from IdLink
      //
      // @TODO: Detect a missing authorization. Currently, the AAS redirects ...
      var shellIds = _restApiService.LookupShells(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(idLink))).Result;

      // Check if endpoint is authorized
      // @TODO: Detect a missing authorization. Currently, the AAS redirects to an IDM, and response with a certain media
      // type. But, why does the AAS server does not issue a proper HTTP status code?
      // Sick workaround with check for MediaType "text/html"
      if (shellIds.StatusCode == HttpStatusCode.Unauthorized ||
          shellIds.ContentHeaders.ContentType.MediaType == "text/html")
        throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while access AAS server");

      if (!shellIds.IsSuccessful)
        throw new HttpProblemResponseException(StatusCodes.Status422UnprocessableEntity, "Error while fetching IdLink from AAS server");

      if (shellIds.Content.Count == 0)
        throw new HttpProblemResponseException(StatusCodes.Status404NotFound, "No shells found for given IdLink");

      Dictionary<string, JsonNode> receivedPcns = new();
      Dictionary<string, JsonNode> receivedSoftwareNameplates = new();

      //
      // Shell Descriptors
      //

      foreach (var shellId in shellIds.Content)
      {
        var shellIdEncoded = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(shellId));
        var response = _restApiService.GetShellDescriptors(shellIdEncoded).Result;
        if (!response.IsSuccessful)
          throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Error while fetching Shell Descriptors from AAS server");


        foreach (var d in response.Content.SubmodelDescriptors)
        {
          if (d.idShort.Contains("ProductChangeNotifications"))
          {
            var pcn = _restApiService.GetSubmodelsFromShell(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(shellId)),
                Base64Url.EncodeToString(Encoding.UTF8.GetBytes(d.id)))
              .Result;
            if (!pcn.IsSuccessful)
              throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Error while fetching PCN from AAS server");

            receivedPcns[pcn.Content["id"].AsValue().ToString()] = pcn.Content;
          }

          if (d.idShort.Contains("SoftwareNameplate"))
          {
            var nameplate = _restApiService
              .GetSubmodelsFromShell(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(shellId)),
                Base64Url.EncodeToString(Encoding.UTF8.GetBytes(d.id))).Result;
            if (!nameplate.IsSuccessful)
              throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Error while fetching Software Nameplate from AAS server");

            receivedSoftwareNameplates[nameplate.Content["id"].AsValue().ToString()] = nameplate.Content;
          }
        }
      }

      if (!featureFlagSkipParseAAS)
        return PcnParser.parsePcnAndSoftwareNameplateSubmodels(receivedPcns.Values.ToList(),
          receivedSoftwareNameplates.Values.ToList());

      var updates = new List<UpdateInformation>();
      var update = new UpdateInformation("", "", "", "", null, null);
      updates.Add(update);

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
