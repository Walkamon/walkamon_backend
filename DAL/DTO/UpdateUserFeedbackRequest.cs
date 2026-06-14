using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class UpdateUserFeedbackRequest
    {
        public string StatusCode { get; set; } = null!;

        public string? AdminNote { get; set; }
    }
}
