using UpdateHub.DestinationRouter;

namespace UpdateHub.DestinationRouter
{
  public class DestinationRouter
  {
    enum destination
    {
      Sick,
      Hilscher,
    }
    public  string Probe(string IdLink)
    {
      switch(IdLink.ToUpper())
      {
        case "Sick":
          Console.WriteLine("Sick");
          break;
        case "Hilscher":
          Console.WriteLine("Hilscher");
          break;
        default:
          Console.WriteLine("Unknown");
          break;
      }

      return null;
    }

  }
}
