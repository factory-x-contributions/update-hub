// SPDX-FileCopyrightText: 2026 Fraunhofer-Institut für Produktionstechnik und Automatisierung IPA
// SPDX-FileCopyrightText: 2026 Hilscher Gesellschaft für Systemautomation mbH
// SPDX-FileCopyrightText: 2026 Siemens AG
//
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Text;
using SSIExtension;

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


  public class LookupShellsRequest
  {
    [AliasAs("name")]
    public string name { get; set; }
    [AliasAs("value")]
    public string value { get; set; }

    public LookupShellsRequest(string idLink)
    {
      this.name = "globalAssetId";
      this.value = idLink;
    }

    public  string ToString()
    {
      return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this)));
    }

  }

  //{
  //   "paging_metadata": {
  //     "cursor": "100"
  //   },
  //   "result": [
  //     "AssetId"
  //   ]
  // }
  //
  public class ShellDesciLookupShellsByAssetIdsResponse
  {
    [AliasAs("result")] public List<string>? Result { get; set; }
    [AliasAs("paging_metadata")] public JsonNode Paging { get; set; }
  }

  [Get("/lookup/shells?assetIds={idLink}")]
  [Headers("Content-Type: application/json")]
  Task<ApiResponse<ShellDesciLookupShellsByAssetIdsResponse>> LookupShellsByAssetIds(string idLink);

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


  public class GetShellResponse
  {
    [AliasAs("results")] public string id { get; set; }
  }
  [Get("/shells/{id}")]
  [Headers("Content-Type: application/json")]
  Task<ApiResponse<JsonNode>> GetShell(string id);

  [Get("/shells/{id}/submodels/{modelId}")]
  [Headers("Content-Type: application/json")]
  Task<ApiResponse<JsonNode>> GetSubmodelsFromShell(string id, string modelId);
}
