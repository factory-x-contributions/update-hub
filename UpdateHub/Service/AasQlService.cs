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

namespace UpdateHub.Service;

public interface IAasQlService
{
    List<UpdateInformation> GetSoftwareUpdate(AasQlQueryAttributes aasQLAttr, HttpRequest request);
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

            // Build AASQL queries (JSON with top-level "Query")
            var aasqlPCNQuery = BuildAasqlPcnQuery(aasQLAttr);
            var aasqlSNPQuery = BuildAasqlSNPQuery(aasQLAttr);
            Log.Debug("AASQL PCN request body: {Body}", aasqlPCNQuery);
            Log.Debug("AASQL SNP request body: {Body}", aasqlSNPQuery);

            // Parse them into JsonNode so Refit sends a proper JSON object
            var pcnNode = JsonNode.Parse(aasqlPCNQuery)!;
            var snpNode = JsonNode.Parse(aasqlSNPQuery)!;

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

            //Execute AASql query
            var queryPCNResponse = aasqlClient.QuerySubmodels(pcnNode).Result;
            Log.Debug("AASQL PCN raw response: {Status} {Text}",
                queryPCNResponse.StatusCode,
                queryPCNResponse.Content?.ToJsonString());

            if (!queryPCNResponse.IsSuccessStatusCode || queryPCNResponse.Content is null)
            {
                throw new HttpProblemResponseException(
                    StatusCodes.Status422UnprocessableEntity,
                    $"Error while executing AASql PCN query: {queryPCNResponse.StatusCode} {queryPCNResponse.Error?.Content}");
            }

            // ---------- SNP ----------
            var querySNPResponse = aasqlClient.QuerySubmodels(snpNode).Result;
            Log.Debug("AASQL SNP raw response: {Status} {Text}",
                querySNPResponse.StatusCode,
                querySNPResponse.Content?.ToJsonString());

            if (!querySNPResponse.IsSuccessStatusCode || querySNPResponse.Content is null)
            {
                throw new HttpProblemResponseException(
                    StatusCodes.Status422UnprocessableEntity,
                    $"Error while executing AASql SNP query: {querySNPResponse.StatusCode} {querySNPResponse.Error?.Content}");
            }

            var pcnJson = queryPCNResponse.Content;
            var snpJson = querySNPResponse.Content;
            List<UpdateInformation>? parsedUpdates = null;

            if (!featureFlagSkipParseAAS)
            {
                try
                {
                    var pcnSubmodels = ExtractSubmodelsFromResult(pcnJson);
                    var snpSubmodels = ExtractSubmodelsFromResult(snpJson);

                    parsedUpdates = PcnParser.parsePcnAndSoftwareNameplateSubmodels(
                        pcnSubmodels,
                        snpSubmodels
                    );
                }
                // aaspe-common missing => fall back instead of 500
                catch (FileNotFoundException ex) when (
                    (ex.FileName ?? string.Empty)
                        .Contains("aaspe-common", StringComparison.OrdinalIgnoreCase)
                )
                {
                    Log.Warning(ex,
                        "aaspe-common not found. Skipping AAS parsing and falling back to raw JSON result.");
                }
            }

            if (parsedUpdates != null)
            {
                return parsedUpdates;
            }

            // ---------- Fallback: use raw JSON submodels ----------
            var firstPcn = ExtractSubmodelsFromResult(pcnJson).FirstOrDefault() as JsonObject
                           ?? new JsonObject();
            var firstSnp = ExtractSubmodelsFromResult(snpJson).FirstOrDefault() as JsonObject
                           ?? new JsonObject();

            var updates = new List<UpdateInformation>
            {
                new(
                    "", "", "", "", "",
                    firstSnp,
                    firstPcn
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

    private string BuildAasqlPcnQuery(AasQlQueryAttributes attributes)
    {
        // The Query returns the AAS Global ID and other information based on submodel attributes
        var query = $@"{{
  ""Query"": {{
    ""$select"": ""id"",
    ""$condition"": {{
      ""$and"": [
        {{
          ""$eq"": [
            {{ ""$field"": ""$sm#idShort"" }},
            {{ ""$strVal"": ""ProductChangeNotifications"" }}
          ]
        }},
        {{
          ""$or"": [
            {{
              ""$contains"": [
                {{ ""$field"": ""$sme.ManufacturerName#value"" }},
                {{ ""$strVal"": ""{attributes.ManufacturerName}"" }}
              ]
            }},
            {{
              ""$contains"": [
                {{ ""$field"": ""$sme.OrderCodeOfManufacturer#value"" }},
                {{ ""$strVal"": ""{attributes.OrderCodeOfManufacturer}"" }}
              ]
            }},
            {{
              ""$contains"": [
                {{ ""$field"": ""$sme.ManufacturerProductType#value"" }},
                {{ ""$strVal"": ""{attributes.ManufacturerProductType}"" }}
              ]
            }}
          ]
        }}
      ]
    }}
  }}
}}";

        return query;
    }

    private string BuildAasqlSNPQuery(AasQlQueryAttributes attributes)
    {
        // The Query returns the AAS Global ID and other information based on Software Nameplate submodel attributes
        var query = $@"{{
  ""Query"": {{
    ""$select"": ""id"",
    ""$condition"": {{
      ""$and"": [
        {{
          ""$eq"": [
            {{ ""$field"": ""$sm#idShort"" }},
            {{ ""$strVal"": ""Nameplate"" }}
          ]
        }},
        {{
          ""$or"": [
            {{
              ""$contains"": [
                {{ ""$field"": ""$sme.ManufacturerName#value"" }},
                {{ ""$strVal"": ""{attributes.ManufacturerName}"" }}
              ]
            }},
            {{
              ""$contains"": [
                {{ ""$field"": ""$sme.OrderCodeOfManufacturer#value"" }},
                {{ ""$strVal"": ""{attributes.OrderCodeOfManufacturer}"" }}
              ]
            }},
            {{
              ""$contains"": [
                {{ ""$field"": ""$sme.ManufacturerProductType#value"" }},
                {{ ""$strVal"": ""{attributes.ManufacturerProductType}"" }}
              ]
            }}
          ]
        }}
      ]
    }}
  }}
}}";

        return query;
    }


    /// Helper to extract the "result" array (submodels) from the AASQL response.
    private static List<JsonNode> ExtractSubmodelsFromResult(JsonNode root)
    {
        if (root?["result"] is JsonArray arr)
        {
            return arr.ToList();
        }

        return new List<JsonNode>();
    }

    private List<UpdateInformation> ParseQueryResults(JsonNode results)
    {
        var updateList = new List<UpdateInformation>();
        
        // Parse the JSON results and convert to UpdateInformation objects
        // This is a placeholder implementation
        if (results?.AsArray() != null)
        {
            foreach (var item in results.AsArray())
            {
                // Parse individual result items
                // For now, create empty UpdateInformation objects
                var update = new UpdateInformation("", "", "", "", "", new JsonObject(), new JsonObject());
                updateList.Add(update);
            }
        }
        
        return updateList;
    }
}
