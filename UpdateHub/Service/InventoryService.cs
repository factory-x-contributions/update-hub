using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Serilog;
using System.Reflection.Metadata;
using Amazon.Runtime;
using UpdateHub.Models;
using Refit;
using UpdateHub.Domain;
using UpdateHub.Endpoints;
using UpdateHub.Clients;

namespace UpdateHub.Service;

public interface IInventoryService{
    public string GetIdLinkFromAsset(string assetId);
}

public class InventoryService : IInventoryService {
    private readonly IInventoryApi? _inventoryApi;

    public InventoryService(IInventoryApi inventoryApi)
    {
        _inventoryApi = inventoryApi;
    }
    
    public string GetIdLinkFromAsset(string assetId)
    {
        try
        {
            var RawAssetJson = _inventoryApi.GetByAssetId(assetId).Result;

            if (RawAssetJson.StatusCode == HttpStatusCode.Unauthorized)
                throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while access Inventory server");
            if (RawAssetJson.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
                throw new HttpProblemResponseException(StatusCodes.Status404NotFound, "AssetId not found");
            if (!RawAssetJson.IsSuccessful)
                throw new HttpProblemResponseException(StatusCodes.Status422UnprocessableEntity, "Inventory call was not succesful");
            if (RawAssetJson.Content == null )
                throw new HttpProblemResponseException(StatusCodes.Status422UnprocessableEntity, "Content empty for given AssetId");
            
            var idLink = RawAssetJson.Content?["product_instance_identifier"]?["manufacturer_product"]?["id"]?.ToString() ?? null;
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