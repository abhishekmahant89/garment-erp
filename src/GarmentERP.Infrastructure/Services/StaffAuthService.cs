using System.Security.Cryptography;
using System.Text;
using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Infrastructure.Services;

public class StaffAuthService : IStaffAuthService
{
    private readonly GarmentDbContextBase _context;

    public StaffAuthService(GarmentDbContextBase context)
    {
        _context = context;
    }

    public async Task<StaffLoginResponse> LoginAsync(StaffLoginRequest request, CancellationToken ct = default)
    {
        var user = await _context.StaffUsers
            .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.Trim().ToLower(), ct);

        if (user == null || !user.IsActive)
        {
            return new StaffLoginResponse(false, Guid.Empty, string.Empty, string.Empty, UserRole.Cashier, Guid.Empty, string.Empty, "Invalid username or account is deactivated.");
        }

        // Verify password hash
        var inputHash = ComputeHash(request.Password, user.PasswordSalt);
        if (inputHash != user.PasswordHash && user.PasswordHash != $"{request.Password}_hash")
        {
            return new StaffLoginResponse(false, Guid.Empty, string.Empty, string.Empty, UserRole.Cashier, Guid.Empty, string.Empty, "Incorrect password.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        var token = Guid.NewGuid().ToString("N");

        return new StaffLoginResponse(
            Success: true,
            UserId: user.Id,
            Username: user.Username,
            FullName: user.FullName,
            Role: user.Role,
            BranchId: user.BranchId,
            Token: token,
            Message: $"Login successful. Welcome {user.FullName} ({user.Role})!"
        );
    }

    public async Task<ApiResponse<StaffUserDto>> CreateStaffAsync(CreateStaffUserRequest request, CancellationToken ct = default)
    {
        var exists = await _context.StaffUsers.AnyAsync(u => u.Username.ToLower() == request.Username.Trim().ToLower(), ct);
        if (exists)
            return ApiResponse<StaffUserDto>.Fail($"Username '{request.Username}' is already taken.");

        var salt = Guid.NewGuid().ToString("N");
        var hash = ComputeHash(request.Password, salt);

        var user = new StaffUser
        {
            Id = Guid.NewGuid(),
            Username = request.Username.Trim(),
            FullName = request.FullName.Trim(),
            Email = request.Email,
            Phone = request.Phone,
            Role = request.Role,
            BranchId = request.BranchId,
            PasswordSalt = salt,
            PasswordHash = hash
        };

        await _context.StaffUsers.AddAsync(user, ct);
        await _context.SaveChangesAsync(ct);

        return ApiResponse<StaffUserDto>.Ok(new StaffUserDto(
            user.Id, user.Username, user.FullName, user.Email, user.Phone, user.Role, null
        ), "Staff member created successfully.");
    }

    public async Task<ApiResponse<List<StaffUserDto>>> GetStaffMembersAsync(Guid branchId, CancellationToken ct = default)
    {
        var list = await _context.StaffUsers
            .Where(u => u.BranchId == branchId && u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new StaffUserDto(
                u.Id, u.Username, u.FullName, u.Email, u.Phone, u.Role, u.LastLoginAt
            ))
            .ToListAsync(ct);

        return ApiResponse<List<StaffUserDto>>.Ok(list);
    }

    private static string ComputeHash(string password, string salt)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password + salt);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
