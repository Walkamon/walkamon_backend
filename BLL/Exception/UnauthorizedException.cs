namespace BLL.Exceptions
{
    public class UnauthorizedException : AppException
    {
        public UnauthorizedException(
            string message = "Unauthorized",
            string? errorCode = null,
            IReadOnlyDictionary<string, object?>? parameters = null)
            : base(message, 401, errorCode ?? ErrorCodeCatalog.Resolve(message, "UNAUTHORIZED"), parameters)
        {
        }
    }
}
