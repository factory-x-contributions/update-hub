using System;
using System.Reflection;
using Microsoft.Extensions.Primitives;
using Microsoft.OpenApi.Models;
using UpdateHub.Configuration;
using UpdateHub.Endpoints;
using Serilog;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;
using UpdateHub.Middleware;
using UpdateHub.Version;
using UpdateHub.Healthcheck;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using UpdateHub.Service;
using Version = UpdateHub.Endpoints.Version;
using Asp.Versioning;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

var configFilePath = Environment.GetEnvironmentVariable("CONFIG_FILE_PATH") ?? "./config.yaml";
var otlpEndpoint = Environment.GetEnvironmentVariable("OTLP_ENDPOINT_URL") ?? null;
var enableConsoleExporter = Convert.ToBoolean(Environment.GetEnvironmentVariable("OTEL_CONSOLE_EXPORTER"));

// Configure logging
var levelSwitch = new LoggingLevelSwitch();
Log.Logger = new LoggerConfiguration()
//  .WriteTo.Console()
.WriteTo.Console(outputTemplate:
    "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
.WriteTo.Conditional(evt => (otlpEndpoint != null), wt => wt.OpenTelemetry(options =>
{
    options.Endpoint = otlpEndpoint;
    options.IncludedData =
      IncludedData.SpanIdField
      | IncludedData.TraceIdField;
    options.ResourceAttributes = new Dictionary<string, object>
    {
        ["service.name"] = "UpdateHub", // Howto get the service name from the builder().
        ["service.version"] = ServiceVersion.FullVersion(),
        ["deployment.environment"] = ""
    };
}))
  .MinimumLevel.ControlledBy(levelSwitch)
  //.Enrich.FromLogContext()
  //.Enrich.WithCorrelationId()
  // Adds the correlation id to the log and http headers. There is another middleware inside the service
  // which adds that as well, to decouple from the used logging library.
  .Enrich.WithCorrelationIdHeader(CorrelationIdMiddleware.CorrelationIdHeader)  // Add the correlation id to the log and http headers.
  .CreateLogger();
levelSwitch.MinimumLevel = LogEventLevel.Information;

Log.Information("Starting up. Version: {0}", ServiceVersion.FullVersion());

var parser = new Parser();
var applicationConfig = parser.ReadConfig(configFilePath);
Log.Information(parser.ToString());

// Configure web application
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSerilog();
builder.Services.AddHealthChecks();
builder.Services.AddHealthChecks().AddCheck<HealthCheckConfiguration>("Configuration check");
builder.Services.AddHttpContextAccessor();
builder.Services.AddCorrelationIdGenerator();
// Forward if header is available
builder.Services.AddHeaderPropagation(options =>
{
    options.Headers.Add(CorrelationIdMiddleware.CorrelationIdHeader);
});
// Create new if not available
builder.Services.AddHeaderPropagation(options =>
  {
      options.Headers.Add(CorrelationIdMiddleware.CorrelationIdHeader, context =>
      {
          return new StringValues(Guid.NewGuid().ToString());
      });
  });

builder.Services.AddOpenTelemetry()
  .WithMetrics(builder =>
  {
      builder.AddPrometheusExporter();
      builder.AddAspNetCoreInstrumentation();
      builder.AddHttpClientInstrumentation();
      builder.AddMeter("AASBroker", "1.0.0");
      builder.AddMeter("IRSBroker", "1.0.0");

      builder.AddMeter("Microsoft.AspNetCore.Hosting",
        "Microsoft.AspNetCore.Server.Kestrel");
      builder.AddView("http.server.request.duration",
        new ExplicitBucketHistogramConfiguration
        {
            Boundaries = new double[]
          {
          0, 0.005, 0.01, 0.025, 0.05,
          0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10
          }
        });
      if (otlpEndpoint != null)
      {
          builder.AddOtlpExporter(otlpOptions =>
          {
              otlpOptions.Endpoint = new Uri(otlpEndpoint);
          });
      }
      if (enableConsoleExporter)
      {
          builder.AddConsoleExporter();
      }
  }).WithTracing(builder =>
  {
      builder.AddAspNetCoreInstrumentation();
      builder.AddHttpClientInstrumentation();
      if (otlpEndpoint != null)
      {
          builder.AddOtlpExporter(otlpOptions =>
          {
              otlpOptions.Endpoint = new Uri(otlpEndpoint);
          });
      }
      if (enableConsoleExporter)
      {
          builder.AddConsoleExporter();
      }
  })
  .ConfigureResource(resource => resource
    .AddService(serviceName: builder.Environment.ApplicationName, serviceVersion: ServiceVersion.FullVersion()));

builder.Services.AddSingleton(parser.aasServerRepository);
builder.Services.AddSingleton(applicationConfig);
builder.Services.AddScoped<IAasService, AasService>();
builder.Services.AddScoped<IIrsService, IrsService>();
builder.Services.AddProblemDetails();
builder.Services.AddApiVersioning(options =>
  {
      options.DefaultApiVersion = new ApiVersion(1, 0);
      options.AssumeDefaultVersionWhenUnspecified = true;
      options.ReportApiVersions = true;
      options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version")
      );
  })
  .AddMvc(options => { })
  .AddApiExplorer(options =>
  {
      options.GroupNameFormat = "'v'VVV";
      options.SubstituteApiVersionInUrl = true;
  }).EnableApiVersionBinding();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "UpdateHub",
        Version = "v1",
        Description = "Example service to communicate with the different IPS."
    });
   c.SwaggerDoc("v2-earlyaccess", new OpenApiInfo
  {
    Title = "UpdateHub",
    Version = "v2-earlyaccess",
    Description = "Example service to communicate with the different IPS."
  });
    // using System.Reflection;
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});
builder.Services.AddHttpClient(string.Empty).ConfigureHttpClient(c =>
{
    c.DefaultRequestHeaders.Add("User-Agent", "Updatehub/" + ServiceVersion.SemanticVersion());
    c.Timeout = TimeSpan.FromSeconds(10);
}).AddHeaderPropagation();
builder.Services.AddControllers();

var app = builder.Build();
// In development mode raise
if (app.Environment.IsDevelopment())
{
    Log.Information("Set log level to Debug");
    levelSwitch.MinimumLevel = LogEventLevel.Debug;
}

app.MapHealthChecks("/healthz").DisableHttpMetrics();
app.AddCorrelationIdMiddleware();
app.UseHeaderPropagation();
if (Environment.GetEnvironmentVariable("ENABLE_METRIC") == "true")
{
    Log.Information("Metric endpoint active");
    app.MapPrometheusScrapingEndpoint();
}
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "UpdateHub v1");
  c.SwaggerEndpoint("/swagger/v2-earlyaccess/swagger.json", "UpdateHub v2-earlyaccess");
    c.RoutePrefix = string.Empty; // Set Swagger UI at apps root
});
//app.UseExceptionHandler();
//app.UseDeveloperExceptionPage();
app.VersionEndpoint();
var updateGroup = app.NewVersionedApi();


updateGroup.MapGroup("/v{version:apiVersion}").MapGroup("/").HasApiVersion(new ApiVersion(2.0, "earlyaccess")).IdLinkV2Endpoint();
updateGroup.MapGroup("/v{version:apiVersion}").MapGroup("/").HasApiVersion(new ApiVersion(1.0)).IdLinkIrsEndpoint();
updateGroup.MapGroup("/v{version:apiVersion}").MapGroup("/").HasApiVersion(new ApiVersion(1.0)).IdLinkEndpointHandover();
updateGroup.MapGroup("/v{version:apiVersion}").MapGroup("/").HasApiVersion(new ApiVersion(2.0, "earlyaccess")).IdLinkV2EndpointHandover();
updateGroup.MapGroup("/v{version:apiVersion}").MapGroup("/").HasApiVersion(new ApiVersion(2.0, "earlyaccess")).AasqlEndpoints();
updateGroup.MapGroup("/v{version:apiVersion}").MapGroup("/").HasApiVersion(new ApiVersion(1.0)).IdLinkIrsEndpointHandover();

app.MapControllers();
app.Run();
