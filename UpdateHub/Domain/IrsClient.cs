namespace UpdateHub.Domain;

using Refit;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using UpdateHub;

public interface IIrsApiBase
{
    [Get("/assets/{assetId}/submodels?semantic_id={semanticId}")] // IRS API v2 : https://demo.codewerk.de/irs/api/v2/docs
    [Headers("Content-Type: application/json")]
    Task<ApiResponse<List<JsonNode>>> GetSubmodelsFromIdLink(string assetId, string semanticId);
}

public interface IIrsApi : IIrsApiBase
{
    [Get("/assets/{assetId}/submodels?semantic_id={semanticId}")] // IRS API v2 : https://demo.codewerk.de/irs/api/v2/docs
    [Headers("Content-Type: application/json")]
    new Task<ApiResponse<List<JsonNode>>> GetSubmodelsFromIdLink(string assetId, string semanticId);
}
public interface IIrsApiV1 : IIrsApiBase
{
    [Get("/submodels/{assetId}?semantic_id={semanticId}")] // IRS API v1 : https://demo.codewerk.de/irs/api/v1/docs
    [Headers("Content-Type: application/json")]
    new Task<ApiResponse<List<JsonNode>>> GetSubmodelsFromIdLink(string assetId, string semanticId);
}
