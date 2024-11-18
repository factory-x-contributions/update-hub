// Feature flag to transition to controller-based API approach
#undef CONTROLLER

using System.Reflection;
using Microsoft.OpenApi.Models;
using UpdateHub;

var builder = WebApplication.CreateBuilder(args);

#if CONTROLLER
  builder.Services.AddControllers();
#endif

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
  c.SwaggerDoc("v1", new OpenApiInfo
  {
    Title = "UpdateHub", Version = "v1",
    Description = "Example service to communicate with the different IPS.",
  });
  // using System.Reflection;
  var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
  c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

#if CONTROLLER
  app.MapControllers();
#else
app.MapGet("/version", () =>
  {
    var version = new ServiceVersion(0, 0, 0, GitHash.Value);
    return Results.Json(version);
  })
  .WithName("version")
  .WithDescription("")
  .WithTags("")
  .WithSummary("Service version")
  .Produces<ServiceVersion>(StatusCodes.Status200OK)
  .WithOpenApi();

app.MapGet("/update/{IdLink}", (string? IdLink) =>
  {
    // Early Abort, since no BL is inside the service
    return Results.Problem("No implemented, yet.", statusCode: StatusCodes.Status405MethodNotAllowed);

    if (string.IsNullOrEmpty(IdLink) || IdLink.ToUpper().Contains("ERROR"))
    {
      return Results.Problem("IdLink cannot be empty", statusCode: StatusCodes.Status400BadRequest);
    }

    //TODO: Some useful returns...
    var productChangeNofication = Enumerable.Range(1, 5).Select(index =>
        new ProductChangeNofication
        (
          "PCN-" + index,
          IdLink
        ))
      .ToArray();
    return Results.Ok(productChangeNofication);
  })
  .WithName("update")
  .WithDescription("No implemented, yet.\nReturns only HTTP status code 405")
  .WithSummary("Resolves a IdLink to PCNs")
  .WithTags("PCN")
  .Produces<ProductChangeNofication[]>(StatusCodes.Status200OK)
  .ProducesProblem(StatusCodes.Status400BadRequest)
  .ProducesProblem(StatusCodes.Status405MethodNotAllowed)
  .WithOpenApi();
#endif
app.Run();

#if !CONTROLLER
  record ServiceVersion(uint major, uint minor, uint patch, string hash) { }
  record ProductChangeNofication(string? Summary, string IdLink){ }
#endif

