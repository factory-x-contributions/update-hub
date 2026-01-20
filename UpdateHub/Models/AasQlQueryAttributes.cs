using System.Text.Json.Nodes;
using AasCore.Aas3_0;
using Newtonsoft.Json;

namespace UpdateHub.Models
{
    public record AasQlQueryAttributes(
      String ManufacturerName,
      String OrderCodeOfManufacturer,
      String ManufacturerProductType
      )
    {
    }
}
