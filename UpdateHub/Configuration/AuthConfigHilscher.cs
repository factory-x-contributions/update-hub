namespace UpdateHub.Configuration
{
  // This is not an Oauth2 compatible configuration
  public class AuthConfigHilscher : AuthConfig
  {
    public string ClientId { get; set; }
    public string Secret { get; set; }
    public string LoginUrl { get; set; }
  }
}
