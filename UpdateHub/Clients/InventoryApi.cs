
using Refit;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace UpdateHub.Clients;

public interface IInventoryApi
{
  public class ManufacturerProduct
  {
    // Fields ProductVersion and Manufacturer are Optional
    [JsonPropertyName("id")] public string id { get; set;  }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("product_id")] public string ProducdtId { get; set; }

    [JsonPropertyName("product_version")] public string? ProductVersion { get; set; }
    [JsonPropertyName("manufacturer")] public JsonNode? Manufacturer { get; set; }
  }


  public class ProductInstanceIdentifier
  {
    [JsonPropertyName("serial_number")] public string SerialNumber { get; set; }
    [JsonPropertyName("manufacturer_product")] public ManufacturerProduct ManufacturerProduct { get; set; }
  }

  public class AssetIdentifier
  {
    [JsonPropertyName("asset_identifier_type")] public string AssetIdentifierType { get; set; }
    [JsonPropertyName("id_link")] public string IdLink { get; set; }
  }

  public class IahAsset
  {
    [JsonPropertyName("id")] public string  Id { get; set; }
    [JsonPropertyName("asset_identifiers")] public List<AssetIdentifier>?  AssetIdentifiers { get; set; }
    [JsonPropertyName("product_instance_identifier")] public ProductInstanceIdentifier?  ProductInstanceIdentifier { get; set; }
  }

  [Get("/v1-earlyaccess/assets/{assetId}")]
  [Headers("Content-Type: application/json")]
  Task<ApiResponse<IahAsset>> GetByAssetId(string assetId);
}
