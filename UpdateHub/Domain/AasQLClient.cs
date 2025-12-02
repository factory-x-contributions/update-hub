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
    [Headers("Content-Type: application/json")]
    Task<ApiResponse<JsonNode>> QuerySubmodels([Body] JsonNode aasqlquery);
}

/// <summary>
/// AASql query request body
/// </summary>
public class AasQLQuery
{
    /// <summary>
    /// The AASql query statement
    /// </summary>
    [AliasAs("aasqlquery")]
    public string Query { get; set; }

    public AasQLQuery(string query)
    {
        Query = query;
    }
}

/// <summary>
/// AASql query response
/// </summary>
public class AasQLResponse
{
    /// <summary>
    /// Query execution status
    /// </summary>
    [AliasAs("status")]
    public string Status { get; set; }

    /// <summary>
    /// Query result data
    /// </summary>
    [AliasAs("result")]
    public JsonNode Result { get; set; }

    /// <summary>
    /// Number of rows returned
    /// </summary>
    [AliasAs("count")]
    public int Count { get; set; }

    /// <summary>
    /// Query execution time in milliseconds
    /// </summary>
    [AliasAs("execution_time")]
    public double? ExecutionTime { get; set; }

    /// <summary>
    /// Error message if query failed
    /// </summary>
    [AliasAs("error")]
    public string? Error { get; set; }
}



