namespace BLL.Exceptions
{
    public class ConflictException : AppException
    {
        public ConflictException(
            string message,
            string? errorCode = null,
            IReadOnlyDictionary<string, object?>? parameters = null)
            : base(message, 409, errorCode ?? ErrorCodeCatalog.Resolve(message, "CONFLICT"), parameters)
        {
        }
    }
}
