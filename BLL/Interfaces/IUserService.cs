using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DAL.DTO;
namespace BLL.Interfaces
{
    public  interface IUserService
    {
        Task<IEnumerable<UserListResponse>> GetAllUsersAsync();
        Task<UserDetailResponse?> GetUserByIdAsync(Guid userId);
        Task<UserDetailResponse> UpdateProfileAsync(
            Guid userId,
            UpdateProfileRequest request,
            string? avatarUrl);
        Task DisableUserAsync(Guid userId);

        Task EnableUserAsync(Guid userId);
    }
}
