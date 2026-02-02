using System.Buffers.Text;
using System.Text;
using System.Text.Json.Nodes;
using AasCore.Aas3_0;
using Extensions;
using Namotion.Reflection;
using Serilog;
using UpdateHub.Models;
using YamlDotNet.Core.Events;
using AasJsonization = AasCore.Aas3_0.Jsonization;

namespace UpdateHub.Domain
{
    public class HoDParser // for Handover Documentation *V1.2*
    {
        public static List<HandoverDocumentation> parseHandoverDocumentationSubmodels(List<JsonNode> hodSubmodels, string baseUrl, string aasIdentifier)
        {
            const string sIDv120 = "0173-1#01-AHF578#001"; // Handover Documentation V1.2.0
            const string sIDv200 = "0173-1#01-AHF578#003"; // Handover Documentation V2.0.0

            var handoverDocs = new List<HandoverDocumentation>();
            var parsedHoDSubmodels = new List<Submodel>(); // should only be one SB Handover Documentation

            foreach (var hodSubmodel in hodSubmodels)
            {
                parsedHoDSubmodels.Add(AasJsonization.Deserialize.SubmodelFrom(hodSubmodel));
            }

            // Check all HoD submodels (should be 1)
            foreach (var hodSubmodel in parsedHoDSubmodels)
            {

                // check which submodel version we have
                switch (hodSubmodel.GetSemanticKey().Value)
                {
                    case sIDv120:
                        Log.Information("Handover Documentation V1.2.0 found");
                        handoverDocs.AddRange(parseHoDV120(hodSubmodel, baseUrl, aasIdentifier));
                        break;
                    case sIDv200:
                        Log.Information("Handover Documentation V2.0.0 found");
                        Log.Information("FIXME: ... not implemented, using  ...");
                        // handoverDocs.AddRange(parseHoDV200(hodSubmodel));
                        break;
                    default:
                        Log.Error("Unknown Handover Documentation version found");
                        throw new InvalidOperationException("Unknown Handover Documentation version found");
                }
            }
            return handoverDocs;
        }

        // parseHandoverDocumentationSubmodels for V2.0.0
        private static List<HandoverDocumentation> parseHoDV200(Submodel hodSubmodel)
        {
            // var documents = new List<HandoverDocumentation>();
            // var collection = new List<SubmodelElementCollection>();

            // var DocumentSubmodelElementList = hodSubmodel.FindFirstIdShortAs<SubmodelElementList>("Documents");
            // // var submodel_as_json = AasJsonization.Serialize.ToJsonObject(DocumentSubmodelElementList);
            // // get document SubmodelElementList
            // foreach (var sme in DocumentSubmodelElementList.OverValueOrEmpty())
            // {
            //     Traverse(sme, collection, 1);
            // }
            // foreach (var col in collection)
            // {
            //     // CONTINUE HERE DAVID!!
            //     var id = "42";
            //     var date = "43";
            //     var version = "44";
            //     var title = "45";
            //     var language = "46";
            //     var classId = "47";
            //     var classSystem = "48";
            //     var status = "49";
            //     var organizationName = "50";
            //     var organizationOfficialName = "51";
            //     var keywords = "52";
            //     var digitalFilePath = "53";
            //     byte[] file = null;
            //     JsonObject hodJsonObject = null;

            //     var doc = new HandoverDocumentation(id, date, version, title, language, classId, classSystem, status, organizationName, organizationOfficialName, keywords, digitalFilePath, file, hodJsonObject);
            //     documents.Add(doc);
            // }
            // return documents;
            return null;
        }

        /// <summary>
        /// find element by semanticId
        /// </summary>
        /// <typeparam name="T">The type of submodel element to search for</typeparam>
        /// <param name="smc">The SubmodelElementCollection to search in</param>
        /// <param name="semanticId">The semantic ID to match</param>
        /// <returns>The first element of type T with the matching semantic ID, or null if not found</returns>
        private static T getBySemanticId<T>(SubmodelElementCollection smc, String semanticId) where T : ISubmodelElement
        {
            foreach (var dec in smc.DescendOnce().OfType<T>())
            {
                if (dec.SemanticId.Keys[0].Value.Split("*")[0] == semanticId)
                {
                    return dec;
                }
            }
            return default(T);
        }

        // parseHandoverDocumentationSubmodels for V1.2.0
        private static List<HandoverDocumentation> parseHoDV120(Submodel hodSubmodel, String baseUrl, String aasIdentifier)
        {
            var documents = new List<HandoverDocumentation>();
            var collection = new List<SubmodelElementCollection>();

            // get document SubmodelElementList
            foreach (var sme in hodSubmodel.OverSubmodelElementsOrEmpty())
            {
                // do not go deeper than one level, and there should only be number of documents in the AAS and Documents
                // just get toplevel collections
                if (sme.IdShort != "numberOfDocuments")
                {
                    if (sme is SubmodelElementCollection col)
                    {
                        collection.Add(col);
                        Log.Information($"Collection: '{sme.IdShort}'");
                    }
                }
            }

            var counter = 0;
            foreach (var col in collection)
            {
                // var semanticId = col.SemanticId;
                Log.Information($"{counter++} IdShort: {col.IdShort}");
                Log.Information($"decendOnce in col:");
                SubmodelElementCollection DocumentIdCollection = getBySemanticId<SubmodelElementCollection>(col, "0173-1#02-ABI501#001/0173-1#01-AHF580#001");
                SubmodelElementCollection DocumentClassificationCollection = getBySemanticId<SubmodelElementCollection>(col, "0173-1#02-ABI502#001/0173-1#01-AHF581#001");
                SubmodelElementCollection DocumentVersionCollection = getBySemanticId<SubmodelElementCollection>(col, "0173-1#02-ABI503#001/0173-1#01-AHF582#001");
                if ((DocumentIdCollection == null) || (DocumentClassificationCollection == null) || (DocumentVersionCollection == null))
                {
                    Log.Information("SMC missing. Skipping. next ...");
                    break;
                }
                // --- Start DocumentId Collection
                //@TODO (optional) domainId
                var domainIdProperty = getBySemanticId<Property>(DocumentIdCollection, "0173-1#02-ABH994#001"); // DocumentIdCollection.FindFirstIdShortAs<Property>("DocumentDomainId");
                var domainIdValuesAsJson = AasJsonization.Serialize.ToJsonObject(domainIdProperty);
                var domainId = (string)domainIdValuesAsJson["value"];

                // get id (documentId)
                var idProperty = getBySemanticId<Property>(DocumentIdCollection, "0173-1#02-AAO099#002");
                var idValuesAsJson = AasJsonization.Serialize.ToJsonObject(idProperty);
                var id = (string)idValuesAsJson["value"];
                // --- End DocumentId Collection

                // --- Start DocumentClassification Collection
                // get classId
                var classIdProperty = getBySemanticId<Property>(DocumentClassificationCollection, "0173-1#02-ABH996#001");// DocumentClassificationCollection.FindFirstIdShortAs<Property>("ClassificationSystem");
                var classIdValuesAsJson = AasJsonization.Serialize.ToJsonObject(classIdProperty);
                var classId = (string)classIdValuesAsJson["value"];

                // @TODO: className
                // get classificationSystem
                var classSystemProperty = getBySemanticId<Property>(DocumentClassificationCollection, "0173-1#02-ABH997#001");// DocumentClassificationCollection.FindFirstIdShortAs<Property>("ClassificationSystem");
                var classSystemValuesAsJson = AasJsonization.Serialize.ToJsonObject(classSystemProperty);
                var classSystem = (string)classSystemValuesAsJson["value"];
                // --- End DocumentClassification Collection

                // --- Start DocumentVersion Collection
                // get language
                var languageProperty = getBySemanticId<Property>(DocumentVersionCollection, "0173-1#02-AAN468#006");
                var languageValuesAsJson = AasJsonization.Serialize.ToJsonObject(languageProperty);
                var language = (string)languageValuesAsJson["value"];

                // get version
                var versionProperty = getBySemanticId<Property>(DocumentVersionCollection, "0173-1#02-AAO100#002");
                var versionValuesAsJson = AasJsonization.Serialize.ToJsonObject(versionProperty);
                var version = (string)versionValuesAsJson["value"];

                // get title
                var titleMLP = getBySemanticId<MultiLanguageProperty>(DocumentVersionCollection, "0173-1#02-AAO105#002");
                var titleValuesAsJson = AasJsonization.Serialize.ToJsonObject(titleMLP);
                var title = (string)titleValuesAsJson["value"][0]["text"]; // @TODO: Multiple titles might exist. I currently takes the first possible result. Better way would be: Search for the natively chosen language of the platform and if not found, chose english, if not found the first occurrence...

                // get keywords
                var keywordsMLP = getBySemanticId<MultiLanguageProperty>(DocumentVersionCollection, "0173-1#02-ABH999#001");
                var keywordsValuesAsJson = AasJsonization.Serialize.ToJsonObject(keywordsMLP);
                var keywords = (string)keywordsValuesAsJson["value"][0]["text"]; // @TODO: Same as title. Multiple keywords strings might exist. I currently takes the first possible result. Better way would be: Search for the natively chosen language of the platform and if not found, chose english, if not found the first occurrence...

                // get statusSetDate
                var dateProperty = getBySemanticId<Property>(DocumentVersionCollection, "0173-1#02-ABI000#001");
                var dateValuesAsJson = AasJsonization.Serialize.ToJsonObject(dateProperty);
                var date = (string)dateValuesAsJson["value"];

                // get statusValue
                var statusProperty = getBySemanticId<Property>(DocumentVersionCollection, "0173-1#02-ABI001#001");
                var statusValuesAsJson = AasJsonization.Serialize.ToJsonObject(statusProperty);
                var status = (string)statusValuesAsJson["value"];

                var organizationNameProperty = getBySemanticId<Property>(DocumentVersionCollection, "0173-1#02-ABI002#001");
                var organizationNameValuesAsJson = AasJsonization.Serialize.ToJsonObject(organizationNameProperty);
                var organizationName = (string)organizationNameValuesAsJson["value"];

                // get OrganizationOfficialName
                var organizationOfficialNameProperty = getBySemanticId<Property>(DocumentVersionCollection, "0173-1#02-ABI004#001");
                var organizationOfficialNameValuesAsJson = AasJsonization.Serialize.ToJsonObject(organizationOfficialNameProperty);
                var organizationOfficialName = (string)organizationOfficialNameValuesAsJson["value"];

                // get digitalFile
                var digitalFileProperty = getBySemanticId<AasCore.Aas3_0.File>(DocumentVersionCollection, "0173-1#02-ABI504#001/0173-1#01-AHF583#001");
                var digitalFileValuesAsJson = AasJsonization.Serialize.ToJsonObject(digitalFileProperty);
                var digitalFilePath = (string)digitalFileValuesAsJson["value"];
                // --- End DocumentVersion Collection

                // handle digitalFilePath to provide, if neccessary, an AAS API call
                if (digitalFilePath.Substring(0, 4).Contains("http") == false)
                {
                    Log.Information($"Shell Id: {aasIdentifier}");
                    var aasIdentifierEnc = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(aasIdentifier));
                    aasIdentifierEnc = aasIdentifierEnc.Remove(aasIdentifierEnc.Length - 2);
                    var submodelIdentifierEnc = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(hodSubmodel.Id));
                    submodelIdentifierEnc = submodelIdentifierEnc = submodelIdentifierEnc.Remove(submodelIdentifierEnc.Length - 2);
                    var idShortPath = $"{col.IdShort}.{DocumentVersionCollection.IdShort}.{digitalFileProperty.IdShort}"; // e.g. Document_01.DocumentVersion.DigitalFile
                    // specific for each shell, apparently legacy?
                    // digitalFilePath = $"{baseUrl}shells/{aasIdentifierEnc}/submodels/{submodelIdentifierEnc}/submodel-elements/{idShortPath}/attachment";
                    // new, go directly to submodel
                    digitalFilePath = $"{baseUrl}submodels/{submodelIdentifierEnc}/submodel-elements/{idShortPath}/attachment";
                }


                // JsonObject hodJsonObject = null;
                JsonObject hodJsonObject = AasJsonization.Serialize.ToJsonObject(col);

                var doc = new HandoverDocumentation(id, date, version, title, language, classId, classSystem, status, organizationName, organizationOfficialName, keywords, digitalFilePath, [], hodJsonObject);
                documents.Add(doc);
            }
            return documents;
        }

    }


}
