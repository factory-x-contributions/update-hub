// SPDX-FileCopyrightText: 2026 Fraunhofer-Institut für Produktionstechnik und Automatisierung IPA
// SPDX-FileCopyrightText: 2026 Hilscher Gesellschaft für Systemautomation mbH
// SPDX-FileCopyrightText: 2026 Siemens AG
//
// SPDX-License-Identifier: Apache-2.0

using UpdateHub.Domain;
using UpdateHub.Service;

namespace UpdateHub.Configuration
{
  public class AasServerConfig
  {
    public string Name { get; set; }

    public string IdLinkPrefix { get; set; }

    public string[] AasEndpointPrefixes { get; set; } = [];

    public string Url { get; set; }
    public string DiscoveryUrl { get; set; }

    public IAasService.AasVersion? Version { get; set; }

    public AuthConfig Auth { get; set; }

  }
}
