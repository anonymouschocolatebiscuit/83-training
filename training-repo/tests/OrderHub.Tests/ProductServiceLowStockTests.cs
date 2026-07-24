using OrderHub.Core.Domain;

namespace OrderHub.Tests;

public class ProductServiceLowStockTests
{
    // 規格：庫存 < threshold（不是 <=）且 IsActive，依庫存量升冪。
    [Fact]
    public async Task GetLowStock_ReturnsActiveBelowThreshold_SortedByStockAscending()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);

        TestSetup.AddProduct(db, sku: "LOW-8", stock: 8);   // < 10 → 列入
        TestSetup.AddProduct(db, sku: "LOW-2", stock: 2);   // < 10 → 列入
        TestSetup.AddProduct(db, sku: "EQ-10", stock: 10);  // == 10 → 排除（是 <，非 <=）
        TestSetup.AddProduct(db, sku: "HI-20", stock: 20);  // > 10 → 排除

        var result = await service.GetLowStockAsync(10);

        // 只留 < 10 的兩筆，且依庫存升冪（2 在 8 前）
        Assert.Equal(new[] { "LOW-2", "LOW-8" }, result.Select(r => r.Product.Sku).ToArray());
    }

    [Fact]
    public async Task GetLowStock_ExcludesInactiveProducts()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);

        TestSetup.AddProduct(db, sku: "ACT-3", stock: 3, isActive: true);
        TestSetup.AddProduct(db, sku: "INA-1", stock: 1, isActive: false);  // 低庫存但已停售 → 排除

        var result = await service.GetLowStockAsync(10);

        var row = Assert.Single(result);
        Assert.Equal("ACT-3", row.Product.Sku);
    }

    // 規格：近 30 天售出數量，從訂單明細統計，排除 Cancelled 訂單。
    [Fact]
    public async Task GetLowStock_SoldLast30Days_ExcludesCancelledAndOrdersOlderThan30Days()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, sku: "SOLD-3", stock: 3);  // 低庫存、販售中

        var now = DateTime.UtcNow;

        // 近 30 天、非取消 → 計入（2 + 3 = 5）
        db.Orders.Add(new Order
        {
            CustomerId = customer.Id, Status = OrderStatus.Shipped, CreatedAt = now.AddDays(-1),
            Items = { new OrderItem { ProductId = product.Id, Quantity = 2, UnitPriceSnapshot = 100m } }
        });
        db.Orders.Add(new Order
        {
            CustomerId = customer.Id, Status = OrderStatus.Confirmed, CreatedAt = now.AddDays(-10),
            Items = { new OrderItem { ProductId = product.Id, Quantity = 3, UnitPriceSnapshot = 100m } }
        });
        // 近 30 天但已取消 → 排除
        db.Orders.Add(new Order
        {
            CustomerId = customer.Id, Status = OrderStatus.Cancelled, CreatedAt = now.AddDays(-2),
            Items = { new OrderItem { ProductId = product.Id, Quantity = 50, UnitPriceSnapshot = 100m } }
        });
        // 超過 30 天 → 排除
        db.Orders.Add(new Order
        {
            CustomerId = customer.Id, Status = OrderStatus.Shipped, CreatedAt = now.AddDays(-40),
            Items = { new OrderItem { ProductId = product.Id, Quantity = 99, UnitPriceSnapshot = 100m } }
        });
        db.SaveChanges();

        var result = await service.GetLowStockAsync(10);

        var row = Assert.Single(result);
        Assert.Equal(product.Id, row.Product.Id);
        Assert.Equal(5, row.SoldLast30Days);
    }
}
