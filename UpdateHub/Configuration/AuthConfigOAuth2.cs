// SPDX-FileCopyrightText: 2026 Fraunhofer-Institut für Produktionstechnik und Automatisierung IPA
// SPDX-FileCopyrightText: 2026 Hilscher Gesellschaft für Systemautomation mbH
// SPDX-FileCopyrightText: 2026 Siemens AG
//
// SPDX-License-Identifier: Apache-2.0

namespace UpdateHub.Configuration
{
  public class AuthConfigOAuth2 : AuthConfig
  {
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }

    public string TokenUrl { get; set; }
  }
}
