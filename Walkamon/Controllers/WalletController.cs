using BLL.Exceptions;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers;

[ApiController]
[Authorize(Roles = "User")]
[Route("api/wallet")]
public class WalletController : BaseController
{
    private readonly IGenericRepository<Wallet> _walletRepository;

    public WalletController(IGenericRepository<Wallet> walletRepository)
    {
        _walletRepository = walletRepository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<WalletBalanceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBalance()
    {
        var wallet = await _walletRepository.GetByIdAsync(CurrentUserId);
        if (wallet == null)
        {
            throw new NotFoundException("Wallet not found");
        }

        return Ok(new ApiResponse<WalletBalanceResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get wallet balance success",
            Data = new WalletBalanceResponse
            {
                Balance = wallet.Balance
            }
        });
    }
}
