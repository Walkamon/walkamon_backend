using System.Text.Json;
using DAL.DTO;
using BLL.Exceptions;
public class ExceptionMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

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
            _logger.LogError(ex, "Request failed");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        int statusCode;
        string message;

        if (ex is AppException appEx)
        {
            statusCode = appEx.StatusCode;
            message = appEx.Message;
        }
        else
        {
            statusCode = 500;
            message = "Internal Server Error";
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

        var response = new ApiResponse<object>
        {
            Success = false,
            Status = statusCode,
            Message = message,
            Data = data,
            TraceId = context.TraceIdentifier
        };

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(response, SerializerOptions)
        );
    }
}
