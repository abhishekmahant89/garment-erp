using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Infrastructure.Services;

public class AccountingService : IAccountingService
{
    private readonly GarmentDbContextBase _context;

    public AccountingService(GarmentDbContextBase context)
    {
        _context = context;
    }

    public async Task PostSaleJournalAsync(Sale sale, CancellationToken ct = default)
    {
        // Prevent duplicate journal postings for the same invoice
        if (await _context.JournalVouchers.AnyAsync(v => v.ReferenceNumber == sale.InvoiceNumber, ct))
        {
            return;
        }

        var accounts = await _context.LedgerAccounts.ToDictionaryAsync(a => a.AccountCode, ct);

        // Account Lookups
        var accCash = accounts.GetValueOrDefault("1010");
        var accUpi = accounts.GetValueOrDefault("1020");
        var accCard = accounts.GetValueOrDefault("1030");
        var accCgst = accounts.GetValueOrDefault("2020");
        var accSgst = accounts.GetValueOrDefault("2030");
        var accIgst = accounts.GetValueOrDefault("2040");
        var accSales = accounts.GetValueOrDefault("4010");

        if (accCash == null || accSales == null)
            return; // System accounts must be seeded

        var jvNumber = $"JV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";
        var voucher = new JournalVoucher
        {
            Id = Guid.NewGuid(),
            VoucherNumber = jvNumber,
            VoucherDate = DateTime.UtcNow,
            ReferenceNumber = sale.InvoiceNumber,
            Description = $"Automated POS Sales Posting: {sale.InvoiceNumber}",
            TotalDebit = 0,
            TotalCredit = 0
        };

        var entries = new List<VoucherEntry>();

        // 1. DEBITS: Tender Received / Cash Debits
        var payments = await _context.Payments.Where(p => p.SaleId == sale.Id).ToListAsync(ct);
        decimal paidSum = payments.Sum(p => p.Amount);

        if (payments.Any())
        {
            foreach (var p in payments)
            {
                var targetAcc = p.Method switch
                {
                    PaymentMethod.UPI => accUpi ?? accCash,
                    PaymentMethod.Card => accCard ?? accCash,
                    _ => accCash
                };

                entries.Add(new VoucherEntry
                {
                    Id = Guid.NewGuid(),
                    JournalVoucherId = voucher.Id,
                    LedgerAccountId = targetAcc.Id,
                    DebitAmount = p.Amount,
                    CreditAmount = 0,
                    LineNarration = $"{p.Method} Payment"
                });
                targetAcc.CurrentBalance += p.Amount;
            }

            if (paidSum < sale.TotalAmount)
            {
                var diff = sale.TotalAmount - paidSum;
                entries.Add(new VoucherEntry
                {
                    Id = Guid.NewGuid(),
                    JournalVoucherId = voucher.Id,
                    LedgerAccountId = accCash.Id,
                    DebitAmount = diff,
                    CreditAmount = 0,
                    LineNarration = "Balance Cash Tendered"
                });
                accCash.CurrentBalance += diff;
            }
        }
        else
        {
            entries.Add(new VoucherEntry
            {
                Id = Guid.NewGuid(),
                JournalVoucherId = voucher.Id,
                LedgerAccountId = accCash.Id,
                DebitAmount = sale.TotalAmount,
                CreditAmount = 0,
                LineNarration = "Cash Received"
            });
            accCash.CurrentBalance += sale.TotalAmount;
        }

        // 2. CREDITS: Sales Revenue (Taxable amount)
        decimal taxableSales = sale.SubTotal - sale.DiscountAmount;
        entries.Add(new VoucherEntry
        {
            Id = Guid.NewGuid(),
            JournalVoucherId = voucher.Id,
            LedgerAccountId = accSales.Id,
            DebitAmount = 0,
            CreditAmount = taxableSales,
            LineNarration = "Retail Apparel Sales Revenue"
        });
        accSales.CurrentBalance += taxableSales;

        // 3. CREDITS: Tax Payable
        if (sale.CgstAmount > 0 && accCgst != null)
        {
            entries.Add(new VoucherEntry
            {
                Id = Guid.NewGuid(),
                JournalVoucherId = voucher.Id,
                LedgerAccountId = accCgst.Id,
                DebitAmount = 0,
                CreditAmount = sale.CgstAmount,
                LineNarration = "Output CGST Payable"
            });
            accCgst.CurrentBalance += sale.CgstAmount;
        }

        if (sale.SgstAmount > 0 && accSgst != null)
        {
            entries.Add(new VoucherEntry
            {
                Id = Guid.NewGuid(),
                JournalVoucherId = voucher.Id,
                LedgerAccountId = accSgst.Id,
                DebitAmount = 0,
                CreditAmount = sale.SgstAmount,
                LineNarration = "Output SGST Payable"
            });
            accSgst.CurrentBalance += sale.SgstAmount;
        }

        if (sale.IgstAmount > 0 && accIgst != null)
        {
            entries.Add(new VoucherEntry
            {
                Id = Guid.NewGuid(),
                JournalVoucherId = voucher.Id,
                LedgerAccountId = accIgst.Id,
                DebitAmount = 0,
                CreditAmount = sale.IgstAmount,
                LineNarration = "Output IGST Payable (Inter-State)"
            });
            accIgst.CurrentBalance += sale.IgstAmount;
        }

        // 4. Round Off adjustment
        if (sale.RoundOff != 0)
        {
            if (sale.RoundOff > 0)
            {
                entries.Add(new VoucherEntry
                {
                    Id = Guid.NewGuid(),
                    JournalVoucherId = voucher.Id,
                    LedgerAccountId = accSales.Id,
                    DebitAmount = 0,
                    CreditAmount = sale.RoundOff,
                    LineNarration = "Round Off Gain"
                });
                accSales.CurrentBalance += sale.RoundOff;
            }
            else
            {
                entries.Add(new VoucherEntry
                {
                    Id = Guid.NewGuid(),
                    JournalVoucherId = voucher.Id,
                    LedgerAccountId = accSales.Id,
                    DebitAmount = Math.Abs(sale.RoundOff),
                    CreditAmount = 0,
                    LineNarration = "Round Off Loss"
                });
                accSales.CurrentBalance -= Math.Abs(sale.RoundOff);
            }
        }

        voucher.TotalDebit = entries.Sum(e => e.DebitAmount);
        voucher.TotalCredit = entries.Sum(e => e.CreditAmount);

        await _context.JournalVouchers.AddAsync(voucher, ct);
        await _context.VoucherEntries.AddRangeAsync(entries, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<ApiResponse<TrialBalanceReport>> GetTrialBalanceAsync(CancellationToken ct = default)
    {
        var accounts = await _context.LedgerAccounts.OrderBy(a => a.AccountCode).ToListAsync(ct);
        var rows = new List<TrialBalanceRow>();

        decimal totalDebits = 0;
        decimal totalCredits = 0;

        foreach (var acc in accounts)
        {
            decimal debit = 0;
            decimal credit = 0;

            if (acc.Type == AccountType.Asset || acc.Type == AccountType.Expense)
            {
                debit = acc.CurrentBalance >= 0 ? acc.CurrentBalance : 0;
                credit = acc.CurrentBalance < 0 ? Math.Abs(acc.CurrentBalance) : 0;
            }
            else
            {
                credit = acc.CurrentBalance >= 0 ? acc.CurrentBalance : 0;
                debit = acc.CurrentBalance < 0 ? Math.Abs(acc.CurrentBalance) : 0;
            }

            totalDebits += debit;
            totalCredits += credit;

            rows.Add(new TrialBalanceRow(
                acc.AccountCode,
                acc.AccountName,
                acc.Type,
                debit,
                credit
            ));
        }

        var report = new TrialBalanceReport(
            DateTime.UtcNow,
            rows,
            totalDebits,
            totalCredits,
            IsBalanced: Math.Abs(totalDebits - totalCredits) < 0.05m
        );

        return ApiResponse<TrialBalanceReport>.Ok(report);
    }
}
