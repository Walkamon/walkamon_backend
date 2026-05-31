namespace BLL.Exception
{
    public class AppSystemException : AppException
    {
        public AppSystemException(string message = "Internal server error")
            : base(message, 500)
        {
        }
    }
}
