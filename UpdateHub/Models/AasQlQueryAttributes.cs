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
    public record AasQlQueryAttributes(
      String ManufacturerName,
      String OrderCodeOfManufacturer,
      String ManufacturerProductType
      )
    {
    }
}
