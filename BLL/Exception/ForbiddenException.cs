namespace BLL.Exceptions
{
    public class ForbiddenException : AppException
    {
        public ForbiddenException(
            string message = "Forbidden",
            string? errorCode = null,
            IReadOnlyDictionary<string, object?>? parameters = null)
            : base(message, 403, errorCode ?? ErrorCodeCatalog.Resolve(message, "FORBIDDEN"), parameters)
        {
        }
    }
}
