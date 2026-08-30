namespace BLL.Exceptions
{
    public abstract class AppException : Exception
    {
        public int StatusCode { get; }
        public string ErrorCode { get; }
        public IReadOnlyDictionary<string, object?> Parameters { get; }

        protected AppException(
            string message,
            int statusCode,
            string errorCode,
            IReadOnlyDictionary<string, object?>? parameters = null)
            : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
            Parameters = parameters ?? new Dictionary<string, object?>();
        }
    }
}
