namespace BLL.Exceptions;

public class TooManyRequestsException : AppException
{
    public TooManyRequestsException(string message, int retryAfterSeconds)
        : base(message, 429)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }

    public int RetryAfterSeconds { get; }
}
