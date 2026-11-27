using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace GarmentERP.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class PromotionsController : ControllerBase
{
    private readonly IPromotionEngine _promotionEngine;

    public PromotionsController(IPromotionEngine promotionEngine)
    {
        _promotionEngine = promotionEngine;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Promotion>>> Create([FromBody] CreatePromotionDto dto, CancellationToken ct)
    {
        var result = await _promotionEngine.CreatePromotionAsync(dto, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("active")]
    public async Task<ActionResult<ApiResponse<List<Promotion>>>> GetActive(CancellationToken ct)
    {
        return Ok(await _promotionEngine.GetActivePromotionsAsync(ct));
    }
}
