using System.Buffers.Text;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using IO.Swagger.Models;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json.Nodes;
using Refit;
using UpdateHub.Domain;
using AasJsonization = AasCore.Aas3_0.Jsonization;
using AasCore.Aas3_0;
using Serilog;
using Swashbuckle.AspNetCore.Annotations;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace UpdateHub.Endpoints
{
  /// <summary>
  /// Controller providing some of the GET entpoints of 'Asset Administration Shell Repository API'
  /// (https://app.swaggerhub.com/apis/Plattform_i40/AssetAdministrationShellRepositoryServiceSpecification/V3.0.3_SSP-002).
  /// </summary>

  [Route("shells/")]
  [ApiController]
  public class ShellRepositoryApiController : ControllerBase
  {
    private ILogger logger;

    private IHttpClientFactory httpClientFactory;
    private AasServerRepository aasServerRepository;

    public ShellRepositoryApiController(ILogger<ShellRepositoryApiController> logger, IHttpClientFactory httpClientFactory, AasServerRepository aasServerRepository)
    {
      this.logger = logger;
      this.httpClientFactory = httpClientFactory;
      this.aasServerRepository = aasServerRepository;
    }

    /// <summary>
    /// Returns all Asset Administration Shells
    /// </summary>
    /// <param name="assetIds">A list of specific Asset identifiers. Every single value asset identifier is a base64-url-encoded [SpecificAssetId](https://api.swaggerhub.com/domains/Plattform_i40/Part1-MetaModel-Schemas/V3.0.3#/components/schemas/SpecificAssetId).</param>
    /// <param name="idShort">The Asset Administration Shell’s IdShort</param>
    /// <param name="limit">The maximum number of elements in the response array</param>
    /// <param name="cursor">A server-generated identifier retrieved from pagingMetadata that specifies from which position the result listing should continue</param>
    /// <response code="200">Requested Asset Administration Shells</response>
    /// <response code="400">Bad Request, e.g. the request parameters of the format of the request body is wrong.</response>
    /// <response code="401">Unauthorized, e.g. the server refused the authorization attempt.</response>
    /// <response code="403">Forbidden</response>
    /// <response code="500">Internal Server Error</response>
    /// <response code="0">Default error handling for unmentioned status codes</response>
    [HttpGet]
    [Route("")]
    [SwaggerOperation("GetAllAssetAdministrationShells")]
    [SwaggerResponse(statusCode: 200, type: typeof(GetAssetAdministrationShellsResult), description: "Requested Asset Administration Shells")]
    [SwaggerResponse(statusCode: 400, type: typeof(Result), description: "Bad Request, e.g. the request parameters of the format of the request body is wrong.")]
    [SwaggerResponse(statusCode: 401, type: typeof(Result), description: "Unauthorized, e.g. the server refused the authorization attempt.")]
    [SwaggerResponse(statusCode: 403, type: typeof(Result), description: "Forbidden")]
    [SwaggerResponse(statusCode: 500, type: typeof(Result), description: "Internal Server Error")]
    [SwaggerResponse(statusCode: 0, type: typeof(Result), description: "Default error handling for unmentioned status codes")]
    public virtual IActionResult GetAllAssetAdministrationShells([FromQuery] List<string> assetIds, [FromQuery] string idShort = "", [FromQuery] int? limit = null, [FromQuery] string cursor = "")
    {
      var allAasList = new List<AssetAdministrationShell>();

      var aasServers = aasServerRepository.GetAll();

      foreach (var aasServer in aasServers)
      {
        var httpClient = httpClientFactory.CreateClient();
        if (aasServer.Auth != null)
        {
          if (!aasServer.Auth.Authenticate(httpClient, httpClientFactory))
          {
            logger.LogError($"Error while executing authentication for AAS server '{aasServer.Name}'");
            //return Unauthorized(new { message = "Error while executing authentication" });
          }
        }
        httpClient.BaseAddress = new Uri(aasServer.Url);
        var _restApiService = RestService.For<IAasApi>(httpClient);

        var response = _restApiService.GetShells().Result;

        if (!response.IsSuccessful)
        {
          return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Error while fetching Shell Descriptors from AAS server '{aasServer.Name}'" });
        }

        var responseObject = response.Content;

        var aasList = responseObject["result"].AsArray();
        foreach (var serializedAas in aasList)
        {
          var parsedAas = (AasJsonization.Deserialize.AssetAdministrationShellFrom(serializedAas));
          allAasList.Add(parsedAas);
        }
      }

      var result = new GetAssetAdministrationShellsResult
      {
        Result = allAasList
      };

      return new ObjectResult(result);
    }


    /// <summary>
    /// Returns all submodels of a specific Asset Administration Shell
    /// </summary>
    /// <param name="aasIdentifier">The Asset Administration Shell’s unique id (UTF8-BASE64-URL-encoded)</param>
    /// <response code="200">Requested Asset Administration Shell</response>
    /// <response code="400">Bad Request, e.g. the request parameters of the format of the request body is wrong.</response>
    /// <response code="401">Unauthorized, e.g. the server refused the authorization attempt.</response>
    /// <response code="403">Forbidden</response>
    /// <response code="404">Not Found</response>
    /// <response code="500">Internal Server Error</response>
    /// <response code="0">Default error handling for unmentioned status codes</response>
    [HttpGet]
    [Route("{aasIdentifier}/submodels")]
    [SwaggerOperation("GetAssetAdministrationShellById")]
    [SwaggerResponse(statusCode: 200, type: typeof(AssetAdministrationShell), description: "Requested Asset Administration Shell")]
    [SwaggerResponse(statusCode: 400, type: typeof(Result), description: "Bad Request, e.g. the request parameters of the format of the request body is wrong.")]
    [SwaggerResponse(statusCode: 401, type: typeof(Result), description: "Unauthorized, e.g. the server refused the authorization attempt.")]
    [SwaggerResponse(statusCode: 403, type: typeof(Result), description: "Forbidden")]
    [SwaggerResponse(statusCode: 404, type: typeof(Result), description: "Not Found")]
    [SwaggerResponse(statusCode: 500, type: typeof(Result), description: "Internal Server Error")]
    [SwaggerResponse(statusCode: 0, type: typeof(Result), description: "Default error handling for unmentioned status codes")]
    public virtual IActionResult GetAssetAdministrationShellById([FromRoute][Required] string aasIdentifier)
    {
      string aasIdUnencoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(aasIdentifier));

      var aasServer = aasServerRepository.GetByIdLink(aasIdUnencoded);
      if (aasServer == null)
      {
        aasServer = aasServerRepository.GetByAasEndpointPrefix(aasIdUnencoded);
        if (aasServer == null)
        {
          return this.NotFound("No AAS Server for given aas id found");
        }
      }

      var httpClient = httpClientFactory.CreateClient();

      // TODO: use proper caching, instead of doing it for every curl.
      // May, https://github.com/TurnerSoftware/CacheTower is an option
      if (aasServer.Auth != null)
      {
        if (!aasServer.Auth.Authenticate(httpClient, httpClientFactory))
        {
          return this.Unauthorized("Error while executing authentication");
        }
      }
      httpClient.BaseAddress = new Uri(aasServer.Url);
      var _restApiService = RestService.For<IAasApi>(httpClient);

      var response = _restApiService.GetShell(aasIdentifier).Result;

      if (!response.IsSuccessful)
      {
        return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Error while fetching shell '{aasIdUnencoded}' from AAS server '{aasServer.Name}'" });
      }

      var responseObject = response.Content;
      var parsedAas = (AasJsonization.Deserialize.AssetAdministrationShellFrom(responseObject));

      var submodels = new List<JsonNode>();
      foreach(var submodelRef in parsedAas.Submodels)
      {
        var submodelId = submodelRef.Keys[0].Value;

        var submodelGetResponse = _restApiService.GetSubmodelsFromShell(
          aasIdentifier,
          Base64Url.EncodeToString(Encoding.UTF8.GetBytes(submodelId))).Result;
        if (submodelGetResponse.IsSuccessful)
        {
          // var parsedSubmodel = (AasJsonization.Deserialize.SubmodelFrom(submodelGetResponse.Content));
          submodels.Add(submodelGetResponse.Content);
        }
        else
        {
          Log.Error($"Error while fetching submodel '{submodelId}' from AAS server '{aasServer.Name}'");
        }
      }

      return new ObjectResult(submodels);
    }
  }
}
