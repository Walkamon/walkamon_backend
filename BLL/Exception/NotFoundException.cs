namespace BLL.Exceptions
{
    public class NotFoundException : AppException
    {
        public NotFoundException(string message = "Resource not found")
            : base(message, 404)
        {
        }
    }
}
