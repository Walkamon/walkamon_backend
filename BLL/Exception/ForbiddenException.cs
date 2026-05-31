namespace BLL.Exception
{
    public class ForbiddenException : AppException
    {
        public ForbiddenException(string message = "Forbidden")
            : base(message, 403)
        {
        }
    }
}
