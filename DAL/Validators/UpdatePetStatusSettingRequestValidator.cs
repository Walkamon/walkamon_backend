using DAL.DTO;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Validators
{
    public class UpdatePetStatusSettingRequestValidator
     : AbstractValidator<UpdatePetStatusSettingRequest>
    {
        public UpdatePetStatusSettingRequestValidator()
        {
            RuleFor(x => x.EnergyRecoverPerMinute)
                .GreaterThanOrEqualTo(0)
                .LessThanOrEqualTo(100);

            RuleFor(x => x.BondDecreasePerMinute)
                .GreaterThanOrEqualTo(0)
                .LessThanOrEqualTo(100);

            RuleFor(x => x.LifeForceDecreasePerMinute)
                .GreaterThanOrEqualTo(0)
                .LessThanOrEqualTo(100);
        }
    }
}
