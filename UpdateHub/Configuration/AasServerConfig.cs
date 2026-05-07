using UpdateHub.Domain;
using UpdateHub.Service;

namespace UpdateHub.Configuration
{
  public class AasServerConfig
  {
    public string Name { get; set; }

    public string IdLinkPrefix { get; set; }

    public string[] AasEndpointPrefixes { get; set; } = [];

    public string Url { get; set; }
    public string DiscoveryUrl { get; set; }

    public IAasService.AasVersion? Version { get; set; }

    public AuthConfig Auth { get; set; }

  }
}
