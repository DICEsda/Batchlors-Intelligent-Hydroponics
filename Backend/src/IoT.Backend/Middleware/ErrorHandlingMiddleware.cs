using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace IoT.Backend.Middleware;

/// <summary>
/// Global error handling middleware that catches unhandled exceptions and returns
/// RFC 9110-compliant ProblemDetails responses with correlation IDs.
/// Must be registered as the first middleware in the pipeline.
/// </summary>
public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public ErrorHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.TraceIdentifier;
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            Log.Error(ex,
                "Unhandled exception on {Method} {Path} [CorrelationId={CorrelationId}]",
                context.Request.Method,
                context.Request.Path,
                correlationId);

            await WriteErrorResponseAsync(context, ex, correlationId);
        }
    }

    private static async Task WriteErrorResponseAsync(
        HttpContext context,
        Exception exception,
        string correlationId)
    {
        // Avoid writing to a response that has already started streaming.
        if (context.Response.HasStarted)
        {
            Log.Warning(
                "Response already started; cannot write ProblemDetails for [CorrelationId={CorrelationId}]",
                correlationId);
            return;
        }

        var env = context.RequestServices.GetRequiredService<IHostEnvironment>();
        var detail = env.IsDevelopment()
            ? exception.ToString()
            : "An internal error occurred. Please reference the correlation_id when contacting support.";

        var problem = new ProblemDetails
        {
            Type = "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1",
            Title = "An unexpected error occurred",
            Status = StatusCodes.Status500InternalServerError,
            Detail = detail,
            Instance = context.Request.Path,
            Extensions =
            {
                ["correlation_id"] = correlationId
            }
        };

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, JsonOptions));
    }
}
