// SPDX-FileCopyrightText: 2026 Fraunhofer-Institut für Produktionstechnik und Automatisierung IPA
// SPDX-FileCopyrightText: 2026 Hilscher Gesellschaft für Systemautomation mbH
// SPDX-FileCopyrightText: 2026 Siemens AG
//
// SPDX-License-Identifier: Apache-2.0

namespace UpdateHub.Version;

public static class ServiceVersion
{
  public static uint Major()
  {
    return GitHash.major;
  }

  public static uint Minor()
  {
    return GitHash.minor;
  }

  public static uint Patch()
  {
    return GitHash.patch;
  }

  public static string Commit()
  {
    return String.Format("{0}", GitHash.Value);
  }

  public static string SemanticVersion()
  {
    return String.Format("{0}.{1}.{2}", Major().ToString(),Minor().ToString(),Patch().ToString());
  }

  public static string FullVersion()
  {
    if (string.IsNullOrEmpty(Commit()))
      return String.Format("{0}.{1}.{2}", Major().ToString(),Minor().ToString(),Patch().ToString());

      return String.Format("{0}.{1}.{2}-{3}", Major().ToString(),Minor().ToString(),Patch().ToString(),Commit());
  }
}
