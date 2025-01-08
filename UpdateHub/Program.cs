using System.Reflection;
using Microsoft.OpenApi.Models;
using UpdateHub.Configuration;
using UpdateHub.Endpoints;
using Serilog;
using OpenTelemetry.Metrics;
using Serilog.Core;
using Serilog.Events;
using UpdateHub.Version;

var CONFIG_FILE_PATH = Environment.GetEnvironmentVariable("CONFIG_FILE_PATH") ?? "./config.yaml";

// Configure logging
var levelSwitch = new LoggingLevelSwitch();
Log.Logger = new LoggerConfiguration()
  .WriteTo.Console()
  .MinimumLevel.ControlledBy(levelSwitch)
  .CreateLogger();
levelSwitch.MinimumLevel = LogEventLevel.Information;

Log.Information("Starting up. Version: {0}", ServiceVersion.FullVersion());

var parser = new Parser();
parser.ReadConfig(CONFIG_FILE_PATH);
Log.Debug(parser.ToString());

// Configure web application
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSerilog();
builder.Services.AddHealthChecks();
builder.Services.AddOpenTelemetry()
  .WithMetrics(builder =>
  {
    builder.AddPrometheusExporter();
    builder.AddAspNetCoreInstrumentation();

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
  });

builder.Services.AddSingleton(parser.aasServerRepository);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
  c.SwaggerDoc("v1", new OpenApiInfo
  {
    Title = "UpdateHub", Version = "v1",
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
});

var app = builder.Build();
// In development mode raise
if (app.Environment.IsDevelopment())
{
  Log.Information("Set log level to Debug");
  levelSwitch.MinimumLevel = LogEventLevel.Debug;
}

app.MapHealthChecks("/healthz").DisableHttpMetrics();

if (Environment.GetEnvironmentVariable("ENABLE_METRIC") == "true")
{
  Log.Information("Metric endpoint active");
  app.MapPrometheusScrapingEndpoint();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
  c.SwaggerEndpoint("/swagger/v1/swagger.json", "UpdateHub v1");
  c.RoutePrefix = string.Empty; // Set Swagger UI at apps root
});

app.VersionEndpoint();
app.IdLinkEndpoint();

app.Run();
