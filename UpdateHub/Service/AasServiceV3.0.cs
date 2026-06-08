// SPDX-FileCopyrightText: 2026 Fraunhofer-Institut für Produktionstechnik und Automatisierung IPA
// SPDX-FileCopyrightText: 2026 Hilscher Gesellschaft für Systemautomation mbH
// SPDX-FileCopyrightText: 2026 Siemens AG
//
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using Refit;
using Serilog;
using UpdateHub.Domain;
using UpdateHub.Models;

namespace UpdateHub.Service;

public partial class AasService
{
    /// <summary>
    /// Fetches a submodel, falling back to GET /submodels/{id} if GET /shells/{shellId}/submodels/{id} returns 404.
    /// </summary>
    private ApiResponse<JsonNode> GetSubmodelWithFallback(IAasApi restApiService, string shellIdEncoded, string submodelIdEncoded)
    {
        var shell = restApiService.GetSubmodelsFromShell(shellIdEncoded, submodelIdEncoded).Result;
        if (shell.StatusCode == HttpStatusCode.NotFound)
        {
            shell = restApiService.GetSubmodel(submodelIdEncoded).Result;
        }
        return shell;
    }

    private List<UpdateInformation> getSoftwareUpdateV3(string idLink, AasServer aasServer, bool featureFlagSkipParseAAS)
    {
        var httpClient = _httpClientFactory.CreateClient();
        if (aasServer.Auth != null)
            if (!aasServer.Auth.Authenticate(httpClient, _httpClientFactory))
                throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while executing authentication");

        httpClient.BaseAddress = new Uri(aasServer.Url.TrimEnd('/'));
        var _restApiService = RestService.For<IAasApi>(httpClient);

        // Temporary, till Discovery Endpoints differs from other ones...
        var httpClientDiscovery = _httpClientFactory.CreateClient();
        if (aasServer.Auth != null)
            if (!aasServer.Auth.Authenticate(httpClientDiscovery, _httpClientFactory))
                throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while executing authentication");

        // TODO: Flexible Discovery URL
        httpClientDiscovery.BaseAddress = new Uri(aasServer.DiscoveryUrl.TrimEnd('/'));
        var _restApiServiceDiscovery = RestService.For<IAasApi>(httpClientDiscovery);
        try
        {
            //
            // Get assetID from IdLink
            //
            // @TODO: Detect a missing authorization. Currently, the AAS redirects ...
            var shellIds = _restApiServiceDiscovery.LookupShellsByAssetIds(new IAasApi.LookupShellsRequest(idLink).ToString()).Result;
            //var shellIds = _restApiService.LookupShellsByAssetId(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(idLink))).Result;
            // Check if endpoint is authorized
            // @TODO: Detect a missing authorization. Currently, the AAS redirects to an IDM, and response with a certain media
            // type. But, why does the AAS server does not issue a proper HTTP status code?
            // Workaround with check for MediaType "text/html"
            if (shellIds.StatusCode == HttpStatusCode.Unauthorized ||
                shellIds.ContentHeaders.ContentType?.MediaType == "text/html")
                throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while access AAS server");

            if (shellIds.StatusCode == HttpStatusCode.NotFound)
            {
                AasNotFound.Add(1, new KeyValuePair<string, object>("IdLink", idLink.ToString()));
                throw new HttpProblemResponseException(StatusCodes.Status404NotFound, "No shells found for given IdLink");
            }

            if (!shellIds.IsSuccessful)
                throw new HttpProblemResponseException(StatusCodes.Status422UnprocessableEntity, "Error while fetching IdLink from AAS server");

            var shellIdsList = shellIds.Content?.Result ?? new List<string>();

            if (shellIdsList.Count == 0)
            {
                AasNotFound.Add(1, new KeyValuePair<string, object>("IdLink", idLink.ToString()));
                throw new HttpProblemResponseException(StatusCodes.Status404NotFound, "No shells found for given IdLink");
            }

            //
            // Shells
            //
            Dictionary<string, JsonNode> receivedPcns = new();
            Dictionary<string, JsonNode> receivedSoftwareNameplates = new();

            foreach (var shellId in shellIdsList)
            {
                var shellIdEncoded = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(shellId));
                var response = _restApiService.GetShell(shellIdEncoded).Result;
                if (!response.IsSuccessful)
                    throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Error while fetching Shells from AAS server");

                foreach (var r in response.Content?["submodels"].AsArray())
                {
                    foreach (var rr in r?["keys"].AsArray())
                    {
                        if (rr["type"].ToString().ToUpper().Equals("SUBMODEL"))
                        {
                            var id = rr?["value"].ToString();
                                                        var shell = GetSubmodelWithFallback(_restApiService, shellIdEncoded,
                                                            Base64Url.EncodeToString(Encoding.UTF8.GetBytes(id)));

                            if (!shell.IsSuccessful)
                                throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError,
                                  "Error while fetching Shell from AAS server");

                            if (shell.Content["idShort"].ToString().Contains("ProductChangeNotifications"))
                            {
                                var smId = shell.Content?["id"].ToString();

                                                                var pcn = GetSubmodelWithFallback(_restApiService,
                                  Base64Url.EncodeToString(Encoding.UTF8.GetBytes(shellId)),
                                                                    Base64Url.EncodeToString(Encoding.UTF8.GetBytes(smId)));
                                if (!pcn.IsSuccessful)
                                    throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Error while fetching Product Change Notification from AAS server");

                                receivedPcns[pcn.Content["id"].AsValue().ToString()] = pcn.Content;
                            }


                            if (shell.Content["idShort"].ToString().Contains("SoftwareNameplate"))
                            {
                                var smId = shell.Content?["id"].ToString();

                                                                var nameplate = GetSubmodelWithFallback(_restApiService,
                                  Base64Url.EncodeToString(Encoding.UTF8.GetBytes(shellId)),
                                                                    Base64Url.EncodeToString(Encoding.UTF8.GetBytes(smId)));
                                if (!nameplate.IsSuccessful)
                                    throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Error while fetching Software Nameplate from AAS server");

                                receivedSoftwareNameplates[nameplate.Content["id"].AsValue().ToString()] = nameplate.Content;
                            }
                        }
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
    // private List<HandoverDocumentation> getHandoverDocumentationBasyx(string idLink, AasServer aasServer, bool featureFlagSkipParseAAS)
    // {
    //     return getHandoverDocumentationV3(idLink, aasServer, featureFlagSkipParseAAS);
    // }
    private List<HandoverDocumentation> getHandoverDocumentationV3(string idLink, AasServer aasServer, bool featureFlagSkipParseAAS, string baseUrl)
    {
        var httpClient = _httpClientFactory.CreateClient();
        if (aasServer.Auth != null)
            if (!aasServer.Auth.Authenticate(httpClient, _httpClientFactory))
                throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while executing authentication");

        httpClient.BaseAddress = new Uri(aasServer.Url.TrimEnd('/'));
        var _restApiService = RestService.For<IAasApi>(httpClient);

        // Temporary, till Discovery Endpoints differs from other ones...
        var httpClientDiscovery = _httpClientFactory.CreateClient();
        if (aasServer.Auth != null)
            if (!aasServer.Auth.Authenticate(httpClientDiscovery, _httpClientFactory))
                throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while executing authentication");

        // TODO: Flexible Discovery URL
        httpClientDiscovery.BaseAddress = new Uri(aasServer.DiscoveryUrl.TrimEnd('/'));
        var _restApiServiceDiscovery = RestService.For<IAasApi>(httpClientDiscovery);
        try
        {
            //
            // Get assetID from IdLink
            //
            // @TODO: Detect a missing authorization. Currently, the AAS redirects ...
            var shellIds = _restApiServiceDiscovery.LookupShellsByAssetIds(new IAasApi.LookupShellsRequest(idLink).ToString()).Result;
            //var shellIds = _restApiService.LookupShellsByAssetId(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(idLink))).Result;
            // Check if endpoint is authorized
            // @TODO: Detect a missing authorization. Currently, the AAS redirects to an IDM, and response with a certain media
            // type. But, why does the AAS server does not issue a proper HTTP status code?
            // Workaround with check for MediaType "text/html"
            if (shellIds.StatusCode == HttpStatusCode.Unauthorized ||
                shellIds.ContentHeaders.ContentType?.MediaType == "text/html")
                throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while access AAS server");

            if (shellIds.StatusCode == HttpStatusCode.NotFound)
            {
                AasNotFound.Add(1, new KeyValuePair<string, object>("IdLink", idLink.ToString()));
                throw new HttpProblemResponseException(StatusCodes.Status404NotFound, "No shells found for given IdLink");
            }

            if (!shellIds.IsSuccessful)
                throw new HttpProblemResponseException(StatusCodes.Status422UnprocessableEntity, "Error while fetching IdLink from AAS server");

            var shellIdsList = shellIds.Content?.Result ?? new List<string>();

            if (shellIdsList.Count == 0)
            {
                AasNotFound.Add(1, new KeyValuePair<string, object>("IdLink", idLink.ToString()));
                throw new HttpProblemResponseException(StatusCodes.Status404NotFound, "No shells found for given IdLink");
            }

            //
            // Shells
            //
            Dictionary<string, JsonNode> receivedHoDs = new();

            var aasIdentifier = ""; // FIXME: assuming, that there is only one shellId that is used, i.e: for loop is unnecessary (would only occur if there are more than one shell with the same identifier)
            var shellIdEncoded = "";
            foreach (var shellId in shellIdsList)
            {
                aasIdentifier = shellId;
                shellIdEncoded = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(shellId));
                var response = _restApiService.GetShell(shellIdEncoded).Result;
                if (!response.IsSuccessful)
                    throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Error while fetching Shells from AAS server");

                foreach (var r in response.Content?["submodels"].AsArray())
                {
                    foreach (var rr in r?["keys"].AsArray())
                    {
                        if (rr["type"].ToString().ToUpper().Equals("SUBMODEL"))
                        {
                            var id = rr?["value"].ToString();
                                                        var shell = GetSubmodelWithFallback(_restApiService, shellIdEncoded,
                                                            Base64Url.EncodeToString(Encoding.UTF8.GetBytes(id)));

                            if (!shell.IsSuccessful)
                                throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError,
                                  "Error while fetching Shell from AAS server");

                            if (shell.Content["idShort"].ToString().Contains("HandoverDocumentation")) // check: which version of the Handover Documentation is used?
                            {
                                var smId = shell.Content?["id"].ToString();

                                                                var hod = GetSubmodelWithFallback(_restApiService,
                                  Base64Url.EncodeToString(Encoding.UTF8.GetBytes(shellId)),
                                                                    Base64Url.EncodeToString(Encoding.UTF8.GetBytes(smId)));
                                if (!hod.IsSuccessful)
                                    throw new HttpProblemResponseException(StatusCodes.Status500InternalServerError, "Error while fetching Handover Documentation from AAS server");

                                receivedHoDs[hod.Content["id"].AsValue().ToString()] = hod.Content;
                            }
                        }
                    }
                }
            }

            if (!featureFlagSkipParseAAS) {
                baseUrl = baseUrl.TrimEnd('/');
                return HoDParser.parseHandoverDocumentationSubmodels(receivedHoDs.Values.ToList(), $"{baseUrl}/shells", shellIdEncoded);
            }

            // Fallback, since the AAS libary does not work on arm64
            JsonObject hodJsonObject = null;
            foreach (var l in receivedHoDs)
            {
                hodJsonObject = l.Value.AsObject();
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
