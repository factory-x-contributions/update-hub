namespace UpdateHub.Domain;

using Refit;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using UpdateHub;

public interface IAasApi
{
  public class ShellIds
  {
    public List<string> Strings { get; set; }
  }

  [Get("/lookup/shells?assetId={idLink}")]
  [Headers("Content-Type: application/json")]
  Task<ApiResponse<List<string>>> LookupShellsByAssetId(string idLink);


  public class SubmodelDescriptors
  {
    [AliasAs("id")] public string id { get; set; }
    [AliasAs("idShort")] public string idShort { get; set; }
  }
  public class ShellDesciptors
  {
    [AliasAs("assetKind")] public string AssetKind { get; set; }
    [AliasAs("submodelDescriptors")] public List<SubmodelDescriptors> SubmodelDescriptors { get; set; }
  }
  [Get("/shell-descriptors/{id}")]
  [Headers("Content-Type: application/json")]
  Task<ApiResponse<ShellDesciptors>> GetShellDescriptors(string id);

  [Get("/shells")]
  [Headers("Content-Type: application/json")]
  Task<ApiResponse<JsonNode>> GetShells();

  [Get("/shells/{id}")]
  [Headers("Content-Type: application/json")]
  Task<ApiResponse<JsonNode>> GetShell(string id);

  [Get("/shells/{id}/submodels/{modelId}")]
  [Headers("Content-Type: application/json")]
  Task<ApiResponse<JsonNode>> GetSubmodelsFromShell(string id, string modelId);
}
