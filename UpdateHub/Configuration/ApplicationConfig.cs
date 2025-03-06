namespace UpdateHub.Configuration
{
  public class ApplicationConfig
  {
    public List<AasServerConfig> aasServers { get; set; } = new List<AasServerConfig>();

    public IrsConfig irs { get; set; } = new IrsConfig();
  }
}
