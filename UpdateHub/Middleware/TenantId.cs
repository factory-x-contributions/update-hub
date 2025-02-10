using System.Diagnostics;
using Serilog;
using Serilog.Context;
using ILogger = Serilog.ILogger;

namespace UpdateHub.Middleware;

public class TenantIdMiddleware
{
  public static string TenantIdHeader { get; } = _tenantIdHeader;
  private const string _tenantIdHeader = "xo-cdm-tenant-id";

  private readonly RequestDelegate _next;

  private ActivitySource activitySource;

  public TenantIdMiddleware(RequestDelegate next) => _next = next;

  public async Task InvokeAsync(HttpContext context)
  {
    // Check before starting, since the other middleware may also add the header.
    context.Request.Headers.TryGetValue(_tenantIdHeader, out var tenantId);
    // Inject tenant id into the log context
    Activity.Current?.SetTag("tenant.id", tenantId);
    await _next(context);
  }
}

public static partial class ApplicationBuilderExtensions
{
  public static IApplicationBuilder AddTenantIdMiddleware(this IApplicationBuilder applicationBuilder)
    => applicationBuilder.UseMiddleware<TenantIdMiddleware>();
}
