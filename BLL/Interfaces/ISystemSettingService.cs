using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DAL.DTO;
namespace BLL.Interfaces
{
    public interface ISystemSettingService
    {
        Task<StepExpRateResponse> GetStepExpRateAsync();

        Task UpdateStepExpRateAsync(UpdateStepExpRateRequest request);
        Task<PetStatusSettingResponse> GetPetStatusSettingAsync();

        Task UpdatePetStatusSettingAsync(UpdatePetStatusSettingRequest request);
    }
}
