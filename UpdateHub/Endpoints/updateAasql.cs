// SPDX-FileCopyrightText: 2026 Fraunhofer-Institut für Produktionstechnik und Automatisierung IPA
// SPDX-FileCopyrightText: 2026 Hilscher Gesellschaft für Systemautomation mbH
// SPDX-FileCopyrightText: 2026 Siemens AG
//
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Text;
using Serilog;

namespace UpdateHub.Endpoints;

using UpdateHub.Models;
using Service;

public static class UpdateAasql
{
  public static RouteGroupBuilder AasqlEndpoints(this RouteGroupBuilder group)
  {
    group.MapPost("/aasql/update",
        (
          HttpRequest request,
          AasQlQueryAttributes aasQLAttributes,
          IHttpClientFactory httpClientFactory,
          IAasQlService aasQLService
        ) =>
        {         
          try
          {
            return Results.Ok(aasQLService.GetSoftwareUpdateViaAssetIDQuery(aasQLAttributes, request));
          }
          catch (HttpProblemResponseException e)
          {
            return Results.Problem(e.Value.ToString(), statusCode: e.StatusCode);
          }
          // Unknown exception handled by dotnet itself, with
          // UseExceptionHandler and UseDeveloperExceptionPage
          catch (Exception e)
          {
            Log.Information(e.ToString());
            return Results.Problem(e.Message, statusCode: StatusCodes.Status500InternalServerError);
          }
        })
      .WithName("AASQL")
      .WithDescription("Execute AASql query to find software updates based on nameplate attributes")
      .WithSummary("Resolves nameplate attributes to PCNs using AASql")
      .WithTags("SoftwareUpdate")
      .Accepts<AasQlQueryAttributes>("application/json")
      .Produces<UpdateInformation[]>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .ProducesProblem(StatusCodes.Status500InternalServerError)
      .WithOpenApi();

    return group;
  }
}
