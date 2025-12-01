using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

            var httpClient = _httpClientFactory.CreateClient();

            if (aasServer.Auth != null)
            {
                if (!aasServer.Auth.Authenticate(httpClient, _httpClientFactory))
                    throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized,
                        "Error while executing authentication");
            }

            httpClient.BaseAddress = new Uri(aasServer.Url);

            // ---- PCN query ----
            const string path = "/query/submodels"; // AAS server endpoint

            var pcnContent = new StringContent(aasqlPCNQuery, Encoding.UTF8, "application/json");
            Log.Debug("AASQL PCN request body: {Body}", aasqlPCNQuery);

            var pcnResponse = httpClient.PostAsync(path, pcnContent).Result;
            var pcnResponseText = pcnResponse.Content.ReadAsStringAsync().Result;

            Log.Debug("AASQL PCN raw response: {Status} {Text}",
                pcnResponse.StatusCode, pcnResponseText);

            if (!pcnResponse.IsSuccessStatusCode)
            {
                throw new HttpProblemResponseException(
                    StatusCodes.Status422UnprocessableEntity,
                    $"Error while executing AASql PCN query: {pcnResponse.StatusCode} {pcnResponseText}"
                );
            }

            var pcnJson = JsonNode.Parse(pcnResponseText)!;

            // ---- SNP query ----
            var snpContent = new StringContent(aasqlSNPQuery, Encoding.UTF8, "application/json");
            Log.Debug("AASQL SNP request body: {Body}", aasqlSNPQuery);

            var snpResponse = httpClient.PostAsync(path, snpContent).Result;
            var snpResponseText = snpResponse.Content.ReadAsStringAsync().Result;

            Log.Debug("AASQL SNP raw response: {Status} {Text}",
                snpResponse.StatusCode, snpResponseText);

            if (!snpResponse.IsSuccessStatusCode)
            {
                throw new HttpProblemResponseException(
                    StatusCodes.Status422UnprocessableEntity,
                    $"Error while executing AASql SNP query: {snpResponse.StatusCode} {snpResponseText}"
                );
            }

            var snpJson = JsonNode.Parse(snpResponseText)!;

            // Use parse pipeline if enabled
            if (!featureFlagSkipParseAAS)
            {
                // PcnParser expects lists of submodel JsonNodes
                var pcnList = new List<JsonNode> { pcnJson };
                var snpList = new List<JsonNode> { snpJson };

                return PcnParser.parsePcnAndSoftwareNameplateSubmodels(pcnList, snpList);
            }

            // ---- Fallback (arm64 AAS library not available) ----
            var pcnJsonObject = pcnJson as JsonObject ?? new JsonObject();
            var softwareNameplateJsonObject = snpJson as JsonObject ?? new JsonObject();

            var updates = new List<UpdateInformation>
            {
                new(
                    "", "", "", "", "",
                    softwareNameplateJsonObject,
                    pcnJsonObject
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
            // TODO: implement similar query for handover documentation
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
        // You can refine this if SNP query should differ from PCN one
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

        if (results is JsonArray arr)
        {
            foreach (var item in arr)
            {
                var update = new UpdateInformation("", "", "", "", "", new JsonObject(), new JsonObject());
                updateList.Add(update);
            }
        }

        return updateList;
    }
}
