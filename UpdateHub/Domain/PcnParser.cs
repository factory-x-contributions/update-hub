using System.Text.Json.Nodes;
using AasCore.Aas3_0;
using Extensions;
using Serilog;
using UpdateHub.Models;
using AasJsonization = AasCore.Aas3_0.Jsonization;

namespace UpdateHub.Domain
{
  public class PcnParser
  {
    public static List<UpdateInformation> parsePcnAndSoftwareNameplateSubmodels(List<JsonNode> pcnSubmodels, List<JsonNode> softwareNameplateSubmodels)
    {
      var updates = new List<UpdateInformation>();

      var parsedPcnSubmodels = new List<Submodel>();
      var parsedSoftwareNameplateSubmodels = new Dictionary<String, Submodel>();

      foreach (var pcnSubmodel in pcnSubmodels)
      {
        parsedPcnSubmodels.Add(AasJsonization.Deserialize.SubmodelFrom(pcnSubmodel));
      }

      foreach (var softwareNameplateSubmodel in softwareNameplateSubmodels)
      {
        var parsedSoftwareNameplateSubmodel = AasJsonization.Deserialize.SubmodelFrom(softwareNameplateSubmodel);
        parsedSoftwareNameplateSubmodels.Add(parsedSoftwareNameplateSubmodel.Id, parsedSoftwareNameplateSubmodel);
      }

      // Check all PCN submodels...
      foreach (var pcnSubmodel in parsedPcnSubmodels)
      {
        // ... and all records of the PCN subnmodel ...
        var smlRecords = pcnSubmodel.FindSubmodelElementByIdShort("Records");
        foreach (SubmodelElementCollection record in smlRecords.GetChildsAsList())
        {
          // ... and all items of change of the record
          var smlReasonsOfChange = record.FindFirstIdShortAs<SubmodelElementList>("ReasonsOfChange");
          foreach (SubmodelElementCollection resonOfChange in smlReasonsOfChange.GetChildsAsList())
          {
            // ... if the reason of change equals "SOFTW" --> Record is a firmware update
            if (resonOfChange.FindFirstIdShortAs<ISubmodelElement>("ReasonId").ValueAsText().Equals("SOFTW"))
            {
              // Get id of the related software nameplate submodel
              var smcItemOfChange = record.FindFirstIdShortAs<SubmodelElementCollection>("ItemOfChange");
              var smlTechnicalDataChanges = smcItemOfChange.FindFirstIdShortAs<SubmodelElementList>("TechnicalData_Changes");
              // ... and all technical data changes of the item of change
              foreach (SubmodelElementCollection smcTechnicalDataChange in smlTechnicalDataChanges.GetChildsAsList())
              {
                // ... if the reason id equals "SOFTW" or "FUNCTION --> Record is a firmware update
                var reasonId = smcTechnicalDataChange.FindFirstIdShortAs<ISubmodelElement>("ReasonId").ValueAsText();
                if (reasonId.Equals("SOFTW") || reasonId.Equals("FUNCTION"))
                {
                  // Get date of record
                  var date = record.FindFirstIdShortAs<ISubmodelElement>("DateOfRecord").ValueAsText();

                  // Get id of the related software nameplate submodel
                  var refOriginOfChange = smcTechnicalDataChange.FindFirstIdShortAs<ReferenceElement>("Origin_of_change");
                  foreach (var key in refOriginOfChange.Value.Keys)
                  {
                    if (key.Type.Equals(KeyTypes.Submodel))
                    {
                      var versionMLProperty = smcTechnicalDataChange.FindFirstIdShortAs<MultiLanguageProperty>("FirmwareVersion");
                      var version = "N/A";
                      if (versionMLProperty != null)
                      {
                        var versionValuesAsJson = AasJsonization.Serialize.ToJsonObject(versionMLProperty);
                        version = (string)versionValuesAsJson["value"][0]["text"];
                      }
                      else
                      {
                        var versionProperty = smcTechnicalDataChange.FindFirstIdShortAs<Property>("Version");
                        var versionValuesAsJson = AasJsonization.Serialize.ToJsonObject(versionProperty);
                        version = (string)versionValuesAsJson["value"];

                      }
                      var softwareNameplateSubmodelId = key.Value;
                      Submodel softwareNameplateSubmodel = null;
                      if (parsedSoftwareNameplateSubmodels.ContainsKey(softwareNameplateSubmodelId))
                      {
                        softwareNameplateSubmodel = parsedSoftwareNameplateSubmodels[softwareNameplateSubmodelId];
                      }
                      else
                      {
                        foreach (var parsedSoftwareNameplateSubmodel in parsedSoftwareNameplateSubmodels.Values)
                        {
                          var smcSoftwareNameplateType = parsedSoftwareNameplateSubmodel.FindFirstIdShortAs<SubmodelElementCollection>("SoftwareNameplateType");
                          var versionProperty = smcSoftwareNameplateType.FindFirstIdShortAs<Property>("Version");
                          if (versionProperty.ValueAsText().Equals(version)) {
                            softwareNameplateSubmodel = parsedSoftwareNameplateSubmodel;
                          }
                        }
                      }

                      if (softwareNameplateSubmodel != null) {
                        var smcSoftwareNameplateType = softwareNameplateSubmodel.FindFirstIdShortAs<SubmodelElementCollection>("SoftwareNameplateType");
                        var propInstallationUri = smcSoftwareNameplateType.FindFirstIdShortAs<Property>("InstallationUri");
                        var installationUri = propInstallationUri.ValueAsText();
                        var propInstallationChecksum = smcSoftwareNameplateType.FindFirstIdShortAs<Property>("InstallationChecksum");
                        var installationChecksum = propInstallationChecksum.ValueAsText();

                        var serializedSoftwareNameplate = AasJsonization.Serialize.ToJsonObject(softwareNameplateSubmodel);
                        var serializedRecord = AasJsonization.Serialize.ToJsonObject(record);

                        var update = new UpdateInformation(date, version, installationUri, installationChecksum, serializedSoftwareNameplate, serializedRecord);
                        updates.Add(update);
                      }
                      else
                      {
                        Log.Error($"Failed to find Software Nameplate Submodel for version '{version}'");
                      }
                    }
                  }

                }
              }
            }
          }
        }
      }

      return updates;
    }
  }
}
