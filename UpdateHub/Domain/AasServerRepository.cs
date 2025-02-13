namespace UpdateHub.Domain;

// Interface
public interface IAasServerRepository
{
  public void AddAasServer(AasServer aasServer);
  public List<AasServer> GetAll();
  public AasServer GetByIdLink(string idLink);
}

// Entity
public class AasServer
{
  public string Name { get; set; }
  public string IdLinkPrefix { get; set; }
  public string[] AasEndpointPrefixes { get; set; }
  public string Url { get; set; }
  public IAuth? Auth { get; set; }
}

public class AasServerRepository : IAasServerRepository
{
  private List<AasServer> _aasServers = new();

  public void AddAasServer(AasServer aasServer)
  {
    _aasServers.Add(aasServer);
  }

  public List<AasServer> GetAll()
  {
    return _aasServers;
  }

  public AasServer GetByIdLink(string idLink)
  {
    if (string.IsNullOrEmpty(idLink))
    {
      return null;
    }

    foreach (var aasServer in _aasServers)
    {
      if (idLink.ToLower().Contains(aasServer.IdLinkPrefix.ToLower()))
      {
        return aasServer;
      }
    }

    return null;
  }


  public AasServer GetByAasEndpointPrefix(string aasId)
  {
    if (string.IsNullOrEmpty(aasId))
    {
      return null;
    }

    foreach (var aasServer in _aasServers)
    {
      foreach(var aasEndpointPreix in aasServer.AasEndpointPrefixes)
      {
        if (aasId.ToLower().Contains(aasEndpointPreix.ToLower()))
        {
          return aasServer;
        }
      }
    }

    return null;
  }

}

/*
      public class AasService
         {
      private IAasServerRepository _repository;
        public AasService(IAasServerRepository repository)
        {
            _repository = repository;
        }

        public void AddAasServer(AasServer aasServer)
        {
            _repository.AddAasServer(aasServer);
        }

        public List<AasServer> GetAll()
        {
            return _repository.GetAll();
        }

       }
       */
