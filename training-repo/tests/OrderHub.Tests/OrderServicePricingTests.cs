using OrderHub.Core.Domain;
using OrderHub.Core.Services;

namespace OrderHub.Tests;

public class OrderServicePricingTests
{
    [Theory]
    [InlineData(CustomerTier.Standard, 0)]
    [InlineData(CustomerTier.Silver, 0.05)]
    [InlineData(CustomerTier.Gold, 0.10)]
    public void GetDiscountRate_ReturnsExpectedRate(CustomerTier tier, decimal expected)
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);

        Assert.Equal(expected, service.GetDiscountRate(tier));
    }

    [Fact]
    public void CalculateSubtotal_SumsQuantityTimesSnapshotPrice()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);

        var order = new Order
        {
            Items =
            {
                new OrderItem { Quantity = 2, UnitPriceSnapshot = 150m },
                new OrderItem { Quantity = 3, UnitPriceSnapshot = 40m }
            }
        };

        Assert.Equal(420m, service.CalculateSubtotal(order));
    }

    [Theory]
    [InlineData(CustomerTier.Standard, 1000, 1000)]
    [InlineData(CustomerTier.Silver, 1000, 950)]
    [InlineData(CustomerTier.Gold, 1000, 900)]
    public void CalculateTotal_AppliesTierDiscountOnSubtotal(CustomerTier tier, decimal unitPrice, decimal expectedTotal)
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);

        var order = new Order
        {
            Customer = new Customer { Tier = tier },
            Items = { new OrderItem { Quantity = 1, UnitPriceSnapshot = unitPrice } }
        };

        Assert.Equal(expectedTotal, service.CalculateTotal(order));
    }

    [Fact]
    public void CalculateTotal_WithoutCustomer_UsesStandardRate()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);

        var order = new Order
        {
            Items = { new OrderItem { Quantity = 2, UnitPriceSnapshot = 250m } }
        };

        Assert.Equal(500m, service.CalculateTotal(order));
    }

    // 回歸測試（客訴 2）：Gold 會員的折扣只能算一次。
    // 修復前 CreateOrderAsync 對 Gold 先把快照打 9 折，CalculateTotal 又打一次 → 0.81 倍（雙重折扣）。
    // 折扣應集中在 CalculateTotal，快照存原價（符合 CLAUDE.md 慣例、與 DbSeeder 一致）。
    [Fact]
    public async Task CreateOrder_GoldCustomer_AppliesDiscountOnce()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db, tier: CustomerTier.Gold);
        var product = TestSetup.AddProduct(db, unitPrice: 1000m, stock: 10);

        var created = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 1) });
        Assert.True(created.Success);

        // 快照必須是原價（折扣不在建單時算）；bug 時會是 900
        Assert.Equal(1000m, created.Value!.Items.Single().UnitPriceSnapshot);

        // 如明細頁重新載入（含 Customer），應付總額為單次 9 折 = 900；bug 時 900×0.9 = 810
        var reloaded = await service.GetOrderAsync(created.Value.Id);
        Assert.Equal(CustomerTier.Gold, reloaded!.Customer!.Tier);
        Assert.Equal(900m, service.CalculateTotal(reloaded));
    }
}
