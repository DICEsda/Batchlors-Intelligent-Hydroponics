using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace IoT.Backend.Middleware;

/// <summary>
/// API key authentication middleware. Validates the <c>X-API-Key</c> request header
/// against the value configured in <c>ApiSecurity:ApiKey</c>.
/// <para>
/// When no key is configured (null or empty), authentication is skipped entirely,
/// preserving backward compatibility and enabling a frictionless dev-mode experience.
/// </para>
/// <para>
/// Certain infrastructure paths (<c>/health</c>, <c>/alive</c>, <c>/ws</c>,
/// <c>/swagger</c>) are always exempt from authentication.
/// </para>
/// </summary>
public sealed class ApiKeyMiddleware
{
    private const string ApiKeyHeaderName = "X-API-Key";

    private static readonly string[] ExemptPrefixes =
    [
        "/health",
        "/alive",
        "/ws",
        "/swagger"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    private readonly RequestDelegate _next;
    private readonly string? _expectedKey;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _expectedKey = configuration["ApiSecurity:ApiKey"];
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // If no API key is configured, skip authentication entirely (dev mode / backward compat).
        if (string.IsNullOrEmpty(_expectedKey))
        {
            await _next(context);
            return;
        }

        // Check if the request path is exempt from authentication.
        var path = context.Request.Path.Value ?? string.Empty;
        if (IsExemptPath(path))
        {
            await _next(context);
            return;
        }

        // Validate the X-API-Key header.
        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey)
            || !string.Equals(providedKey, _expectedKey, StringComparison.Ordinal))
        {
            Log.Warning(
                "Unauthorized API request to {Path} from {RemoteIp}",
                path,
                context.Connection.RemoteIpAddress);

            await WriteUnauthorizedResponseAsync(context, path);
            return;
        }

        await _next(context);
    }

    private static bool IsExemptPath(string path)
    {
        foreach (var prefix in ExemptPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static async Task WriteUnauthorizedResponseAsync(HttpContext context, string path)
    {
        var problem = new ProblemDetails
        {
            Type = "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.2",
            Title = "Unauthorized",
            Status = StatusCodes.Status401Unauthorized,
            Detail = "Missing or invalid API key. Provide a valid key via the X-API-Key header.",
            Instance = path
        };

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, JsonOptions));
    }
}
