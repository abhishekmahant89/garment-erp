using GarmentERP.Application.DTOs;
using GarmentERP.Domain.Entities;
using GarmentERP.Infrastructure.Data;
using GarmentERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GarmentERP.Tests;

public class StaffAuthAndBackupTests
{
    private GarmentOfflineDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GarmentOfflineDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var context = new GarmentOfflineDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task StaffAuth_ValidatesPasswordAndEnforcesRoles()
    {
        await using var context = CreateInMemoryDbContext();
        await DatabaseSeeder.SeedInitialDataAsync(context);

        var authService = new StaffAuthService(context);

        // 1. Correct password for seeded cashier
        var goodLogin = await authService.LoginAsync(new StaffLoginRequest("cashier1", "cashier123"));
        Assert.True(goodLogin.Success);
        Assert.Equal(UserRole.Cashier, goodLogin.Role);
        Assert.Equal("Rahul Verma", goodLogin.FullName);
        Assert.NotEmpty(goodLogin.Token);

        // 2. Incorrect password
        var badLogin = await authService.LoginAsync(new StaffLoginRequest("cashier1", "wrong_password"));
        Assert.False(badLogin.Success);
        Assert.Empty(badLogin.Token);

        // 3. Unknown username
        var unknownLogin = await authService.LoginAsync(new StaffLoginRequest("ghost_user", "password"));
        Assert.False(unknownLogin.Success);
    }

    [Fact]
    public async Task BackupService_VerifiesIntegrityAndCreatesSnapshot()
    {
        await using var context = CreateInMemoryDbContext();
        await DatabaseSeeder.SeedInitialDataAsync(context);

        var backupService = new BackupService(context);

        // 1. Integrity check
        var integrityResult = await backupService.VerifyDatabaseIntegrityAsync();
        Assert.True(integrityResult.Success);

        // 2. Create backup manifest/snapshot
        var tempFolder = Path.Combine(Path.GetTempPath(), "GarmentERP_TestBackups");
        var backupResult = await backupService.CreateDatabaseBackupAsync(tempFolder);
        Assert.True(backupResult.Success);
        Assert.True(File.Exists(backupResult.Data!.BackupFilePath));

        // Cleanup
        try { File.Delete(backupResult.Data.BackupFilePath); } catch { }
    }
}
