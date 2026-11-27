using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Infrastructure.Services;

public class LoyaltyService : ILoyaltyService
{
    private readonly GarmentDbContextBase _context;

    public LoyaltyService(GarmentDbContextBase context)
    {
        _context = context;
    }

    public async Task<ApiResponse<LoyaltyAccrualResult>> AccruePointsAsync(Guid customerId, Guid? saleId, decimal eligibleSpend, CancellationToken ct = default)
    {
        var customer = await _context.Customers.FindAsync(new object[] { customerId }, ct);
        if (customer == null)
        {
            return ApiResponse<LoyaltyAccrualResult>.Fail("Customer not found.");
        }

        // Rule: 1 Loyalty Point for every Rs. 100 spent
        int pointsEarned = (int)Math.Floor(eligibleSpend / 100m);
        if (pointsEarned <= 0)
        {
            return ApiResponse<LoyaltyAccrualResult>.Ok(new LoyaltyAccrualResult(0, customer.LoyaltyPoints, 0), "No loyalty points earned for this amount.");
        }

        customer.LoyaltyPoints += pointsEarned;

        var transaction = new LoyaltyTransaction
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            SaleId = saleId,
            TransactionType = LoyaltyTransactionType.Earned,
            Points = pointsEarned,
            EquivalentAmount = pointsEarned, // 1 point = Rs. 1
            Description = $"Earned {pointsEarned} points on spend of Rs.{eligibleSpend:N2}"
        };

        await _context.LoyaltyTransactions.AddAsync(transaction, ct);
        await _context.SaveChangesAsync(ct);

        return ApiResponse<LoyaltyAccrualResult>.Ok(
            new LoyaltyAccrualResult(pointsEarned, customer.LoyaltyPoints, pointsEarned),
            $"Customer earned {pointsEarned} loyalty points!"
        );
    }

    public async Task<ApiResponse<decimal>> RedeemPointsAsync(Guid customerId, int pointsToRedeem, decimal currentBillTotal, CancellationToken ct = default)
    {
        if (pointsToRedeem <= 0)
        {
            return ApiResponse<decimal>.Fail("Points to redeem must be greater than zero.");
        }

        var customer = await _context.Customers.FindAsync(new object[] { customerId }, ct);
        if (customer == null)
        {
            return ApiResponse<decimal>.Fail("Customer not found.");
        }

        if (customer.LoyaltyPoints < pointsToRedeem)
        {
            return ApiResponse<decimal>.Fail($"Customer only has {customer.LoyaltyPoints} points available.");
        }

        // Max redemption rule: up to 25% of current bill total
        decimal maxAllowedRedemption = Math.Floor(currentBillTotal * 0.25m);
        if (pointsToRedeem > maxAllowedRedemption)
        {
            return ApiResponse<decimal>.Fail($"Maximum allowable loyalty redemption for this bill is {maxAllowedRedemption} points (25% cap).");
        }

        customer.LoyaltyPoints -= pointsToRedeem;
        decimal discountValue = pointsToRedeem; // 1 point = Rs. 1

        var transaction = new LoyaltyTransaction
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            TransactionType = LoyaltyTransactionType.Redeemed,
            Points = -pointsToRedeem,
            EquivalentAmount = discountValue,
            Description = $"Redeemed {pointsToRedeem} points for Rs.{discountValue:N2} discount"
        };

        await _context.LoyaltyTransactions.AddAsync(transaction, ct);
        await _context.SaveChangesAsync(ct);

        return ApiResponse<decimal>.Ok(discountValue, $"Successfully redeemed {pointsToRedeem} points for Rs.{discountValue:N2} discount.");
    }
}
