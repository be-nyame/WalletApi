using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace WalletApi.API.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception for {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (status, message) = ex switch
        {
            // 400 — bad input or business rule violation
            ArgumentException => (HttpStatusCode.BadRequest, ex.Message),
            InvalidOperationException => (HttpStatusCode.BadRequest, ex.Message),

            // 401 — authentication failures
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, ex.Message),

            // 404 — resource not found
            KeyNotFoundException => (HttpStatusCode.NotFound, ex.Message),

            // 409 — concurrency conflict
            DbUpdateConcurrencyException => (HttpStatusCode.Conflict, 
                "The resource was modified by another request. Please retry."),

            // 422 — database constraint violations (duplicate email, etc.)
            DbUpdateException => (HttpStatusCode.UnprocessableEntity,
                "A database error occurred. The request could not be completed."),

            // 503 — downstream service unavailable
            TimeoutException => (HttpStatusCode.ServiceUnavailable,
                "The request timed out. Please try again."),

            // 500 — everything else
            _ => (HttpStatusCode.InternalServerError,
                 "An unexpected error occurred.")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)status;

        var body = JsonSerializer.Serialize(new
        {
            error = message,
            status = (int)status,
            path = context.Request.Path.Value,
            traceId = context.TraceIdentifier
        });

        return context.Response.WriteAsync(body);
    }
}