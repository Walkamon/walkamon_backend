
namespace BLL.Exceptions
{
    public class BadRequestException : AppException
    {
        public BadRequestException(
            string message,
            string? errorCode = null,
            IReadOnlyDictionary<string, object?>? parameters = null)
            : base(message, 400, errorCode ?? ErrorCodeCatalog.Resolve(message, "BAD_REQUEST"), parameters)
        {
        }
    }
}
