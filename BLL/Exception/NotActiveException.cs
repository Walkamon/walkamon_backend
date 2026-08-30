using BLL.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Exceptions
{
    public class NotActiveException : AppException
    {
        public object DataObject { get; }

        public NotActiveException(Guid? requestCode)
            : base(
                "Account is not activated",
                400,
                "AUTH_ACCOUNT_NOT_ACTIVE",
                new Dictionary<string, object?> { ["requestCode"] = requestCode })
        {
            DataObject = new
            {
                RequestCode = requestCode
            };
        }
    }
}
