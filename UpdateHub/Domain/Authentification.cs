using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UpdateHub.Domain;

using Refit;

public interface IAuth
{
  public bool Authenticate(HttpClient httpclient);
}

// Oauth2
public class TokenEndpointRequest
{
  [AliasAs("grant_type")]
  public string GrantType { get; set; }
  [AliasAs("client_id")]
  public string ClientId { get; set; }

  [AliasAs("client_secret")]
  public string ClientSecret { get; set; }
}

public class TokenResponse
{
  [JsonPropertyName("access_token")]
  public string AccessToken { get; set; }

[JsonPropertyName("token_type")]
  public string TokenType { get; set; }

  [JsonPropertyName("expires_in")]
  public int ExpiresIn { get; set; }

  [JsonPropertyName("refresh_expires_in")]
  public int RefreshExpiresIn { get; set; }

  [JsonPropertyName("scope")]
  public string Scope { get; set; }

}
public interface IOauth2Token
{
  [Post("")]
  [Headers("Content-Type: application/x-www-form-urlencoded; charset=UTF-8", "Accept: application/json")]
  Task<ApiResponse<TokenResponse>> GetAccessToken([Body(BodySerializationMethod.UrlEncoded)] TokenEndpointRequest tokenEndpointRequest);
}

public class Oauth2CredentialsFlow : IAuth
{
  public string ClientId { get; set; }
  public string ClientSecret { get; set; }
  public string TokenEndpoint { get; set; }

  public bool Authenticate(HttpClient httpClient )
  {
    if (string.IsNullOrEmpty(ClientId) || string.IsNullOrEmpty(ClientSecret) || string.IsNullOrEmpty(TokenEndpoint))
    {
      return false;
    }

    HttpClient tokenHttpClient = new HttpClient();
    tokenHttpClient.BaseAddress = new Uri(TokenEndpoint);
    var _restApiService = RestService.For<IOauth2Token>(tokenHttpClient);
    var token = _restApiService.GetAccessToken(new TokenEndpointRequest
    {
      ClientId = ClientId,
      ClientSecret = ClientSecret,
      GrantType = "client_credentials"
    }).Result;

    if (!token.IsSuccessful || (token.StatusCode != HttpStatusCode.OK))
    {
      Console.WriteLine("HTTP Status Code: " + token.StatusCode);
      return false;
    }

    httpClient.DefaultRequestHeaders.Add("Authorization", string.Format("Bearer {0}",token.Content.AccessToken));
    return true;
  }
}

public class ApiKey : IAuth
{
  public string EnvironmentKey { get; set; }

  public bool Authenticate(HttpClient httpclient)
  {
    var apiKey = Environment.GetEnvironmentVariable(EnvironmentKey);
    httpclient.DefaultRequestHeaders.Add("Authorization", string.Format("{0}", apiKey));
    return true;
  }
}

public class BearerToken : IAuth
{
  public string EnvironmentKey { get; set; }

  public bool Authenticate(HttpClient httpclient)
  {
    var bearerToken = Environment.GetEnvironmentVariable(EnvironmentKey);
    httpclient.DefaultRequestHeaders.Add("Authorization", string.Format("Bearer {0}", bearerToken));
    return true;
  }
}
