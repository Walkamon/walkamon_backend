using System.Text.Json;
using DAL.DTO;
using BLL.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly JsonSerializerOptions _serializerOptions;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IOptions<JsonOptions> jsonOptions)
    {
        _next = next;
        _logger = logger;
        _serializerOptions = jsonOptions.Value.JsonSerializerOptions;
    }

   
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request failed");
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        int statusCode;
        string message;
        string errorCode;
        IReadOnlyDictionary<string, object?> parameters;

        if (ex is AppException appEx)
        {
            statusCode = appEx.StatusCode;
            message = appEx.Message;
            errorCode = appEx.ErrorCode;
            parameters = appEx.Parameters;
        }
        else if (ex is DbUpdateConcurrencyException ||
                 ex is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 or 1205 } } ||
                 ex is SqlException { Number: 2601 or 2627 or 1205 })
        {
            statusCode = StatusCodes.Status409Conflict;
            message = "The request conflicted with another operation. Refresh state and retry safely.";
            errorCode = "CONCURRENCY_CONFLICT";
            parameters = new Dictionary<string, object?>();
        }
        else
        {
            statusCode = 500;
            message = "Internal Server Error";
            errorCode = "INTERNAL_ERROR";
            parameters = new Dictionary<string, object?>();
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        object? data = null;
        if (ex is TooManyRequestsException tooManyRequestsException)
        {
            context.Response.Headers.RetryAfter =
                tooManyRequestsException.RetryAfterSeconds.ToString();
            data = new
            {
                retryAfterSeconds = tooManyRequestsException.RetryAfterSeconds
            };
        }
        if (ex is NotActiveException notActiveException)
        {
            data = notActiveException.DataObject;
        }
        var response = new ApiResponse<object>
        {
            Success = false,
            Status = statusCode,
            Message = message,
            ErrorCode = errorCode,
            Params = parameters,
            Data = data,
            TraceId = context.TraceIdentifier
        };

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(response, _serializerOptions)
        );
    }
}
