// Feature flag to transition to controller-based API approach

using System.Diagnostics;
using System.Reflection;
using Microsoft.OpenApi.Models;
using UpdateHub;
using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;
using UpdateHub.Configuration;
using UpdateHub.Domain;
using UpdateHub.Endpoints;

using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
  .WriteTo.Console()
  .MinimumLevel.Debug()
  .CreateLogger();

Log.Information("Starting up. Version: {0}.{1}.{2} Commit: {3}", GitHash.major, GitHash.minor, GitHash.patch,GitHash.Value);


var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
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
builder.Services.AddHttpClient(string.Empty).ConfigureHttpClient(c => {
  c.DefaultRequestHeaders.Add("User-Agent", "Updatehub/" + GitHash.major + "." + GitHash.minor + "." + GitHash.patch);
  c.Timeout = TimeSpan.FromSeconds(10);
});


builder.Services.AddAasModelFetcherService(); // TODO: Remove with update/idlink1
builder.Services.AddAasServerService();
var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

app.VersionEndpoint();
app.Idlink();

app.Run();
