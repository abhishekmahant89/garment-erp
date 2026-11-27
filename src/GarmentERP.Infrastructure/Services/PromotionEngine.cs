using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Infrastructure.Services;

public class PromotionEngine : IPromotionEngine
{
    private readonly GarmentDbContextBase _context;

    public PromotionEngine(GarmentDbContextBase context)
    {
        _context = context;
    }

    public async Task<ApiResponse<Promotion>> CreatePromotionAsync(CreatePromotionDto dto, CancellationToken ct = default)
    {
        var promo = new Promotion
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Code = dto.Code.Trim().ToUpperInvariant(),
            Type = dto.Type,
            DiscountValue = dto.DiscountValue,
            MinimumBillValue = dto.MinimumBillValue,
            BuyQuantity = dto.BuyQuantity,
            FreeQuantity = dto.FreeQuantity,
            TargetCategoryId = dto.TargetCategoryId,
            TargetBrandId = dto.TargetBrandId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            MaxUsageCount = dto.MaxUsageCount,
            UsageCount = 0
        };

        await _context.Promotions.AddAsync(promo, ct);
        await _context.SaveChangesAsync(ct);

        return ApiResponse<Promotion>.Ok(promo, "Promotion created successfully.");
    }

    public async Task<ApiResponse<List<Promotion>>> GetActivePromotionsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var list = await _context.Promotions
            .Where(p => p.IsActive && p.StartDate <= now && p.EndDate >= now)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return ApiResponse<List<Promotion>>.Ok(list);
    }

    public PromotionEvaluationResult EvaluatePromotion(Promotion promotion, decimal billSubTotal, int totalGarments)
    {
        var now = DateTime.UtcNow;
        if (!promotion.IsActive || promotion.StartDate > now || promotion.EndDate < now)
        {
            return new PromotionEvaluationResult(false, 0, promotion.Name, "Promotion is expired or inactive.");
        }

        if (promotion.MaxUsageCount.HasValue && promotion.UsageCount >= promotion.MaxUsageCount.Value)
        {
            return new PromotionEvaluationResult(false, 0, promotion.Name, "Promotion usage limit reached.");
        }

        if (billSubTotal < promotion.MinimumBillValue)
        {
            return new PromotionEvaluationResult(false, 0, promotion.Name, $"Minimum bill value of Rs.{promotion.MinimumBillValue:N2} required.");
        }

        decimal discount = 0;
        switch (promotion.Type)
        {
            case PromotionType.FlatPercentage:
                discount = Math.Round(billSubTotal * (promotion.DiscountValue / 100m), 2);
                break;

            case PromotionType.FlatAmount:
                discount = Math.Min(billSubTotal, promotion.DiscountValue);
                break;

            case PromotionType.BuyXGetYFree:
                if (promotion.BuyQuantity > 0 && totalGarments >= (promotion.BuyQuantity + promotion.FreeQuantity))
                {
                    int sets = totalGarments / (promotion.BuyQuantity + promotion.FreeQuantity);
                    int freeItemsCount = sets * promotion.FreeQuantity;
                    decimal averagePiecePrice = totalGarments > 0 ? (billSubTotal / totalGarments) : 0;
                    discount = Math.Round(freeItemsCount * averagePiecePrice, 2);
                }
                break;

            case PromotionType.CouponCode:
                discount = Math.Round(billSubTotal * (promotion.DiscountValue / 100m), 2);
                break;
        }

        return new PromotionEvaluationResult(
            IsApplicable: discount > 0,
            TotalDiscountAmount: discount,
            PromotionName: promotion.Name,
            Message: discount > 0 ? $"Promotion '{promotion.Name}' applied! Discount: Rs.{discount:N2}" : "Conditions not met."
        );
    }
}
