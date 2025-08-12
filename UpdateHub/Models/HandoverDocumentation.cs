using System.Text.Json.Nodes;
using AasCore.Aas3_0;
using Newtonsoft.Json;

namespace UpdateHub.Models
{
    public record HandoverDocumentation( // FIXME:
      String id,
      String date,
      String version,
      String title,
      String language,
      String classId,
      String classSystem, // This one just for future use (and awareness), the first iteration just assumes it's "VDI 2770: Blatt 1"
      String status,
      String organizationName,
      String organizationOfficialName,
      String keywords,
      String filepath,
      byte[] file, // FIXME: Does this type work out?
      JsonObject HandoverDocumentationSubmodel
      )
    {
    }
}
