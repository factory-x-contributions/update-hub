using System.Buffers.Text;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Refit;
using Serilog;
using UpdateHub.Configuration;
using UpdateHub.Domain;
using UpdateHub.Models;

namespace UpdateHub.Service;


public interface IIrsService
{
  public List<UpdateInformation> GetSoftwareUpdate(string idLink, HttpRequest request);
}


public partial class IrsService : IIrsService
{
  private readonly ApplicationConfig? _applicationConfig;
  private readonly IHttpClientFactory? _httpClientFactory;

  private static readonly System.Diagnostics.Metrics.Meter meter = new Meter("IRSBroker",
    "1.0.0");
  private static readonly Counter<int> AasFound = meter.CreateCounter<int>("aas_found", "counter", "Counts the number of found AAS shells");
  private static readonly Counter<int> AasNotFound = meter.CreateCounter<int>("aas_not_found","counter", "Counts the number of not found AAS shells");


  public IrsService(ApplicationConfig applicationConfig, IHttpClientFactory httpClientFactory)
  {
    _applicationConfig = applicationConfig;
    _httpClientFactory = httpClientFactory;
  }

  private bool skipParseAas(HttpRequest request)
  {
    var flag = false;
    return bool.TryParse(request.Headers["SKIP_PARSE_AAS"].ToString().ToLower(), out flag);
  }

  public List<UpdateInformation> GetSoftwareUpdate(string idLink, HttpRequest request)
  {
    var featureFlagSkipParseAAS = skipParseAas(request);

    var semanticIdSoftwareNameplateSubmodel = "https://admin-shell.io/idta/SoftwareNameplate/1/0";
    var semanticIdPcnSubmodel = "0173-10029#01-XFB001#001";

    var httpClient = _httpClientFactory.CreateClient();
    var irsAuth = new Oauth2PasswordFlow();
    irsAuth.Username = _applicationConfig.irs.Username;
    irsAuth.Password = _applicationConfig.irs.Password;
    irsAuth.TokenUrl = $"{_applicationConfig.irs.Url}/api/v1/token";

    if (!irsAuth.Authenticate(httpClient, _httpClientFactory))
      throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while executing authentication with IRS");

    httpClient.BaseAddress = new Uri($"{_applicationConfig.irs.Url}/api/v1");
    var _restApiService = RestService.For<IIrsApi>(httpClient);

    try
    {

      var pcn = _restApiService.GetSubmodelsFromIdLink(idLink, semanticIdPcnSubmodel).Result;
      if (pcn.StatusCode != HttpStatusCode.OK)
      {

        AasNotFound.Add(1, new KeyValuePair<string, object>("IdLink", idLink.ToString()));
        throw new HttpProblemResponseException(StatusCodes.Status404NotFound, "No PCN found for given IdLink");
      }

      var receivedPcnSubmodels = pcn.Content;
      var receivedSoftwareNameplateSubmodels = _restApiService.GetSubmodelsFromIdLink(idLink, semanticIdSoftwareNameplateSubmodel).Result.Content;

      if (!featureFlagSkipParseAAS)
        return PcnParser.parsePcnAndSoftwareNameplateSubmodels(receivedPcnSubmodels,
          receivedSoftwareNameplateSubmodels);

      // Fallback, since the AAS libary does not work on arm64
      JsonObject pcnJsonObject = null;
      JsonObject softwareNameplateJsonObject = null;
      foreach (var l in receivedSoftwareNameplateSubmodels)
      {
        softwareNameplateJsonObject = l.AsObject();
      }
      foreach (var l in receivedPcnSubmodels)
      {
        pcnJsonObject = l.AsObject();
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
