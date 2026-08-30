namespace BLL.Exceptions
{
    public class AppSystemException : AppException
    {
        public AppSystemException(
            string message = "Internal server error",
            string errorCode = "INTERNAL_ERROR",
            IReadOnlyDictionary<string, object?>? parameters = null)
            : base(message, 500, errorCode, parameters)
        {
        }
    }
}
