using System.Diagnostics.Metrics;
using System.Net;
using Serilog;
using UpdateHub.Clients;

namespace UpdateHub.Service;

public interface IInventoryService{
    public string GetIdLinkFromAsset(string assetId);
}

public class InventoryService : IInventoryService {
    private readonly IInventoryApi? _inventoryApi;

    private static readonly System.Diagnostics.Metrics.Meter meter = new Meter("IndustrialAssetHub", "1.0.0");
    private static readonly Counter<int> CounterAssetIdentifier = meter.CreateCounter<int>("asset_identifier", "counter", "Counts the number of found asset_identifiers");
    private static readonly Counter<int> CounterAssetIdentifierNotFound = meter.CreateCounter<int>("asset_identifier_not_found", "counter","Counts the number not found asset_identifiers");
    private static readonly Counter<int> CounterIdLinkNotFound = meter.CreateCounter<int>("idlink_not_found", "counter","Counts the number for which no IdLink was found");
    private static readonly Counter<int> CounterIdLink = meter.CreateCounter<int>("idlink_as_identifier","counter", "Counts the number of asset_identifiers IdLink fields");
    private static readonly Counter<int> CounterIdLinkLegacy = meter.CreateCounter<int>("idlink_as_product_id", "counter","Counts the number of product_instance_identifier IdLink fields");

    public InventoryService(IInventoryApi inventoryApi)
    {
        _inventoryApi = inventoryApi;
    }

    public string? extactIdLink(IInventoryApi.IahAsset asset)
    {
      // First search for AssetIdentifiers
      if (asset.AssetIdentifiers!=null) {
        CounterAssetIdentifier.Add(1);
        foreach (var l in asset?.AssetIdentifiers)
        {
          if (l.AssetIdentifierType.ToUpper() == "IdLink".ToUpper())
          {
              Log.Debug("Use asset_identifiers.{id_link}");
              CounterIdLink.Add(1);
              return l.IdLink;
          }
        }
      }
      CounterAssetIdentifierNotFound.Add(1);


      // Fallback ...
      if (asset.ProductInstanceIdentifier?.ManufacturerProduct?.id != null)
      {
        Log.Debug("Use product_instance_identifiers.manufacturer_product.id");
        CounterIdLinkLegacy.Add(1);
        return asset.ProductInstanceIdentifier.ManufacturerProduct.id;
      }

      CounterIdLinkNotFound.Add(1);
      return null;
    }

    public string GetIdLinkFromAsset(string assetId)
    {
        try
        {
            var Asset = _inventoryApi.GetByAssetId(assetId).Result;
            if (Asset.StatusCode == HttpStatusCode.Unauthorized)
                throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while access Inventory server");
            if (Asset.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
                throw new HttpProblemResponseException(StatusCodes.Status404NotFound, "AssetId not found");
            if (!Asset.IsSuccessful)
                throw new HttpProblemResponseException(StatusCodes.Status422UnprocessableEntity, "Inventory call was not successful");
            if (Asset.Content == null )
                throw new HttpProblemResponseException(StatusCodes.Status422UnprocessableEntity, "Content empty for given AssetId");

            //var idLink = RawAssetJson.Content?["product_instance_identifier"]?["manufacturer_product"]?["id"]?.ToString() ?? null;
            var idLink = extactIdLink(Asset.Content);
            if(String.IsNullOrEmpty(idLink))
              throw new HttpProblemResponseException(StatusCodes.Status422UnprocessableEntity, "IdLink empty");

            return idLink;
        }
        catch (Exception ex)
        {
            Log.Error($"Exception occurred: {ex.Message}");
            throw;
        }
    }

}
