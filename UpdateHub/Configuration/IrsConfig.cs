// SPDX-FileCopyrightText: 2026 Fraunhofer-Institut für Produktionstechnik und Automatisierung IPA
// SPDX-FileCopyrightText: 2026 Hilscher Gesellschaft für Systemautomation mbH
// SPDX-FileCopyrightText: 2026 Siemens AG
//
// SPDX-License-Identifier: Apache-2.0

namespace UpdateHub.Configuration;

public class IrsConfig
{
    public String ApiVersion { get; set; }
    public String Url { get; set; }
    public String Username { get; set; }
    public String Password { get; set; }
    public String ClientId { get; set; }
    public String ClientSecret { get; set; }
    public String TokenUrl { get; set; }
}
