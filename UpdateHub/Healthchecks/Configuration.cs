// SPDX-FileCopyrightText: 2026 Fraunhofer-Institut für Produktionstechnik und Automatisierung IPA
// SPDX-FileCopyrightText: 2026 Hilscher Gesellschaft für Systemautomation mbH
// SPDX-FileCopyrightText: 2026 Siemens AG
//
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;
using UpdateHub.Configuration;
using UpdateHub.Domain;

namespace UpdateHub.Healthcheck;

public class HealthCheckConfiguration : IHealthCheck
{
  private readonly AasServerRepository aasServerRepository;


  public HealthCheckConfiguration(AasServerRepository aasServerRepository)
  {
    this.aasServerRepository = aasServerRepository;
  }

  public Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context, CancellationToken cancellationToken = default)
  {
     if (aasServerRepository == null)
      {
        return Task.FromResult(HealthCheckResult.Unhealthy("Configuration not available."));
      }

      if (aasServerRepository.GetAll().Count == 0)
      {
        return Task.FromResult(HealthCheckResult.Unhealthy("No AAS Server found."));
      }

      return Task.FromResult(HealthCheckResult.Healthy("Configuration valid."));
  }
}
