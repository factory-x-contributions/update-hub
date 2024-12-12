namespace UpdateHub.Configuration;
using UpdateHub.Domain;

public static class ServiceCollectionExtensions
{
  public static void AddAasServerService(this IServiceCollection services)
  {
    services.AddSingleton<AasServerService>();
  }
}
/*
private void ConfigureHttpClients(IServiceCollection services)
{
  services.AddHttpClient<IService1, Service>(client =>
    {
      client.BaseAddress = new Uri("http://api.test.com/");
    })
    .UseErrorHandling()
    .UseDefaultHeaders();

}
*/

public class AasServerService
{
  private IAasServerRepository _repository;
  private readonly HttpClient _httpClient;

  public AasServerService()
  {
    _repository = new AasServerRepository();

    var sickAasServer = new AasServer
    {
      Name = "Sick",
      IdLinkPrefix = "https://pid.sick.com/",
      Url = "https://aasx.dev-cloud.sick.com",
      // TODO: Move to secret location
      Auth = new Oauth2CredentialsFlow
      {
        ClientId = Environment.GetEnvironmentVariable("SICK_CLIENTID"),
        ClientSecret = Environment.GetEnvironmentVariable("SICK_CLIENTSECRET"),
        TokenEndpoint = "https://id.sick.com/auth/realms/sickservices/protocol/openid-connect/token"
      }
    };
    _repository.AddAasServer(sickAasServer);

    var festoAasServer = new AasServer
    {
      Name = "Festo",
      IdLinkPrefix = "https://smart.festo.com/",
      Url = "https://dev.api.festo.com/products/aas/v1/",
      // TODO: Mov to secret location
      Auth =  new ApiKey
      {
        EnvironmentKey = "FESTO_BEARER"
      }
    };
    _repository.AddAasServer(festoAasServer);

    var testAas = new AasServer
    {
      Name = "test",
      IdLinkPrefix = "test/",
      Url = "http://localhost:8080",
      // TODO: Mov to secret location
      Auth =  new BearerToken
      {
        EnvironmentKey = "TEST_BEARER"
      }
    };
  _repository.AddAasServer(testAas);

  var testAas1 = new AasServer
  {
    Name = "test1",
    IdLinkPrefix = "test1/",
    Url = "http://localhost:8080",
    // TODO: Mov to secret location
    Auth = null
  };
  _repository.AddAasServer(testAas1 );

  }

  public  IAasServerRepository GetAasRepository()
  {
    return _repository;
  }
}
