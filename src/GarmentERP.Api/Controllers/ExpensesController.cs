using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace GarmentERP.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public ExpensesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ExpenseDto>>> RecordExpense([FromBody] CreateExpenseDto dto, CancellationToken ct)
    {
        var result = await _expenseService.RecordExpenseAsync(dto, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ExpenseDto>>>> GetExpenses([FromQuery] Guid branchId, [FromQuery] DateTime? date, CancellationToken ct)
    {
        var targetDate = date ?? DateTime.UtcNow;
        return Ok(await _expenseService.GetDailyExpensesAsync(branchId, targetDate, ct));
    }
}
