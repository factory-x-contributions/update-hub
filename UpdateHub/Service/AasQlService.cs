using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Refit;
using Serilog;
using UpdateHub.Domain;
using UpdateHub.Endpoints;
using UpdateHub.Models;
using System.Reflection;
using System.Buffers.Text;
using System.Text;

namespace UpdateHub.Service;

public interface IAasQlService
{
    List<UpdateInformation> GetSoftwareUpdateViaAssetIDQuery(AasQlQueryAttributes aasQLAttr, HttpRequest request);
    List<HandoverDocumentation> GetHandoverDocumentation(AasQlQueryAttributes aasQLAttr, HttpRequest request);
}

public partial class AasQlService : IAasQlService
{
    private readonly AasServerRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly Meter meter = new("AASBroker", "1.0.0");
    private static readonly Counter<int> AasFound =
        meter.CreateCounter<int>("aas_found", "counter", "Counts the number of found AAS shells");
    private static readonly Counter<int> AasNotFound =
        meter.CreateCounter<int>("aas_not_found", "counter", "Counts the number of not found AAS shells");

    public AasQlService(AasServerRepository aasServerRepository, IHttpClientFactory httpClientFactory)
    {
        _repository = aasServerRepository;
        _httpClientFactory = httpClientFactory;
    }

    private bool skipParseAas(HttpRequest request)
    {
        var flag = false;
        return bool.TryParse(request.Headers["SKIP_PARSE_AAS"].ToString().ToLower(), out flag);
    }


    public List<UpdateInformation> GetSoftwareUpdateViaAssetIDQuery(AasQlQueryAttributes aasQLAttr, HttpRequest request)
    { 
        try
        {      
            var featureFlagSkipParseAAS = skipParseAas(request);

            // Simply take the first server in config.yaml which is the only one supporting AASQL
            var aasServer = _repository.GetFirstServerInList();
            if (aasServer == null)
                throw new HttpProblemResponseException(StatusCodes.Status404NotFound, "No AAS Server found");

            Log.Information("[{Method}] AasQLAttributes: {Attributes} => AAS Server: {AasServer}",
                nameof(GetSoftwareUpdate), aasQLAttr, aasServer.Name);

            // Build AASQL query that returns AAS shells (id + shell info)
            var aasqlShellQuery = BuildAasqlShellQuery(aasQLAttr);
            Log.Debug("AASQL Shell request body: {Body}", aasqlShellQuery);

            // Parse JSON so Refit sends an actual JSON object body
            var shellNode = JsonNode.Parse(aasqlShellQuery)!;

            var httpClient = _httpClientFactory.CreateClient();
            if (aasServer.Auth != null)
            {
                if (!aasServer.Auth.Authenticate(httpClient, _httpClientFactory))
                    throw new HttpProblemResponseException(
                        StatusCodes.Status401Unauthorized,
                        "Error while executing authentication");
            }

            httpClient.BaseAddress = new Uri(aasServer.Url);

            var aasqlClient = RestService.For<IAasQLApi>(httpClient);
            var queryShellResponse = aasqlClient.QueryShells(shellNode).Result;
            
            Log.Debug(
                "AASQL Shell raw response: {Status} {Text}",
                queryShellResponse.StatusCode,
                queryShellResponse.Content?.ToJsonString()
            );

            if (!queryShellResponse.IsSuccessStatusCode || queryShellResponse.Content is null)
            {
                throw new HttpProblemResponseException(
                    StatusCodes.Status422UnprocessableEntity,
                    $"Error while executing AASql shell query: {queryShellResponse.StatusCode} {queryShellResponse.Error?.Content}");
            }

            var resultJson = queryShellResponse.Content;
            var shellsJson = ExtractResultArray(resultJson);

            if (shellsJson.Count == 0)
            {
                AasNotFound.Add(1, new KeyValuePair<string, object?>("method", MethodBase.GetCurrentMethod()?.Name ?? "unknown"));
                // No matching asset – return empty list
                return new List<UpdateInformation>();
            }
            AasFound.Add(1, new KeyValuePair<string, object?>("method", MethodBase.GetCurrentMethod()?.Name ?? "unknown"));

            var firstShell = shellsJson[0] as JsonObject ?? new JsonObject();
            var shellId = firstShell["id"]?.GetValue<string>() ?? string.Empty;
            Log.Information("Resolved AAS shell id: {ShellId}", shellId);

            var _restApiService = RestService.For<IAasApi>(httpClient);
            Dictionary<string, JsonNode> receivedPcns = new();
            Dictionary<string, JsonNode> receivedSoftwareNameplates = new();

            // Legacy code to fetch submodels from AAS server

            var shellIdEncoded = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(shellId));
            var response = _restApiService.GetShellDescriptors(shellIdEncoded).Result;
            if (!response.IsSuccessful)
                throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Error while fetching Shell Descriptors from AAS server");


            foreach (var d in response.Content.SubmodelDescriptors)
            {
                if (d.idShort.Contains("ProductChangeNotifications"))
                {
                    var pcn = _restApiService.GetSubmodelsFromShell(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(shellId)),
                        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(d.id)))
                      .Result;
                    if (!pcn.IsSuccessful)
                        throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Error while fetching PCN from AAS server");

                    receivedPcns[pcn.Content["id"].AsValue().ToString()] = pcn.Content;
                }

                if (d.idShort.Contains("SoftwareNameplate"))
                {
                    var nameplate = _restApiService
                      .GetSubmodelsFromShell(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(shellId)),
                        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(d.id))).Result;
                    if (!nameplate.IsSuccessful)
                        throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Error while fetching Software Nameplate from AAS server");

                    receivedSoftwareNameplates[nameplate.Content["id"].AsValue().ToString()] = nameplate.Content;
                }
            }

            if (!featureFlagSkipParseAAS)
                return PcnParser.parsePcnAndSoftwareNameplateSubmodels(receivedPcns.Values.ToList(),
                  receivedSoftwareNameplates.Values.ToList());

            var updates = new List<UpdateInformation>
            {
                new(
                    shellId,           
                    "",                // date
                    "",                // version
                    "",                // installationUri
                    "",                // installationChecksum
                    firstShell,        // full shell JSON here (softwareNameplateSubmodel slot)
                    new JsonObject()   // empty PCN record (no PCN here)
                )
            };

            return updates;

        }
        catch (HttpProblemResponseException)
        {
            throw;
        }
        catch (Exception e)
        {
            Log.Error(e, "Error executing AASql query for software update");
            throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }


    public List<UpdateInformation> GetSoftwareUpdate(AasQlQueryAttributes aasQLAttr, HttpRequest request)
    {
        try
        {
            var featureFlagSkipParseAAS = skipParseAas(request);

            var aasServer = _repository.GetFirstServerInList();
            if (aasServer == null)
                throw new HttpProblemResponseException(StatusCodes.Status404NotFound, "No AAS Server found");

            Log.Information("[{Method}] AasQLAttributes: {Attributes} => AAS Server: {AasServer}",
                nameof(GetSoftwareUpdate), aasQLAttr, aasServer.Name);

            // Build AASQL query that returns AAS shells (id + shell info)
            var aasqlShellQuery = BuildAasqlShellQuery(aasQLAttr);
            Log.Debug("AASQL Shell request body: {Body}", aasqlShellQuery);

            // Parse JSON so Refit sends an actual JSON object body
            var shellNode = JsonNode.Parse(aasqlShellQuery)!;

            var httpClient = _httpClientFactory.CreateClient();
            if (aasServer.Auth != null)
            {
                if (!aasServer.Auth.Authenticate(httpClient, _httpClientFactory))
                    throw new HttpProblemResponseException(
                        StatusCodes.Status401Unauthorized,
                        "Error while executing authentication");
            }

            httpClient.BaseAddress = new Uri(aasServer.Url);
            
            // // Configure Refit with explicit JSON serialization settings
            // var refitSettings = new RefitSettings
            // {
            //     ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
            //     {
            //         PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            //         WriteIndented = false
            //     })
            // };
            //var aasqlClient = RestService.For<IAasQLApi>(httpClient, refitSettings);

            var aasqlClient = RestService.For<IAasQLApi>(httpClient);
            var queryShellResponse = aasqlClient.QueryShells(shellNode).Result;
            Log.Debug(
                "AASQL Shell raw response: {Status} {Text}",
                queryShellResponse.StatusCode,
                queryShellResponse.Content?.ToJsonString()
            );

            if (!queryShellResponse.IsSuccessStatusCode || queryShellResponse.Content is null)
            {
                throw new HttpProblemResponseException(
                    StatusCodes.Status422UnprocessableEntity,
                    $"Error while executing AASql shell query: {queryShellResponse.StatusCode} {queryShellResponse.Error?.Content}");
            }


            var shellJson = queryShellResponse.Content;
            var shells = ExtractResultArray(shellJson);
            if (shells.Count == 0)
            {
                AasNotFound.Add(1, new KeyValuePair<string, object?>("method", "GetSoftwareUpdate"));
                // No matching asset – return empty list
                return new List<UpdateInformation>();
            }
            AasFound.Add(1, new KeyValuePair<string, object?>("method", "GetSoftwareUpdate"));
            var firstShell = shells[0] as JsonObject ?? new JsonObject();
            var assetId = firstShell["id"]?.GetValue<string>() ?? string.Empty;
            Log.Information("Resolved AAS shell id: {AssetId}", assetId);

            var updates = new List<UpdateInformation>
            {
                new(
                    assetId,           
                    "",                // date
                    "",                // version
                    "",                // installationUri
                    "",                // installationChecksum
                    firstShell,        // full shell JSON here (softwareNameplateSubmodel slot)
                    new JsonObject()   // empty PCN record (no PCN here)
                )
            };

            return updates;

        }
        catch (HttpProblemResponseException)
        {
            throw;
        }
        catch (Exception e)
        {
            Log.Error(e, "Error executing AASql query for software update");
            throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    public List<HandoverDocumentation> GetHandoverDocumentation(AasQlQueryAttributes aasQLAttr, HttpRequest request)
    {
        try
        {
            // Similar implementation for handover documentation
            // For now, return empty list
            return new List<HandoverDocumentation>();
        }
        catch (Exception e)
        {
            Log.Error(e, "Error executing AASql query for handover documentation");
            throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    private string BuildAasqlShellQuery(AasQlQueryAttributes attributes)
    {
        // Returns the Asset ID based on the nameplate submodel attributes
        var query = $@"{{
  ""Query"": {{
    ""$select"": ""id"",
    ""$condition"": {{
      ""$and"": [
        {{
          ""$eq"": [
            {{ ""$field"": ""$sme.ManufacturerName#value"" }},
            {{ ""$strVal"": ""{attributes.ManufacturerName}"" }}
          ]
        }},
        {{
          ""$eq"": [
            {{ ""$field"": ""$sme.OrderCodeOfManufacturer#value"" }},
            {{ ""$strVal"": ""{attributes.OrderCodeOfManufacturer}"" }}
          ]
        }},
        {{
          ""$eq"": [
            {{ ""$field"": ""$sme.ManufacturerProductType#value"" }},
            {{ ""$strVal"": ""{attributes.ManufacturerProductType}"" }}
          ]
        }},
        {{
          ""$eq"": [
            {{ ""$field"": ""$sme.UriOfTheProduct#value"" }},
            {{ ""$strVal"": ""{attributes.UriOfTheProduct}"" }}
          ]
        }},
        {{
          ""$eq"": [
            {{ ""$field"": ""$sm#idShort"" }},
            {{ ""$strVal"": ""Nameplate"" }}
          ]
        }}
      ]
    }}
  }}
}}";

        return query;
    }


    /// Helper to extract the result array (shells) from the AASQL response.
    private static List<JsonNode> ExtractResultArray(JsonNode root)
    {
        if (root?["result"] is JsonArray arr)
        {
            // List<JsonNode?> → List<JsonNode>
            return arr.Where(n => n is not null).Cast<JsonNode>().ToList();
        }

        return new List<JsonNode>();
    }
}
