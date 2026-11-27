using System.Text.Json;
using Xunit;

namespace GarmentERP.Tests;

public class CartCrashRecoveryTests : IDisposable
{
    private readonly string _testSnapshotFile;

    public CartCrashRecoveryTests()
    {
        _testSnapshotFile = Path.Combine(Path.GetTempPath(), $"cart_snap_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testSnapshotFile))
        {
            File.Delete(_testSnapshotFile);
        }
    }

    public class CartTestDto
    {
        public string CustomerName { get; set; } = string.Empty;
        public decimal GrandTotal { get; set; }
        public List<CartTestItem> Items { get; set; } = new();
    }

    public class CartTestItem
    {
        public string SKU { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; }
    }

    [Fact]
    public void CartState_AtomicSaveAndRestore_SurvivesPowerCutSimulation()
    {
        // 1. Simulate Cashier scanning 2 shirts before sudden power cut
        var activeCart = new CartTestDto
        {
            CustomerName = "Rajesh Sharma",
            GrandTotal = 2598.00m,
            Items = new List<CartTestItem>
            {
                new() { SKU = "SHIRT-SLM-2026-BLK-S", Barcode = "8900327943047", UnitPrice = 1299.00m, Quantity = 1 },
                new() { SKU = "SHIRT-SLM-2026-BLU-XL", Barcode = "8900734656318", UnitPrice = 1299.00m, Quantity = 1 }
            }
        };

        // Atomic write to disk (same mechanism as POS)
        var json = JsonSerializer.Serialize(activeCart, new JsonSerializerOptions { WriteIndented = true });
        var tempFile = _testSnapshotFile + ".tmp";
        File.WriteAllText(tempFile, json);
        File.Move(tempFile, _testSnapshotFile, overwrite: true);

        Assert.True(File.Exists(_testSnapshotFile));

        // 2. Simulate System Crash / Power Cut: Memory completely wiped, new session boots up
        var restoredJson = File.ReadAllText(_testSnapshotFile);
        var restoredCart = JsonSerializer.Deserialize<CartTestDto>(restoredJson);

        Assert.NotNull(restoredCart);
        Assert.Equal("Rajesh Sharma", restoredCart.CustomerName);
        Assert.Equal(2598.00m, restoredCart.GrandTotal);
        Assert.Equal(2, restoredCart.Items.Count);
        Assert.Equal("SHIRT-SLM-2026-BLK-S", restoredCart.Items[0].SKU);
        Assert.Equal("8900327943047", restoredCart.Items[0].Barcode);
    }
}
