using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Refit;
using UpdateHub.Configuration;
using UpdateHub.Helper;

namespace UpdateHub.Domain;

public static class ServiceCollectionExtensions
{
  public static void AddAasModelFetcherService(this IServiceCollection services)
  {
    services.AddSingleton<AasModelFetcherService>();
  }
}


public class AasModelFetcherService(IHttpClientFactory httpClientFactory, AasServerService aasServerService)
{
  //public async Task<string> GetPcns(string idLink)
  public async Task<string> GetPcns(string idLink){

    var encodedIdLink = Uri.UnescapeDataString(idLink);
    var aasServer = aasServerService.GetAasRepository().GetByIdLink(encodedIdLink);
    if (aasServer == null)
    {
      throw new Exception("No AAS Server for given IDLink found");
      //return Results.Problem("No AAS Server for given IDLink found", statusCode: StatusCodes.Status404NotFound);
    }


    var httpClient = httpClientFactory.CreateClient(aasServer.Name);
    httpClient.BaseAddress = new Uri(aasServer.Url);
    aasServer.Auth.Authenticate(httpClient);
    IAasApi _restApiService = RestService.For<IAasApi>(httpClient);
    var test = _restApiService.LookupShells(Base64UrlOwnImplementation.Encode(idLink)).Result;
    Console.WriteLine(test.Content);

    throw new Exception("Not Implemented");
    return "";
  }
}
