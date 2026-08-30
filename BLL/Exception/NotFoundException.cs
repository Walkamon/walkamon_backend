namespace BLL.Exceptions
{
    public class NotFoundException : AppException
    {
        public NotFoundException(
            string message = "Resource not found",
            string? errorCode = null,
            IReadOnlyDictionary<string, object?>? parameters = null)
            : base(message, 404, errorCode ?? ErrorCodeCatalog.Resolve(message, "NOT_FOUND"), parameters)
        {
        }
    }
}
