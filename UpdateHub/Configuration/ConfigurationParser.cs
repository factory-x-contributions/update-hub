using Serilog;
using UpdateHub.Domain;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

// Read configuration

namespace UpdateHub.Configuration;

public class Parser
{
  public AasServerRepository aasServerRepository { get; set; } = null;

  public void ReadConfig(string configFilePath)
  {
    var deserializer = new DeserializerBuilder()
      .WithNamingConvention(HyphenatedNamingConvention.Instance)
      .WithTypeDiscriminatingNodeDeserializer((o) =>
      {
        IDictionary<string, Type> valueMappings = new Dictionary<string, Type>
        {
          { "oauth2", typeof(AuthConfigOAuth2) },
          { "apikey", typeof(AuthConfigApiKey) },
          { "bearertoken", typeof(AuthConfigBearerToken) },
        };
        o.AddKeyValueTypeDiscriminator<AuthConfig>("auth-type", valueMappings);
      })
      .Build();

    Log.Information($"Reading configuration from {configFilePath}");
     ApplicationConfig applicationConfig = null;
    try
    {
      using (var reader = new StreamReader(configFilePath))
      {
        applicationConfig = deserializer.Deserialize<ApplicationConfig>(reader);
      }
    }
    catch (Exception e)
    {
      Log.Error(e, $"Failed to read configuration file '{configFilePath}'");
    }
    aasServerRepository = new AasServerRepository();
    if (applicationConfig.aasServers == null|| applicationConfig.aasServers.Count == 0)
    {
      Log.Error("No AAS servers configured");
      return;
    }

    foreach (var aasServerConfig in applicationConfig.aasServers)
    {
      var aasServer = new AasServer
      {
        Name = aasServerConfig.Name,
        IdLinkPrefix = aasServerConfig.IdLinkPrefix,
        Url = aasServerConfig.Url
      };

      switch (aasServerConfig.Auth)
      {
        case AuthConfigOAuth2 oauth2:
          aasServer.Auth = new Oauth2CredentialsFlow
          {
            ClientId = oauth2.ClientId,
            ClientSecret = oauth2.ClientSecret,
            TokenUrl = oauth2.TokenUrl
          };
          break;

        case AuthConfigApiKey apiKey:
          aasServer.Auth = new ApiKeyAuth
          {
            ApiKey = apiKey.ApiKey
          };
          break;

        case AuthConfigBearerToken bearerToken:
          aasServer.Auth = new BearerTokenAuth
          {
            BearerToken = bearerToken.BearerToken
          };
          break;
        case null:
          aasServer.Auth = null;
          break;

        default:
          throw new InvalidOperationException($"Unsupported auth type: {aasServerConfig.Auth.AuthType}");
      }

      aasServerRepository.AddAasServer(aasServer);
    }
  }

  public override string ToString()
  {
    string ret = "AAS Server configuration:";
    foreach (var aasServer in aasServerRepository.GetAll())
    {
      ret += string.Format("{0}Name: {1}{0}IdLinkPrefix: {2}{0}Url: {3}{0}Auth: {4}{0}",
        Environment.NewLine,
        aasServer.Name,
        aasServer.IdLinkPrefix,
        aasServer.Url,
        aasServer.Auth != null ?  aasServer.Auth.GetType().ToString(): "none"
      );
    }

    return ret;
  }
}
