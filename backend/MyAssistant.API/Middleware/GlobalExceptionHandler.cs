using System.Net;
using System.Text.Json;
using MyAssistant.Application.Common;

namespace MyAssistant.API.Middleware;

public class GlobalExceptionHandler
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(RequestDelegate next, ILogger<GlobalExceptionHandler> logger)
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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = HttpStatusCode.InternalServerError;
        object response;

        switch (exception)
        {
            case NotFoundException:
                statusCode = HttpStatusCode.NotFound;
                break;
            case UnauthorizedException:
                statusCode = HttpStatusCode.Unauthorized;
                break;
            case ForbiddenException:
                statusCode = HttpStatusCode.Forbidden;
                break;
            case ConflictException:
                statusCode = HttpStatusCode.Conflict;
                break;
            case ValidationException:
                statusCode = HttpStatusCode.BadRequest;
                break;
            case AppException appException:
                statusCode = (HttpStatusCode)appException.StatusCode;
                break;
            default:
                _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
                break;
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        response = new ApiResponse<object?>
        {
            Success = false,
            Message = exception switch
            {
                NotFoundException => exception.Message,
                UnauthorizedException => exception.Message,
                ForbiddenException => exception.Message,
                ConflictException => exception.Message,
                ValidationException v => string.Join("; ", v.Errors),
                AppException => exception.Message,
                _ => "An unexpected error occurred."
            }
        };

        if (exception is ValidationException val)
        {
            response = new ApiResponse<object?>
            {
                Success = false,
                Message = "Validation failed.",
                Errors = val.Errors
            };
        }

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await context.Response.WriteAsync(json);
    }
}
