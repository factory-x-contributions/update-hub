using Serilog;
using UpdateHub.Domain;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3.Model;
using Amazon.S3;
using Amazon;
using System;
using Amazon.S3.Util;

// Read configuration

namespace UpdateHub.Configuration;

public class Parser
{
  public AasServerRepository aasServerRepository { get; set; } = null;

  public async void ReadConfig(string configFilePath)
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
      var uri = new Uri(configFilePath);
      AmazonS3Uri AmazonS3UriObject = null;
      if(AmazonS3Uri.TryParseAmazonS3Uri(uri, out AmazonS3UriObject)){
        try{
          //Get config File from S3 Bucket
          var BucketName = AmazonS3UriObject.Bucket;
          var Key = AmazonS3UriObject.Key;
          // Enviroment variables must be given for the s3Client configuration
          var s3Client = new AmazonS3Client(RegionEndpoint.EUCentral1);
          var GetObjectRequest = new GetObjectRequest
          {
            BucketName = BucketName,
            Key = Key
          };
          using var getObjectResponse = s3Client.GetObjectAsync(GetObjectRequest).Result;
          using var stream = getObjectResponse.ResponseStream;
          using var reader = new StreamReader(stream);
          applicationConfig = deserializer.Deserialize<ApplicationConfig>(reader);
        }
        catch(AmazonS3Exception ex)
        {
            Log.Error($"Error encountered on server. Message:'{ex.Message}' when reading from S3 Bucket", ex);
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Log.Error($"Unknown error encountered while accessing S3, Error Message:'{ex.Message}'", ex);
            Environment.Exit(1);
        }
      }else if(uri.IsFile){
        try
        {
          using (var reader = new StreamReader(uri.LocalPath))
          {
            applicationConfig = deserializer.Deserialize<ApplicationConfig>(reader);
          }
        }
        catch (Exception e)
        {
          Log.Error(e, $"Failed to read configuration file '{configFilePath}'");
          Environment.Exit(1);
        }
      }
    }
    catch (Exception e)
    {
      if(File.Exists(configFilePath)){
        using (var reader = new StreamReader(configFilePath))
          {
            applicationConfig = deserializer.Deserialize<ApplicationConfig>(reader);
          }
      }else{
      Log.Error(e, $"Failed to read configuration file '{configFilePath}'");
      Environment.Exit(1);
      }
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
