using System.Text.Json.Nodes;
using AasCore.Aas3_0;
using Newtonsoft.Json;

namespace UpdateHub.Models
{
    public record UpdateInformation(
      String id,
      String date,
      String version,
      String installationUri,
      String installationChecksum,
      JsonObject softwareNameplateSubmodel,
      JsonObject pcnRecord)
    {
    }

}
