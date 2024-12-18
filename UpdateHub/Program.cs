// Feature flag to transition to controller-based API approach

using System.Reflection;
using Microsoft.OpenApi.Models;
using UpdateHub;
using UpdateHub.Configuration;
using UpdateHub.Domain;
using UpdateHub.Endpoints;

using Serilog;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using System;

var CONFIG_FILE_PATH = "./config.yaml";

// Configure logging
Log.Logger = new LoggerConfiguration()
  .WriteTo.Console()
  .MinimumLevel.Debug()
  .CreateLogger();

Log.Information("Starting up. Version: {0}.{1}.{2} Commit: {3}", GitHash.major, GitHash.minor, GitHash.patch,GitHash.Value);

// Read configuration
var deserializer = new DeserializerBuilder()
      .WithNamingConvention(HyphenatedNamingConvention.Instance)
      .WithTypeDiscriminatingNodeDeserializer((o) =>
      {
        IDictionary<string, Type> valueMappings = new Dictionary<string, Type>
        {
            { "oauth2", typeof(AuthConfigOAuth2) },
            { "apikey", typeof(AuthConfigApiKey) },
            { "bearertoken", typeof(AuthConfigBearerToken) }
        };
        o.AddKeyValueTypeDiscriminator<AuthConfig>("auth-type", valueMappings);
      })
      .Build();

Log.Information($"Reading configuration from {CONFIG_FILE_PATH}");
ApplicationConfig applicationConfig;
try
{
  using (var reader = new StreamReader(CONFIG_FILE_PATH))
  {
    applicationConfig = deserializer.Deserialize<ApplicationConfig>(reader);
  }
}
catch (Exception e)
{
  Log.Error(e, $"Failed to read configuration file '{CONFIG_FILE_PATH}'");
  return;
}

// Configure web application
var builder = WebApplication.CreateBuilder(args);

var aasServerRepository = new AasServerRepository();
if (applicationConfig.aasServers.Count == 0)
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

    default:
      throw new InvalidOperationException($"Unsupported auth type: {aasServerConfig.Auth.AuthType}");
  }

  aasServerRepository.AddAasServer(aasServer);

}
builder.Services.AddSingleton(aasServerRepository);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(c =>
{
  c.EnableAnnotations();
  c.SwaggerDoc("v1", new OpenApiInfo
  {
    Title = "UpdateHub", Version = "v1",
    Description = "Example service to communicate with the different IPS."
  });
  // using System.Reflection;
  var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
  c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});
builder.Services.AddHttpClient(string.Empty).ConfigureHttpClient(c => {
  c.DefaultRequestHeaders.Add("User-Agent", "Updatehub/" + GitHash.major + "." + GitHash.minor + "." + GitHash.patch);
  c.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddAasModelFetcherService(); // TODO: Remove with update/idlink1

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

app.VersionEndpoint();
app.Idlink();

app.UseRouting();
app.MapControllers();

app.Run();
