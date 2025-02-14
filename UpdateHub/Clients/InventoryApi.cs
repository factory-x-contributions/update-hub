
using Refit;
using System.Text.Json.Nodes;

namespace UpdateHub.Clients;
public interface IInventoryApi
{
  
  [Get("/assets/{assetId}")]
  [Headers("Content-Type: application/json")]
  Task<ApiResponse<JsonNode>> GetByAssetId(string assetId);
}


