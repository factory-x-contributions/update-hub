using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using IO.Swagger.Models;
using System.ComponentModel.DataAnnotations;
using Refit;
using UpdateHub.Domain;
using AasJsonization = AasCore.Aas3_0.Jsonization;
using AasCore.Aas3_0;
using Swashbuckle.AspNetCore.Annotations;

namespace UpdateHub.Endpoints
{
  /// <summary>
  /// Controller providing some of the GET entpoints of 'Asset Administration Shell Repository API'
  /// (https://app.swaggerhub.com/apis/Plattform_i40/AssetAdministrationShellRepositoryServiceSpecification/V3.0.3_SSP-002).
  /// </summary>

  [Route("shells/")]
  [ApiController]
  public class ShellRepositoryController : ControllerBase
  {
    private ILogger logger;

    private IHttpClientFactory httpClientFactory;
    private AasServerRepository aasServerRepository;

    public ShellRepositoryController(ILogger<ShellRepositoryController> logger, IHttpClientFactory httpClientFactory, AasServerRepository aasServerRepository)
    {
      this.logger = logger;
      this.httpClientFactory = httpClientFactory;
      this.aasServerRepository = aasServerRepository;
    }

    [HttpGet]
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
          if (!aasServer.Auth.Authenticate(httpClient))
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
    /// Returns a specific Asset Administration Shell
    /// </summary>
    /// <param name="aasIdentifier">The Asset Administration Shell’s unique id (UTF8-BASE64-URL-encoded)</param>
    /// <response code="200">Requested Asset Administration Shell</response>
    /// <response code="400">Bad Request, e.g. the request parameters of the format of the request body is wrong.</response>
    /// <response code="401">Unauthorized, e.g. the server refused the authorization attempt.</response>
    /// <response code="403">Forbidden</response>
    /// <response code="404">Not Found</response>
    /// <response code="500">Internal Server Error</response>
    /// <response code="0">Default error handling for unmentioned status codes</response>
    //  [HttpGet]
    //  [Route("/{aasIdentifier}")]
    //  [SwaggerOperation("GetAssetAdministrationShellById")]
    //  [SwaggerResponse(statusCode: 200, type: typeof(AssetAdministrationShell), description: "Requested Asset Administration Shell")]
    //  [SwaggerResponse(statusCode: 400, type: typeof(Result), description: "Bad Request, e.g. the request parameters of the format of the request body is wrong.")]
    //  [SwaggerResponse(statusCode: 401, type: typeof(Result), description: "Unauthorized, e.g. the server refused the authorization attempt.")]
    //  [SwaggerResponse(statusCode: 403, type: typeof(Result), description: "Forbidden")]
    //  [SwaggerResponse(statusCode: 404, type: typeof(Result), description: "Not Found")]
    //  [SwaggerResponse(statusCode: 500, type: typeof(Result), description: "Internal Server Error")]
    //  [SwaggerResponse(statusCode: 0, type: typeof(Result), description: "Default error handling for unmentioned status codes")]
    //  public virtual IActionResult GetAssetAdministrationShellById([FromRoute][Required] string aasIdentifier)
    //  {

    //    //TODO: Uncomment the next line to return response 200 or use other options such as return this.NotFound(), return this.BadRequest(..), ...
    //    // return StatusCode(200, default(AssetAdministrationShell));

    //    //TODO: Uncomment the next line to return response 400 or use other options such as return this.NotFound(), return this.BadRequest(..), ...
    //    // return StatusCode(400, default(Result));

    //    //TODO: Uncomment the next line to return response 401 or use other options such as return this.NotFound(), return this.BadRequest(..), ...
    //    // return StatusCode(401, default(Result));

    //    //TODO: Uncomment the next line to return response 403 or use other options such as return this.NotFound(), return this.BadRequest(..), ...
    //    // return StatusCode(403, default(Result));

    //    //TODO: Uncomment the next line to return response 404 or use other options such as return this.NotFound(), return this.BadRequest(..), ...
    //    // return StatusCode(404, default(Result));

    //    //TODO: Uncomment the next line to return response 500 or use other options such as return this.NotFound(), return this.BadRequest(..), ...
    //    // return StatusCode(500, default(Result));

    //    //TODO: Uncomment the next line to return response 0 or use other options such as return this.NotFound(), return this.BadRequest(..), ...
    //    // return StatusCode(0, default(Result));
    //    string exampleJson = null;
    //    exampleJson = "\"\"";

    //    var example = exampleJson != null
    //    ? JsonConvert.DeserializeObject<AssetAdministrationShell>(exampleJson)
    //    : default(AssetAdministrationShell);            //TODO: Change the data returned
    //    return new ObjectResult(example);
    //  }
  }
}
