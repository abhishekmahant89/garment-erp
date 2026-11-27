using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace GarmentERP.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class SalesController : ControllerBase
{
    private readonly IPosBillingService _billingService;

    public SalesController(IPosBillingService billingService)
    {
        _billingService = billingService;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SaleResponseDto>>> ProcessSale([FromBody] CreateSaleRequest request, CancellationToken ct)
    {
        var result = await _billingService.ProcessSaleAsync(request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("hold")]
    public async Task<ActionResult<ApiResponse<Guid>>> HoldBill([FromQuery] string tag, [FromBody] CreateSaleRequest request, CancellationToken ct)
    {
        var result = await _billingService.HoldSaleAsync(tag, request, ct);
        return Ok(result);
    }

    [HttpPost("resume/{id:guid}")]
    public async Task<ActionResult<ApiResponse<CreateSaleRequest>>> ResumeBill(Guid id, CancellationToken ct)
    {
        var result = await _billingService.ResumeSaleAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("held")]
    public async Task<ActionResult<ApiResponse<List<HoldBillDto>>>> GetHeldBills(CancellationToken ct)
    {
        return Ok(await _billingService.GetHeldSalesAsync(ct));
    }

    [HttpPost("shift/open")]
    public async Task<ActionResult<ApiResponse<CashierShift>>> OpenShift(
        [FromQuery] Guid branchId,
        [FromQuery] string counterNo,
        [FromQuery] Guid cashierId,
        [FromQuery] string cashierName,
        [FromQuery] decimal startingFloat,
        CancellationToken ct)
    {
        var result = await _billingService.OpenShiftAsync(branchId, counterNo, cashierId, cashierName, startingFloat, ct);
        return Ok(result);
    }

    [HttpPost("shift/close/{id:guid}")]
    public async Task<ActionResult<ApiResponse<CashierShift>>> CloseShift(
        Guid id,
        [FromQuery] decimal actualCash,
        [FromQuery] string? notes,
        CancellationToken ct)
    {
        var result = await _billingService.CloseShiftAsync(id, actualCash, notes, ct);
        return Ok(result);
    }
}
