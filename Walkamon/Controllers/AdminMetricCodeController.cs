using BLL.Service;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/metric-codes")]
public class AdminMetricCodeController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AdminMetricCodeResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public IActionResult GetMetricCodes()
    {
        return Ok(new ApiResponse<IReadOnlyList<AdminMetricCodeResponse>>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get metric codes success",
            Data = MissionMetricCodeCatalog.GetAll()
        });
    }
}
