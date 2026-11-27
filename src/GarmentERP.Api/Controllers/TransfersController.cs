using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace GarmentERP.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class TransfersController : ControllerBase
{
    private readonly IStockTransferService _transferService;

    public TransfersController(IStockTransferService transferService)
    {
        _transferService = transferService;
    }

    [HttpPost("dispatch")]
    public async Task<ActionResult<ApiResponse<StockTransferDto>>> DispatchTransfer([FromBody] CreateStockTransferRequest request, CancellationToken ct)
    {
        var result = await _transferService.CreateTransferDispatchAsync(request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("receive/{id:guid}")]
    public async Task<ActionResult<ApiResponse<StockTransferDto>>> ReceiveTransfer(Guid id, [FromQuery] string receivedBy, CancellationToken ct)
    {
        var result = await _transferService.ConfirmTransferReceiveAsync(id, receivedBy, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("pending")]
    public async Task<ActionResult<ApiResponse<List<StockTransferDto>>>> GetPendingTransfers(CancellationToken ct)
    {
        return Ok(await _transferService.GetPendingTransfersAsync(ct));
    }
}
