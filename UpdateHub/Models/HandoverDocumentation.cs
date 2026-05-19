// SPDX-FileCopyrightText: 2026 Fraunhofer-Institut für Produktionstechnik und Automatisierung IPA
// SPDX-FileCopyrightText: 2026 Hilscher Gesellschaft für Systemautomation mbH
// SPDX-FileCopyrightText: 2026 Siemens AG
//
// SPDX-License-Identifier: Apache-2.0

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
