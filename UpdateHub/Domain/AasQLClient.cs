using System.Text.Json.Nodes;
using Refit;

namespace UpdateHub.Domain;

public interface IAasQLApi
{
    // /// <summary>
    // /// Execute AASql query with JSON body
    // /// </summary>
    // /// <param name="query">AASql query object containing the SQL statement</param>
    // /// <returns>Query result as JSON response</returns>
    // [Post("/query")]
    // [Headers("Content-Type: application/json")]
    // Task<ApiResponse<JsonNode>> ExecuteQuery([Body] AasQLQuery query);

    // /// <summary>
    // /// Execute AASql query for asset lookup
    // /// </summary>
    // /// <param name="query">AASql query for asset search</param>
    // /// <returns>List of matching asset IDs</returns>
    // [Post("/query/assets")]
    // [Headers("Content-Type: application/json")]
    // Task<ApiResponse<List<string>>> QueryAssets([Body] AasQLQuery query);

    /// <summary>
    /// Execute AASql query for submodel lookup
    /// </summary>
    /// <param name="aasqlquery">AASql query for submodel search</param>
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



