using System.Text.Json;
using DAL.DTO;
using BLL.Exceptions;
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
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

        var response = new ApiResponse<object>
        {
            Success = false,
            Status = statusCode,
            Message = message,
            TraceId = context.TraceIdentifier
        };

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(response)
        );
    }
}