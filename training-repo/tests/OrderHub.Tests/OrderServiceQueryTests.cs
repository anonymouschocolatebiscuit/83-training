using OrderHub.Core.Domain;

namespace OrderHub.Tests;

public class OrderServiceQueryTests
{
    [Fact]
    public async Task GetOrders_WithStatusFilter_ReturnsOnlyMatchingStatus()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);

        db.Orders.AddRange(
            new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customer.Id, Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customer.Id, Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        var result = await service.GetOrdersAsync(1, 20, OrderStatus.Shipped);

        Assert.All(result.Items, o => Assert.Equal(OrderStatus.Shipped, o.Status));
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetOrders_ReportsTotalCountAndTotalPages()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);

        for (var i = 0; i < 45; i++)
            db.Orders.Add(new Order { CustomerId = customer.Id, Status = OrderStatus.Confirmed, CreatedAt = DateTime.UtcNow.AddMinutes(-i) });
        db.SaveChanges();

        var result = await service.GetOrdersAsync(1, 20, null);

        Assert.Equal(45, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GetCustomerOrders_ReturnsOnlyThatCustomersOrders()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customerA = TestSetup.AddCustomer(db, name: "客戶A");
        var customerB = TestSetup.AddCustomer(db, name: "客戶B");

        db.Orders.AddRange(
            new Order { CustomerId = customerA.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customerB.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customerA.Id, Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        var orders = await service.GetCustomerOrdersAsync(customerA.Id);

        Assert.Equal(2, orders.Count);
        Assert.All(orders, o => Assert.Equal(customerA.Id, o.CustomerId));
    }

    // 回歸測試（客訴 1）：分頁必須 1-based。第 1 頁要回傳最新的一頁、最後一頁不可空白。
    // 修復前 GetPagedAsync 用 Skip(page * pageSize)，第 1 頁跳過最新 20 筆、最後一頁 overshoot 成空白。
    [Fact]
    public async Task GetOrders_FirstPage_IncludesNewest_AndLastPageNotEmpty()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);

        // 25 筆訂單，CreatedAt 由新到舊嚴格遞減（i=0 為最新）
        var newest = DateTime.UtcNow;
        for (var i = 0; i < 25; i++)
            db.Orders.Add(new Order { CustomerId = customer.Id, Status = OrderStatus.Confirmed, CreatedAt = newest.AddMinutes(-i) });
        db.SaveChanges();

        var page1 = await service.GetOrdersAsync(1, 20, null);
        Assert.Equal(20, page1.Items.Count);                        // 第 1 頁應為滿頁（bug 時只有 5 筆）
        Assert.Equal(newest, page1.Items.Max(o => o.CreatedAt));    // 最新的訂單必須在第 1 頁（bug 時被跳過）

        var page2 = await service.GetOrdersAsync(2, 20, null);
        Assert.Equal(5, page2.Items.Count);                         // 最後一頁應有剩下 5 筆（bug 時為 0，空白）
    }
}
