namespace UpdateHub.Domain;

using Refit;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using UpdateHub;

public interface IIrsApi
{
  [Get("/submodels/{assetId}?semantic_id={semanticId}")]
  [Headers("Content-Type: application/json")]
  Task<ApiResponse<List<JsonNode>>> GetSubmodelsFromIdLink(string assetId, string semanticId);
}
