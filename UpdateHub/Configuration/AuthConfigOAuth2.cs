namespace UpdateHub.Configuration
{
  public class AuthConfigOAuth2 : AuthConfig
  {
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }

    public string TokenUrl { get; set; }
  }
}
