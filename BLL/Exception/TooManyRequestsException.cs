namespace BLL.Exceptions;

public class TooManyRequestsException : AppException
{
    public TooManyRequestsException(
        string message,
        int retryAfterSeconds,
        string errorCode = "TOO_MANY_REQUESTS",
        IReadOnlyDictionary<string, object?>? parameters = null)
        : base(
            message,
            429,
            errorCode,
            MergeRetryAfter(parameters, retryAfterSeconds))
    {
        RetryAfterSeconds = retryAfterSeconds;
    }

    public int RetryAfterSeconds { get; }

    private static IReadOnlyDictionary<string, object?> MergeRetryAfter(
        IReadOnlyDictionary<string, object?>? parameters,
        int retryAfterSeconds)
    {
        var result = parameters == null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(parameters);
        result["retryAfterSeconds"] = retryAfterSeconds;
        return result;
    }
}
