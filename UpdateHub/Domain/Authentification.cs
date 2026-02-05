using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using SSIExtension;

namespace UpdateHub.Domain;

using Refit;

public interface IAuth
{
    public bool Authenticate(HttpClient httpClient, IHttpClientFactory httpClientFactory);
}

// Oauth2
public abstract class TokenEndpointRequest
{
    [AliasAs("grant_type")]
    public string GrantType { get; set; }
}

public class TokenEndpointRequestCredentialsFlow : TokenEndpointRequest
{
    [AliasAs("client_id")]
    public string ClientId { get; set; }

    [AliasAs("client_secret")]
    public string ClientSecret { get; set; }
}

public class TokenEndpointRequestPasswordFlow : TokenEndpointRequest
{
    [AliasAs("username")]
    public string Username { get; set; }

    [AliasAs("password")]
    public string Password { get; set; }
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
    public string TokenUrl { get; set; }

    public bool Authenticate(HttpClient httpClient, IHttpClientFactory httpClientFactory)
    {
        if (string.IsNullOrEmpty(ClientId) || string.IsNullOrEmpty(ClientSecret) || string.IsNullOrEmpty(TokenUrl))
        {
            return false;
        }

        var tokenHttpClient = httpClientFactory.CreateClient();
        tokenHttpClient.BaseAddress = new Uri(TokenUrl);
        var _restApiService = RestService.For<IOauth2Token>(tokenHttpClient);
        var token = _restApiService.GetAccessToken(new TokenEndpointRequestCredentialsFlow
        {
            ClientId = ClientId,
            ClientSecret = ClientSecret,
            GrantType = "client_credentials"
        }).Result;

        if (!token.IsSuccessful || (token.StatusCode != HttpStatusCode.OK))
        {
            Log.Information("HTTP Status Code: " + token.StatusCode);
            return false;
        }

        httpClient.DefaultRequestHeaders.Add("Authorization", string.Format("Bearer {0}", token.Content.AccessToken));
        return true;
    }
}

public class Oauth2PasswordFlow : IAuth
{
    public string Username { get; set; }
    public string Password { get; set; }
    public string TokenUrl { get; set; }

    public bool Authenticate(HttpClient httpClient, IHttpClientFactory httpClientFactory)
    {
        if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password) || string.IsNullOrEmpty(TokenUrl))
        {
            return false;
        }

        var tokenHttpClient = httpClientFactory.CreateClient();
        tokenHttpClient.BaseAddress = new Uri(TokenUrl);
        var _restApiService = RestService.For<IOauth2Token>(tokenHttpClient);
        var token = _restApiService.GetAccessToken(new TokenEndpointRequestPasswordFlow
        {
            Username = Username,
            Password = Password,
            GrantType = "password"
        }).Result;

        if (!token.IsSuccessful || (token.StatusCode != HttpStatusCode.OK))
        {
            Log.Information("HTTP Status Code: " + token.StatusCode);
            return false;
        }

        httpClient.DefaultRequestHeaders.Add("Authorization", string.Format("Bearer {0}", token.Content.AccessToken));
        return true;
    }
}


public class ApiKeyAuth : IAuth
{
    public string ApiKey { get; set; }

    public bool Authenticate(HttpClient httpclient, IHttpClientFactory httpClientFactory)
    {
        httpclient.DefaultRequestHeaders.Add("Authorization", ApiKey);
        return true;
    }
}

public class BearerTokenAuth : IAuth
{
    public string BearerToken { get; set; }

    public bool Authenticate(HttpClient httpclient, IHttpClientFactory httpClientFactory)
    {
        httpclient.DefaultRequestHeaders.Add("Authorization", BearerToken);
        return true;
    }
}


public class HilscherLoginEndpointRequest
{
    [AliasAs("clientId")]
    public string clientId { get; set; }

    [AliasAs("secret")]
    public string secret { get; set; }
}

public class HilscherLoginResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; }
}

public interface IHilscherLogin
{
    [Post("")]
    [Headers("Content-Type: application/json", "Accept: application/json")]
    Task<ApiResponse<HilscherLoginResponse>> GetBearerToken(HilscherLoginEndpointRequest hilscherLoginEndpointRequest);
}

public class HilscherAuth : IAuth
{
    public string ClientId { set; get; }
    public string Secret { set; get; }
    public string LoginUrl { set; get; }

    public bool Authenticate(HttpClient httpClient, IHttpClientFactory httpClientFactory)
    {

        var tokenHttpClient = httpClientFactory.CreateClient();
        tokenHttpClient.BaseAddress = new Uri(LoginUrl);
        var _restApiService = RestService.For<IHilscherLogin>(tokenHttpClient);
        var token = _restApiService.GetBearerToken(new HilscherLoginEndpointRequest
        {
            clientId = ClientId,
            secret = Secret,
        }).Result;
        if (!token.IsSuccessful || (token.StatusCode != HttpStatusCode.OK))
        {
            Log.Information("HTTP Status Code: " + token.StatusCode);
            return false;
        }

        httpClient.DefaultRequestHeaders.Add("Authorization", string.Format("Bearer {0}", token.Content.Token));
        return true;
    }
}
