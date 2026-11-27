using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace GarmentERP.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IStaffAuthService _authService;

    public AuthController(IStaffAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<StaffLoginResponse>> Login([FromBody] StaffLoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [HttpPost("staff")]
    public async Task<ActionResult<ApiResponse<StaffUserDto>>> CreateStaff([FromBody] CreateStaffUserRequest request, CancellationToken ct)
    {
        var result = await _authService.CreateStaffAsync(request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("staff")]
    public async Task<ActionResult<ApiResponse<List<StaffUserDto>>>> GetStaff([FromQuery] Guid branchId, CancellationToken ct)
    {
        return Ok(await _authService.GetStaffMembersAsync(branchId, ct));
    }
}
