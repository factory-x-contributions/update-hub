using System.Text.Json.Nodes;
using Refit;

namespace UpdateHub.Domain;

public interface IAasQLApi
{
   
    /// <summary>
    /// Execute AASql query with raw JSON string
    /// </summary>
    /// <param name="jsonBody">Raw JSON body as string</param>
    /// <returns>Query result with submodel data</returns>
    [Post("/query/submodels")]
    [Headers("Content-Type: application/json", "Accept: application/json", "User-Agent: UpdateHub-Client/1.0")]
    Task<ApiResponse<JsonNode>> QuerySubmodels([Body] string jsonBody);
}





