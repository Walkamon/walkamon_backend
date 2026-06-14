using DAL.DTO;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Validators
{
    public class SendFriendRequestRequestValidator
     : AbstractValidator<SendFriendRequestRequest>
    {
        public SendFriendRequestRequestValidator()
        {
            RuleFor(x => x.ReceiverUserId)
                .NotEmpty()
                .WithMessage("Receiver user id is required.");
        }
    }
}
