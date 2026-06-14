using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class CreateUserFeedbackRequest
    {
        public string FeedbackTypeCode { get; set; } = null!;

        public string Content { get; set; } = null!;
    }
}
