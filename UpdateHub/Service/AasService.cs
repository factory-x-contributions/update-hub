using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Refit;
using Serilog;
using UpdateHub.Domain;
using UpdateHub.Models;

namespace UpdateHub.Service;


public interface IAasService
{
    enum AasVersion
    {
        Basyx,
        v30,
    }

    public List<UpdateInformation> GetSoftwareUpdate(string idLink, HttpRequest request);
    public List<HandoverDocumentation> GetHandoverDocumentation(string idLink, HttpRequest request);
}


public partial class AasService : IAasService
{
    private readonly AasServerRepository? _repository;
    private readonly IHttpClientFactory? _httpClientFactory;

    private static readonly System.Diagnostics.Metrics.Meter meter = new Meter("AASBroker",
      "1.0.0");
    private static readonly Counter<int> AasFound = meter.CreateCounter<int>("aas_found", "counter", "Counts the number of found AAS shells");
    private static readonly Counter<int> AasNotFound = meter.CreateCounter<int>("aas_not_found", "counter", "Counts the number of not found AAS shells");

    public AasService(AasServerRepository aasServerRepository, IHttpClientFactory httpClientFactory)
    {
        _repository = aasServerRepository;
        _httpClientFactory = httpClientFactory;
    }

    private bool skipParseAas(HttpRequest request)
    {
        var flag = false;
        return bool.TryParse(request.Headers["SKIP_PARSE_AAS"].ToString().ToLower(), out flag);
    }

    // PCN - Software Update
    public List<UpdateInformation> GetSoftwareUpdate(string idLink, HttpRequest request)
    {
        var featureFlagSkipParseAAS = skipParseAas(request);

        var aasServer = _repository.GetByIdLink(idLink);
        if (aasServer == null)
            throw new HttpProblemResponseException(StatusCodes.Status404NotFound, "No AAS Server for given IDLink found");

        Log.Information("[{Method}] idLink: '{IdLink}' => AAS Server: {AasServer}",
            nameof(GetSoftwareUpdate), idLink, aasServer);

        switch (aasServer.Version)
        {
            case IAasService.AasVersion.Basyx:
                Log.Debug("Use Basyx flow");
                return getSoftwareUpdateBasyx(idLink, aasServer, featureFlagSkipParseAAS);
            case IAasService.AasVersion.v30:
                Log.Debug("Use v30 flow");
                return getSoftwareUpdateV3(idLink, aasServer, featureFlagSkipParseAAS);
        }

        throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Error while fetching software update");
    }

    private List<UpdateInformation> getSoftwareUpdateBasyx(string idLink, AasServer aasServer, bool featureFlagSkipParseAAS)
    {
        var httpClient = _httpClientFactory.CreateClient();
        if (aasServer.Auth != null)
            if (!aasServer.Auth.Authenticate(httpClient, _httpClientFactory))
                throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while executing authentication");

        httpClient.BaseAddress = new Uri(aasServer.Url);
        var _restApiService = RestService.For<IAasApi>(httpClient);
        try
        {
            //
            // Get assetID from IdLink
            //
            // @TODO: Detect a missing authorization. Currently, the AAS redirects ...
            var shellIds = _restApiService.LookupShellsByAssetId(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(idLink))).Result;

            // Check if endpoint is authorized
            // @TODO: Detect a missing authorization. Currently, the AAS redirects to an IDM, and response with a certain media
            // type. But, why does the AAS server does not issue a proper HTTP status code?
            // Sick workaround with check for MediaType "text/html"
            if (shellIds.StatusCode == HttpStatusCode.Unauthorized ||
                shellIds.ContentHeaders.ContentType.MediaType == "text/html")
                throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while access AAS server");

            if (!shellIds.IsSuccessful)
                throw new HttpProblemResponseException(StatusCodes.Status422UnprocessableEntity, "Error while fetching IdLink from AAS server");

            if (shellIds.Content.Count == 0)
            {
                AasNotFound.Add(1, new KeyValuePair<string, object>("IdLink", idLink.ToString()));
                throw new HttpProblemResponseException(StatusCodes.Status404NotFound, "No shells found for given IdLink");
            }

            Dictionary<string, JsonNode> receivedPcns = new();
            Dictionary<string, JsonNode> receivedSoftwareNameplates = new();

            //
            // Shell Descriptors
            //
            foreach (var shellId in shellIds.Content)
            {
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
            }

            if (!featureFlagSkipParseAAS)
                return PcnParser.parsePcnAndSoftwareNameplateSubmodels(receivedPcns.Values.ToList(),
                  receivedSoftwareNameplates.Values.ToList());

            // Fallback, since the AAS libary does not work on arm64
            JsonObject pcnJsonObject = null;
            JsonObject softwareNameplateJsonObject = null;
            foreach (var l in receivedSoftwareNameplates)
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

            AasFound.Add(1, new KeyValuePair<string, object>("IdLink", idLink.ToString()));
            return updates;
        }
        catch (HttpProblemResponseException e)
        {
            throw;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw new Exception("Unknown exception" + e);
        }
    }

    // Handover Documentation
    public List<HandoverDocumentation> GetHandoverDocumentation(string idLink, HttpRequest request)
    {
        var featureFlagSkipParseAAS = skipParseAas(request);

        var aasServer = _repository.GetByIdLink(idLink);

        if (aasServer == null)
            throw new HttpProblemResponseException(StatusCodes.Status404NotFound, "No AAS Server for given IDLink found");
        Log.Information("[{Method}] idLink: '{IdLink}' => AAS Server: {AasServer}",
            nameof(GetHandoverDocumentation), idLink, aasServer);

        switch (aasServer.Version)
        {
            case IAasService.AasVersion.Basyx:
                Log.Debug("Use Basyx flow");
                return getHandoverDocumentationBasyx(idLink, aasServer, featureFlagSkipParseAAS, aasServer.Url);
            case IAasService.AasVersion.v30:
                Log.Debug("Use v30 flow");
                return getHandoverDocumentationV3(idLink, aasServer, featureFlagSkipParseAAS, aasServer.Url);
        }

        throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Error while fetching software update");
    }

    private List<HandoverDocumentation> getHandoverDocumentationBasyx(string idLink, AasServer aasServer, bool featureFlagSkipParseAAS, string baseUrl)
    {
        var httpClient = _httpClientFactory.CreateClient();
        if (aasServer.Auth != null)
            if (!aasServer.Auth.Authenticate(httpClient, _httpClientFactory))
                throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while executing authentication");

        httpClient.BaseAddress = new Uri(aasServer.Url);
        var _restApiService = RestService.For<IAasApi>(httpClient);
        try
        {
            //
            // Get assetID from IdLink
            //
            // @TODO: Detect a missing authorization. Currently, the AAS redirects ...
            var shellIds = _restApiService.LookupShellsByAssetId(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(idLink))).Result;

            // Check if endpoint is authorized
            // @TODO: Detect a missing authorization. Currently, the AAS redirects to an IDM, and response with a certain media
            // type. But, why does the AAS server does not issue a proper HTTP status code?
            // Sick workaround with check for MediaType "text/html"
            if (shellIds.StatusCode == HttpStatusCode.Unauthorized ||
                shellIds.ContentHeaders.ContentType.MediaType == "text/html")
                throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while access AAS server");

            if (!shellIds.IsSuccessful)
                throw new HttpProblemResponseException(StatusCodes.Status422UnprocessableEntity, "Error while fetching IdLink from AAS server");

            if (shellIds.Content.Count == 0)
            {
                AasNotFound.Add(1, new KeyValuePair<string, object>("IdLink", idLink.ToString()));
                throw new HttpProblemResponseException(StatusCodes.Status404NotFound, "No shells found for given IdLink");
            }

            // FIXME: use HandoverDocument Model fields

            Dictionary<string, JsonNode> receivedHoDs = new();
            // Dictionary<string, byte[]> receivedHoDs = new();
            // byte[] file = Array.Empty<byte>();
            //
            // Shell Descriptors
            var aasIdentifier = ""; // FIXME: assuming, that there is only one shellId that is used, i.e: for loop is unnecessary (would only occur if there are more than one shell with the same identifier)
            foreach (var shellId in shellIds.Content)
            {
                Log.Information($"Shell Id: {shellId}");
                aasIdentifier = shellId;
                var shellIdEncoded = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(shellId));
                var response = _restApiService.GetShellDescriptors(shellIdEncoded).Result;
                if (!response.IsSuccessful)
                    throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Error while fetching Shell Descriptors from AAS server");


                foreach (var d in response.Content.SubmodelDescriptors)
                {
                    // FIXME: Use SemanticId
                    if (d.idShort.Contains("HandoverDocumentation")) // which version of the Handover Documentation is used?
                    {
                        var hod = _restApiService.GetSubmodelsFromShell(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(shellId)),
                            Base64Url.EncodeToString(Encoding.UTF8.GetBytes(d.id)))
                          .Result;
                        if (!hod.IsSuccessful)
                            throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Error while fetching Handover Documentation from AAS server");

                        receivedHoDs[hod.Content["id"].AsValue().ToString()] = hod.Content;
                    }
                }
            }

            if (!featureFlagSkipParseAAS)
                return HoDParser.parseHandoverDocumentationSubmodels(receivedHoDs.Values.ToList(), baseUrl, aasIdentifier);

            // maybe needs array of byte[] arrays to provide all pdfs blobs
            JsonObject hodJsonObject = null;
            foreach (var l in receivedHoDs)
            {
                hodJsonObject = l.Value.AsObject(); // FIXME: needs  to handle blobs?
            }
            var handoverDocs = new List<HandoverDocumentation>();
            var handover = new HandoverDocumentation("", "", "", "", "", "", "", "", "", "", "", "", [], hodJsonObject);
            handoverDocs.Add(handover);

            AasFound.Add(1, new KeyValuePair<string, object>("IdLink", idLink.ToString()));
            return handoverDocs;
        }
        catch (HttpProblemResponseException e)
        {
            throw;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw new Exception("Unknown exception" + e);
        }
    }
}
