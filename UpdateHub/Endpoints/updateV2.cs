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

public static class UpdateV2EndpointsExt
{
  public static RouteGroupBuilder IdLinkV2Endpoint(this RouteGroupBuilder group)
  {
    group.MapGet("/update/{assetIdBase64Encoded}",
        (
          HttpRequest request,
          string assetIdBase64Encoded,
          IHttpClientFactory httpClientFactory,
          IAasService aasService
        ) =>
        {
          var encodedIdLink = "";

          // TODO: Make input validation readable
          try
          {
            encodedIdLink = Encoding.UTF8.GetString(Base64Url.DecodeFromUtf8(Encoding.UTF8.GetBytes(assetIdBase64Encoded)));

          return Results.Ok(aasService.GetSoftwareUpdate(encodedIdLink, request));
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
      .WithName("Update V2")
      .WithDescription("")
      .WithSummary("Resolves a IdLink to PCNs")
      .WithTags("SoftwareUpdate")
      .Produces<UpdateInformation[]>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .ProducesProblem(StatusCodes.Status500InternalServerError)
      .WithOpenApi();

    return group;
  }
}
