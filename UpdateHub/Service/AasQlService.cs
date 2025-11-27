using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Refit;
using Serilog;
using UpdateHub.Domain;
using UpdateHub.Models;
using UpdateHub.Endpoints;

namespace UpdateHub.Service;

public interface IAasQlService
{
    public List<UpdateInformation> GetSoftwareUpdate(AasQlQueryAttributes aasQLAttr, HttpRequest request);
    public List<HandoverDocumentation> GetHandoverDocumentation(AasQlQueryAttributes aasQLAttr, HttpRequest request);
}

public partial class AasQlService : IAasQlService
{
    private readonly AasServerRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;

     private static readonly System.Diagnostics.Metrics.Meter meter = new Meter("AASBroker",
      "1.0.0");
    private static readonly Counter<int> AasFound = meter.CreateCounter<int>("aas_found", "counter", "Counts the number of found AAS shells");
    private static readonly Counter<int> AasNotFound = meter.CreateCounter<int>("aas_not_found", "counter", "Counts the number of not found AAS shells");

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

            // Create AASql PCN query string using the provided attributes
            var aasqlPCNQuery = BuildAasqlPcnQuery(aasQLAttr);

            // Create AASql PCN query string using the provided attributes
            var aasqlSNPQuery = BuildAasqlSNPQuery(aasQLAttr);
            
            var httpClient = _httpClientFactory.CreateClient();
            if (aasServer.Auth != null)
                if (!aasServer.Auth.Authenticate(httpClient, _httpClientFactory))
                    throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while executing authentication");

            httpClient.BaseAddress = new Uri(aasServer.Url);
            var aasqlClient = RestService.For<IAasQLApi>(httpClient);

            // Execute AASql query
            var queryPCNResponse = aasqlClient.QuerySubmodels(new AasQLQuery(aasqlPCNQuery)).Result;
            var querySNPResponse = aasqlClient.QuerySubmodels(new AasQLQuery(aasqlSNPQuery)).Result;
            
            if (!queryPCNResponse.IsSuccessful)
                throw new HttpProblemResponseException(StatusCodes.Status422UnprocessableEntity, "Error while executing AASql PCN query");
           if (!querySNPResponse.IsSuccessful)
                throw new HttpProblemResponseException(StatusCodes.Status422UnprocessableEntity, "Error while executing AASql SNP query");
            
            Dictionary<string, JsonNode> receivedPcns = new();
            receivedPcns[queryPCNResponse.Content["id"].AsValue().ToString()] = queryPCNResponse.Content;

            Dictionary<string, JsonNode> receivedSnps = new();
            receivedSnps[querySNPResponse.Content["id"].AsValue().ToString()] = querySNPResponse.Content;
  
            if (!featureFlagSkipParseAAS)
                return PcnParser.parsePcnAndSoftwareNameplateSubmodels(receivedPcns.Values.ToList(),
                  receivedSnps.Values.ToList());

            // Fallback, since the AAS libary does not work on arm64
            JsonObject pcnJsonObject = null;
            JsonObject softwareNameplateJsonObject = null;
            foreach (var l in receivedSnps)
            {
                softwareNameplateJsonObject = l.Value.AsObject();
            }
            foreach (var l in receivedPcns)
            {
                pcnJsonObject = l.Value.AsObject();
            }
            var updates = new List<UpdateInformation>();
            var update = new UpdateInformation("", "", "", "", "", softwareNameplateJsonObject, pcnJsonObject);
            updates.Add(update);
            return updates;
        }
        catch (HttpProblemResponseException)
        {
            // QueryFailed.Add(1, new KeyValuePair<string, object?>("method", "GetSoftwareUpdate"));
            throw;
        }
        catch (Exception e)
        {
            // QueryFailed.Add(1, new KeyValuePair<string, object?>("method", "GetSoftwareUpdate"));
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
        // Build AASql query based on provided attributes
        var query = $@"{{
            ""Query"": {{
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

    private string BuildAasqlSNPQuery(AasQlQueryAttributes attributes)
    {
        // Build AASql query based on provided attributes
        var query = $@"{{
            ""Query"": {{
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
