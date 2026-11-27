using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Infrastructure.Services;

public class ExpenseService : IExpenseService
{
    private readonly GarmentDbContextBase _context;

    public ExpenseService(GarmentDbContextBase context)
    {
        _context = context;
    }

    public async Task<ApiResponse<ExpenseDto>> RecordExpenseAsync(CreateExpenseDto dto, CancellationToken ct = default)
    {
        var category = await _context.ExpenseCategories.FindAsync(new object[] { dto.ExpenseCategoryId }, ct);
        if (category == null)
            return ApiResponse<ExpenseDto>.Fail("Expense category not found.");

        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            BranchId = dto.BranchId,
            ExpenseCategoryId = dto.ExpenseCategoryId,
            CashierShiftId = dto.CashierShiftId,
            Amount = dto.Amount,
            PaidTo = dto.PaidTo.Trim(),
            PaymentMode = dto.PaymentMode,
            Remarks = dto.Remarks,
            ExpenseDate = DateTime.UtcNow,
            ReceiptVoucherNumber = $"EXP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}"
        };

        await _context.Expenses.AddAsync(expense, ct);
        await _context.SaveChangesAsync(ct);

        var resultDto = new ExpenseDto(
            expense.Id,
            category.Name,
            expense.Amount,
            expense.PaidTo,
            expense.PaymentMode,
            expense.ExpenseDate,
            expense.Remarks
        );

        return ApiResponse<ExpenseDto>.Ok(resultDto, "Expense recorded successfully.");
    }

    public async Task<ApiResponse<List<ExpenseDto>>> GetDailyExpensesAsync(Guid branchId, DateTime date, CancellationToken ct = default)
    {
        var start = date.Date;
        var end = start.AddDays(1);

        var list = await _context.Expenses
            .Include(e => e.ExpenseCategory)
            .Where(e => e.BranchId == branchId && e.ExpenseDate >= start && e.ExpenseDate < end)
            .OrderByDescending(e => e.ExpenseDate)
            .Select(e => new ExpenseDto(
                e.Id,
                e.ExpenseCategory != null ? e.ExpenseCategory.Name : "General Expense",
                e.Amount,
                e.PaidTo,
                e.PaymentMode,
                e.ExpenseDate,
                e.Remarks
            ))
            .ToListAsync(ct);

        return ApiResponse<List<ExpenseDto>>.Ok(list);
    }
}
