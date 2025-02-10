using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Primitives;
using Serilog;

namespace UpdateHub.Middleware;

public class CorrelationIdGenerator : ICorrelationIdGenerator
{
  private string _correlationId = Guid.NewGuid().ToString();

  public string Get() => _correlationId;

  public void Set(string correlationId) => _correlationId = correlationId;
}

public interface ICorrelationIdGenerator
{
  string Get();
  void Set(string correlationId);
}
public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddCorrelationIdGenerator(this IServiceCollection services)
  {
    services.AddScoped<ICorrelationIdGenerator, CorrelationIdGenerator>();

    return services;
  }
}

// Middleware
public class CorrelationIdMiddleware
{
  public static string CorrelationIdHeader { get; } = _correlationIdHeader;


  private readonly RequestDelegate _next;
  private const string _correlationIdHeader = "X-Correlation-Id";

  public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

  public async Task Invoke(HttpContext context, ICorrelationIdGenerator correlationIdGenerator)
  {

    var correlationId = GetCorrelationId(context, correlationIdGenerator);
    AddCorrelationIdHeaderToResponse(context, correlationId);
    await _next(context);
  }

  private static StringValues GetCorrelationId(HttpContext context, ICorrelationIdGenerator correlationIdGenerator)
  {

    // serilog injects its own header into the response. We just, set the id to the generator if found
    if(context.Response.Headers.TryGetValue(_correlationIdHeader, out var existingIdFromResponse));
      correlationIdGenerator.Set(existingIdFromResponse);

    if (context.Request.Headers.TryGetValue(_correlationIdHeader, out var correlationId))
    {
      correlationIdGenerator.Set(correlationId);
      return correlationId;
    }
    else
    {
      var id = correlationIdGenerator.Get();
      // TODO: Set value, if request has no one, to get HTTP Header Propagation working
      context.Request.Headers.Add(_correlationIdHeader, new[] { id});
      return id; // correlationIdGenerator.Get();
    }
  }

  private static void AddCorrelationIdHeaderToResponse(HttpContext context, StringValues correlationId)
    => context.Response.OnStarting(() =>
    {
      // Check before starting, since the other middleware may also add the header.
      if (!context.Response.Headers.TryGetValue(_correlationIdHeader, out var correlationIdExisting))
        context.Response.Headers.Add(_correlationIdHeader, new[] { correlationId.ToString() });

      return Task.CompletedTask;
    });
}

public static partial class ApplicationBuilderExtensions
{
  public static IApplicationBuilder AddCorrelationIdMiddleware(this IApplicationBuilder applicationBuilder)
    => applicationBuilder.UseMiddleware<CorrelationIdMiddleware>();
}
