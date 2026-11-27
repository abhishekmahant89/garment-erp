using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace GarmentERP.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AccountingController : ControllerBase
{
    private readonly IAccountingService _accountingService;

    public AccountingController(IAccountingService accountingService)
    {
        _accountingService = accountingService;
    }

    [HttpGet("trial-balance")]
    public async Task<ActionResult<ApiResponse<TrialBalanceReport>>> GetTrialBalance(CancellationToken ct)
    {
        return Ok(await _accountingService.GetTrialBalanceAsync(ct));
    }
}
