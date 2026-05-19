// SPDX-FileCopyrightText: 2026 Fraunhofer-Institut für Produktionstechnik und Automatisierung IPA
// SPDX-FileCopyrightText: 2026 Hilscher Gesellschaft für Systemautomation mbH
// SPDX-FileCopyrightText: 2026 Siemens AG
//
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Refit;
using Serilog;
using UpdateHub.Configuration;
using UpdateHub.Domain;
using UpdateHub.Models;

namespace UpdateHub.Service;


public interface IIrsService
{
    public List<UpdateInformation> GetSoftwareUpdate(string idLink, HttpRequest request);
    public List<HandoverDocumentation> GetHandoverDocumentation(string idLink, HttpRequest request);
}


public partial class IrsService : IIrsService
{
    private readonly ApplicationConfig? _applicationConfig;
    private readonly IHttpClientFactory? _httpClientFactory;

    private static readonly System.Diagnostics.Metrics.Meter meter = new Meter("IRSBroker",
      "1.0.0");
    private static readonly Counter<int> AasFound = meter.CreateCounter<int>("aas_found", "counter", "Counts the number of found AAS shells");
    private static readonly Counter<int> AasNotFound = meter.CreateCounter<int>("aas_not_found", "counter", "Counts the number of not found AAS shells");


    public IrsService(ApplicationConfig applicationConfig, IHttpClientFactory httpClientFactory)
    {
        _applicationConfig = applicationConfig;
        _httpClientFactory = httpClientFactory;
    }

    private bool skipParseAas(HttpRequest request)
    {
        var flag = false;
        return bool.TryParse(request.Headers["SKIP_PARSE_AAS"].ToString().ToLower(), out flag);
    }

    public List<UpdateInformation> GetSoftwareUpdate(string idLink, HttpRequest request)
    {
        var featureFlagSkipParseAAS = skipParseAas(request);

        var semanticIdSoftwareNameplateSubmodel = "https://admin-shell.io/idta/SoftwareNameplate/1/0";
        var semanticIdPcnSubmodel = "0173-10029#01-XFB001#001";

        var httpClient = _httpClientFactory.CreateClient();

        IAuth irsAuth;
        switch (_applicationConfig.irs.ApiVersion)
        {
            case "v2": // irs api v2 : cliendid + secret
                var credAuth = new Oauth2CredentialsFlow();
                credAuth.ClientId = _applicationConfig.irs.ClientId;
                credAuth.ClientSecret = _applicationConfig.irs.ClientSecret;
                credAuth.TokenUrl = _applicationConfig.irs.TokenUrl;
                irsAuth = credAuth;
                // irs v2 uses base64url encoding for semanticId
                // semanticIdSoftwareNameplateSubmodel = "aHR0cHM6Ly9hZG1pbi1zaGVsbC5pby9pZHRhL1NvZnR3YXJlTmFtZXBsYXRlLzEvMA";
                // semanticIdPcnSubmodel = "MDE3My0xMDAyOSMwMS1YRkIwMDEjMDAx";
                break;
            case "v1": // irs api v1 : username + password + token
            default:
                var pwAuth = new Oauth2PasswordFlow();
                pwAuth.Username = _applicationConfig.irs.Username;
                pwAuth.Password = _applicationConfig.irs.Password;
                pwAuth.TokenUrl = $"{_applicationConfig.irs.Url}/api/v1/token";
                irsAuth = pwAuth;
                break;
        }

        try
        {
            ((Oauth2CredentialsFlow)irsAuth).Authenticate(httpClient, _httpClientFactory);
        }
        catch
        {
            try
            {
                ((Oauth2PasswordFlow)irsAuth).Authenticate(httpClient, _httpClientFactory);
            }
            catch
            {
                throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while executing authentication with IRS");
            }
        }

        IIrsApiBase _restApiService;
        switch (_applicationConfig.irs.ApiVersion)
        {
            case "v2": // irs api v2
                httpClient.BaseAddress = new Uri($"{_applicationConfig.irs.Url}/api/v2");
                _restApiService = RestService.For<IIrsApi>(httpClient);
                break;
            case "v1": // irs api v1
            default:
                httpClient.BaseAddress = new Uri($"{_applicationConfig.irs.Url}/api/v1");
                _restApiService = RestService.For<IIrsApiV1>(httpClient);
                break;
        }

        try
        {

            var pcn = _restApiService.GetSubmodelsFromIdLink(idLink, semanticIdPcnSubmodel).Result;
            if (pcn.StatusCode != HttpStatusCode.OK)
            {

                AasNotFound.Add(1, new KeyValuePair<string, object>("IdLink", idLink.ToString()));
                throw new HttpProblemResponseException(StatusCodes.Status404NotFound, "No PCN found for given IdLink");
            }

            var receivedPcnSubmodels = pcn.Content;
            var receivedSoftwareNameplateSubmodels = _restApiService.GetSubmodelsFromIdLink(idLink, semanticIdSoftwareNameplateSubmodel).Result.Content;

            if (!featureFlagSkipParseAAS)
                return PcnParser.parsePcnAndSoftwareNameplateSubmodels(receivedPcnSubmodels,
                  receivedSoftwareNameplateSubmodels);

            // Fallback, since the AAS libary does not work on arm64
            JsonObject pcnJsonObject = null;
            JsonObject softwareNameplateJsonObject = null;
            foreach (var l in receivedSoftwareNameplateSubmodels)
            {
                softwareNameplateJsonObject = l.AsObject();
            }
            foreach (var l in receivedPcnSubmodels)
            {
                pcnJsonObject = l.AsObject();
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

    public List<HandoverDocumentation> GetHandoverDocumentation(string idLink, HttpRequest request)
    {
        var featureFlagSkipParseAAS = skipParseAas(request);

        var semanticIdHandoverDocumentationV120Submodel = "0173-1#01-AHF578#001"; // Handover Documentation V1.2.0
        var semanticIdHandoverDocumentationV200Submodel = "0173-1#01-AHF578#003"; // Handover Documentation V2.0.0
                                                                                  // var semanticIdSoftwareNameplateSubmodel = "https://admin-shell.io/idta/SoftwareNameplate/1/0";
                                                                                  // var semanticIdPcnSubmodel = "0173-10029#01-XFB001#001";

        var httpClient = _httpClientFactory.CreateClient();

        IAuth irsAuth;
        switch (_applicationConfig.irs.ApiVersion)
        {
            case "v2": // irs api v2 : cliendid + secret
                var credAuth = new Oauth2CredentialsFlow();
                credAuth.ClientId = _applicationConfig.irs.ClientId;
                credAuth.ClientSecret = _applicationConfig.irs.ClientSecret;
                credAuth.TokenUrl = _applicationConfig.irs.TokenUrl;
                irsAuth = credAuth;
                // v2 api uses base64 url encoded semantic ids
                // semanticIdHandoverDocumentationV120Submodel = "MDE3My0xIzAxLUFIRjU3OCMwMDE"; // Handover Documentation V1.2.0
                // semanticIdHandoverDocumentationV200Submodel = "MDE3My0xIzAxLUFIRjU3OCMwMDM"; // Handover Documentation V2.0.0
                break;
            case "v1": // irs api v1 : username + password + token
            default:
                var pwAuth = new Oauth2PasswordFlow();
                pwAuth.Username = _applicationConfig.irs.Username;
                pwAuth.Password = _applicationConfig.irs.Password;
                pwAuth.TokenUrl = $"{_applicationConfig.irs.Url}/api/v1/token";
                irsAuth = pwAuth;
                break;
        }

        try
        {
            ((Oauth2CredentialsFlow)irsAuth).Authenticate(httpClient, _httpClientFactory);
        }
        catch
        {
            try
            {
                ((Oauth2PasswordFlow)irsAuth).Authenticate(httpClient, _httpClientFactory);
            }
            catch
            {
                throw new HttpProblemResponseException(StatusCodes.Status401Unauthorized, "Error while executing authentication with IRS");
            }
        }


        IIrsApiBase _restApiService;
        switch (_applicationConfig.irs.ApiVersion)
        {
            case "v2": // irs api v2
                httpClient.BaseAddress = new Uri($"{_applicationConfig.irs.Url}/api/v2");
                _restApiService = RestService.For<IIrsApi>(httpClient);
                break;
            case "v1": // irs api v1
            default:
                httpClient.BaseAddress = new Uri($"{_applicationConfig.irs.Url}/api/v1");
                _restApiService = RestService.For<IIrsApiV1>(httpClient);
                break;
        }

        try
        {
            // check if any handover documentation is available
            var noHandover = 0;
            var receivedHoDSubmodels = new List<JsonNode>();
            // // version 2.0.0 - not implemented yet
            // var hodV200 = _restApiService.GetSubmodelsFromIdLink(idLink, semanticIdHandoverDocumentationV200Submodel).Result;
            // if (hodV200.StatusCode != HttpStatusCode.OK)
            // {
            //     AasNotFound.Add(1, new KeyValuePair<string, object>("IdLink", idLink.ToString()));
            // }
            // else
            // {
            //     noHandover += 1;
            //     receivedHoDSubmodels = hodV200.Content;
            // }

            // version 1.2.0 , stick to latest number when possible
            if (noHandover == 0)
            {
                var hodV120 = _restApiService.GetSubmodelsFromIdLink(idLink, semanticIdHandoverDocumentationV120Submodel).Result;
                if (hodV120.StatusCode != HttpStatusCode.OK)
                {
                    AasNotFound.Add(1, new KeyValuePair<string, object>("IdLink", idLink.ToString()));
                }
                else
                {
                    noHandover += 1;
                    receivedHoDSubmodels = hodV120.Content;
                }
            }

            if (noHandover == 0)
            {
                throw new HttpProblemResponseException(StatusCodes.Status404NotFound, "No Handover Documentation (V1.2 or V2.0) found for given IdLink");
            }


            if (!featureFlagSkipParseAAS) {
                var  baseUrl =  httpClient.BaseAddress.ToString().TrimEnd('/');
                return HoDParser.parseHandoverDocumentationSubmodels(receivedHoDSubmodels, $"{baseUrl}/assets", idLink); // baseUrl, aasIdentifier
            }
                                                                                                                                                   // return HoDParser.parseHandoverDocumentationSubmodels(receivedHoDSubmodels, _applicationConfig.irs.Url, idLink); // baseUrl, aasIdentifier

            // Fallback, since the AAS libary does not work on arm64
            JsonObject hodJsonObject = null;
            foreach (var l in receivedHoDSubmodels)
            {
                hodJsonObject = l.AsObject();
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
