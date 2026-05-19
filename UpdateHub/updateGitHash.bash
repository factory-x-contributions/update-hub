#!/usr/bin/env bash

# SPDX-FileCopyrightText: 2026 Fraunhofer-Institut für Produktionstechnik und Automatisierung IPA
# SPDX-FileCopyrightText: 2026 Hilscher Gesellschaft für Systemautomation mbH
# SPDX-FileCopyrightText: 2026 Siemens AG
#
# SPDX-License-Identifier: Apache-2.0

# This script determines the Git SHA256 inside .cs file

set -o pipefail
set -o errexit

# Get the current Git hash
if [[ -z "$GIT_HASH" ]]; then
  GIT_HASH=$(git rev-parse --short HEAD)
else
  GIT_HASH="$GIT_HASH"
fi

# Path to the GitHash.cs file
GIT_HASH_FILE="./Version/gitHash.cs"

# Create the GitHash.cs file with the Git hash
cat << EOF > $GIT_HASH_FILE
namespace UpdateHub.Version;

public static class GitHash
{
  public const string Value = "$GIT_HASH";

  public const UInt16 major = 0;
  public const UInt16 minor = 0;
  public const UInt16 patch = 0;
}


EOF

echo "Git hash updated in $GIT_HASH_FILE to $GIT_HASH"
