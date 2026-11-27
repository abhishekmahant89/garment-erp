using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace GarmentERP.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class PurchasesController : ControllerBase
{
    private readonly IPurchaseService _purchaseService;

    public PurchasesController(IPurchaseService purchaseService)
    {
        _purchaseService = purchaseService;
    }

    [HttpPost("suppliers")]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> CreateSupplier([FromBody] CreateSupplierDto dto, CancellationToken ct)
    {
        var result = await _purchaseService.CreateSupplierAsync(dto, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("suppliers")]
    public async Task<ActionResult<ApiResponse<List<SupplierDto>>>> GetSuppliers(CancellationToken ct)
    {
        return Ok(await _purchaseService.GetSuppliersAsync(ct));
    }

    [HttpPost("invoices")]
    public async Task<ActionResult<ApiResponse<PurchaseInvoiceDto>>> CreatePurchaseInvoice([FromBody] CreatePurchaseInvoiceRequest request, CancellationToken ct)
    {
        var result = await _purchaseService.ProcessPurchaseInvoiceAsync(request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("invoices")]
    public async Task<ActionResult<ApiResponse<List<PurchaseInvoiceDto>>>> GetRecentPurchases([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        return Ok(await _purchaseService.GetRecentPurchasesAsync(limit, ct));
    }
}
