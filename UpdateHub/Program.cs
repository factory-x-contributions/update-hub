// Feature flag to transition to controller-based API approach

using System.Reflection;
using Microsoft.OpenApi.Models;
using UpdateHub;
using UpdateHub.Configuration;
using UpdateHub.Domain;
using UpdateHub.Endpoints;

using Serilog;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using System;
using System.IO.Enumeration;

var CONFIG_FILE_PATH = Environment.GetEnvironmentVariable("CONFIG_FILE_PATH") ?? "./config.yaml";

// Configure logging
Log.Logger = new LoggerConfiguration()
  .WriteTo.Console()
  .MinimumLevel.Debug()
  .CreateLogger();

Log.Information("Starting up. Version: {0}.{1}.{2} Commit: {3}", GitHash.major, GitHash.minor, GitHash.patch,GitHash.Value);

var parser = new Parser();
parser.ReadConfig(CONFIG_FILE_PATH);
Log.Debug(parser.ToString());

// Configure web application
var builder = WebApplication.CreateBuilder(args);
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
builder.Services.AddHttpClient(string.Empty).ConfigureHttpClient(c => {
  c.DefaultRequestHeaders.Add("User-Agent", "Updatehub/" + GitHash.major + "." + GitHash.minor + "." + GitHash.patch);
  c.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

app.VersionEndpoint();
app.Idlink();

app.Run();
