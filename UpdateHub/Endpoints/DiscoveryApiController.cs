using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Net;
using IO.Swagger.Models;
using Refit;
using UpdateHub.Domain;

namespace UpdateHub.Endpoints;

/// <summary>
///
/// </summary>
[Route("lookup/")]
[ApiController]
public class DiscoveryApiController : ControllerBase
{
  private ILogger logger;

  private IHttpClientFactory httpClientFactory;
  private AasServerRepository aasServerRepository;

  public DiscoveryApiController(ILogger<ShellRepositoryApiController> logger, IHttpClientFactory httpClientFactory, AasServerRepository aasServerRepository)
  {
    this.logger = logger;
    this.httpClientFactory = httpClientFactory;
    this.aasServerRepository = aasServerRepository;
  }

  /// <summary>
  /// Returns a list of Asset Administration Shell ids linked to specific Asset identifiers
  /// </summary>
  /// <param name="assetIds">A list of specific Asset identifiers. Each Asset identifier is a base64-url-encoded [SpecificAssetId](https://api.swaggerhub.com/domains/Plattform_i40/Part1-MetaModel-Schemas/V3.0.1#/components/schemas/SpecificAssetId)</param>
  /// <param name="limit">The maximum number of elements in the response array</param>
  /// <param name="cursor">A server-generated identifier retrieved from pagingMetadata that specifies from which position the result listing should continue</param>
  /// <response code="200">Requested Asset Administration Shell ids</response>
  /// <response code="0">Default error handling for unmentioned status codes</response>
  [HttpGet]
  [Route("shells")]
  [SwaggerOperation("GetAllAssetAdministrationShellIdsByAssetLink")]
  [SwaggerResponse(statusCode: 200, type: typeof(string[]),
    description: "Requested Asset Administration Shell ids")]
  [SwaggerResponse(statusCode: 0, type: typeof(Result),
    description: "Default error handling for unmentioned status codes")]
  public virtual IActionResult GetAllAssetAdministrationShellIdsByAssetLink([FromQuery] List<string> assetIds,
    [FromQuery] int? limit = null, [FromQuery] string? cursor = null)
  {
    var assetIdEncoded = assetIds[0];
    string assetID = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(assetIdEncoded));

    var aasServer = aasServerRepository.GetByIdLink(assetID);
    if (aasServer == null)
    {
      return this.NotFound("No AAS Server for given IDLink found");
    }

    var httpClient = httpClientFactory.CreateClient();

    // TODO: use proper caching, instead of doing it for every curl.
    // May, https://github.com/TurnerSoftware/CacheTower is an option
    if (aasServer.Auth != null)
    {
      if (!aasServer.Auth.Authenticate(httpClient))
      {
        return this.Unauthorized("Error while executing authentication");
      }
    }
    httpClient.BaseAddress = new Uri(aasServer.Url);
    var _restApiService = RestService.For<IAasApi>(httpClient);

    // Get shell ids for asset id
    var shellIds = _restApiService.LookupShellsByAssetId(assetIdEncoded).Result;

    if ((shellIds.StatusCode == HttpStatusCode.Unauthorized) ||  (shellIds.ContentHeaders.ContentType.MediaType == "text/html"))
      return this.Unauthorized("Error while access AAS server");

    if (!shellIds.IsSuccessful)
    {
      return this.UnprocessableEntity("Error while fetching IdLink from AAS server");
    }

    if (shellIds.Content.Count == 0)
    {
      return this.NotFound("No shells found for given asset id");
    }

    return new ObjectResult(shellIds.Content);
  }
}
