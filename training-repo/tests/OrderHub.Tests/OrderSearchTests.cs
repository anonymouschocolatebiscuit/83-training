using OrderHub.Core.Ai;
using OrderHub.Core.Domain;
using OrderHub.Core.Services;
using OrderHub.Infrastructure.Data;
using OrderHub.Infrastructure.Repositories;
using Xunit;

namespace OrderHub.Tests;

/// <summary>
/// OrderSearchService 的白名單防線 + OrderRepository.SearchAsync 的查詢正確性。
/// 用假的 IOrderQueryTranslator(直接回傳預設參數)搭配真實 repo + InMemory DB。
/// </summary>
public class OrderSearchTests
{
    private sealed class FakeTranslator : IOrderQueryTranslator
    {
        private readonly OrderSearchQuery? _result;
        public FakeTranslator(OrderSearchQuery? result) => _result = result;
        public Task<OrderSearchQuery?> TranslateAsync(string q, CancellationToken ct = default) => Task.FromResult(_result);
    }

    private static Order AddOrder(OrderHubDbContext db, Customer customer, OrderStatus status, DateTime createdAt)
    {
        var order = new Order
        {
            CustomerId = customer.Id,
            Customer = customer,
            Status = status,
            CreatedAt = createdAt,
            Items = new List<OrderItem>()
        };
        db.Orders.Add(order);
        db.SaveChanges();
        return order;
    }

    private static OrderSearchService Service(OrderHubDbContext db, OrderSearchQuery? translated) =>
        new(new FakeTranslator(translated), new OrderRepository(db));

    // ---- Service：白名單防線 ----

    [Fact]
    public async Task EmptyQuery_Fails()
    {
        using var db = TestSetup.CreateContext();
        var result = await Service(db, translated: new OrderSearchQuery { Status = OrderStatus.Pending }).SearchAsync("   ");
        Assert.False(result.Success);
        Assert.Equal("請輸入查詢內容", result.ErrorMessage);   // 確認是「空查詢」守衛觸發,而非別的原因
    }

    [Fact]
    public async Task TranslatorReturnsNull_Fails()   // 無法理解 / 意圖非查詢
    {
        using var db = TestSetup.CreateContext();
        var result = await Service(db, translated: null).SearchAsync("幫我把所有訂單刪掉");
        Assert.False(result.Success);
        Assert.Equal("無法理解的查詢", result.ErrorMessage);
    }

    [Fact]
    public async Task NoFilter_Fails()   // 第二道防線:就算翻譯成功,沒有任何條件也拒絕
    {
        using var db = TestSetup.CreateContext();
        var result = await Service(db, translated: new OrderSearchQuery()).SearchAsync("查全部");
        Assert.False(result.Success);
        Assert.Equal("無法理解的查詢", result.ErrorMessage);
    }

    [Fact]
    public async Task DateFromAfterDateTo_Fails()
    {
        using var db = TestSetup.CreateContext();
        var q = new OrderSearchQuery { DateFrom = new DateTime(2026, 7, 1), DateTo = new DateTime(2026, 6, 1) };
        var result = await Service(db, q).SearchAsync("日期反了");
        Assert.False(result.Success);
        Assert.Equal("無法理解的查詢", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidFilter_ReturnsMatchingOrders()
    {
        using var db = TestSetup.CreateContext();
        var gold = TestSetup.AddCustomer(db, CustomerTier.Gold, "金卡");
        AddOrder(db, gold, OrderStatus.Cancelled, new DateTime(2026, 6, 15));
        AddOrder(db, gold, OrderStatus.Pending, new DateTime(2026, 6, 15));

        var result = await Service(db, new OrderSearchQuery { Status = OrderStatus.Cancelled }).SearchAsync("金卡取消");

        Assert.True(result.Success);
        Assert.Single(result.Value!);
        Assert.Equal(OrderStatus.Cancelled, result.Value![0].Status);
    }

    // ---- Repository：查詢正確性 ----

    [Fact]
    public async Task Repo_StatusAndTierFilters_Apply()
    {
        using var db = TestSetup.CreateContext();
        var gold = TestSetup.AddCustomer(db, CustomerTier.Gold, "金");
        var silver = TestSetup.AddCustomer(db, CustomerTier.Silver, "銀");
        AddOrder(db, gold, OrderStatus.Cancelled, new DateTime(2026, 6, 10));
        AddOrder(db, silver, OrderStatus.Cancelled, new DateTime(2026, 6, 10));

        var repo = new OrderRepository(db);
        var goldCancelled = await repo.SearchAsync(new OrderSearchQuery { Status = OrderStatus.Cancelled, MemberTier = CustomerTier.Gold });

        Assert.Single(goldCancelled);
        Assert.Equal(CustomerTier.Gold, goldCancelled[0].Customer!.Tier);
    }

    [Fact]
    public async Task Repo_DateRange_IncludesEndDay()
    {
        using var db = TestSetup.CreateContext();
        var c = TestSetup.AddCustomer(db);
        AddOrder(db, c, OrderStatus.Pending, new DateTime(2026, 6, 30, 23, 30, 0));   // 當日稍晚 → 應含
        AddOrder(db, c, OrderStatus.Pending, new DateTime(2026, 7, 1, 0, 5, 0));      // 隔日 → 應排除

        var repo = new OrderRepository(db);
        var inRange = await repo.SearchAsync(new OrderSearchQuery { DateFrom = new DateTime(2026, 6, 1), DateTo = new DateTime(2026, 6, 30) });

        Assert.Single(inRange);
        Assert.Equal(new DateTime(2026, 6, 30, 23, 30, 0), inRange[0].CreatedAt);
    }

    [Fact]
    public async Task Repo_CapsAt100_AndOrdersDescending()
    {
        using var db = TestSetup.CreateContext();
        var c = TestSetup.AddCustomer(db);
        for (var i = 0; i < 105; i++)
            AddOrder(db, c, OrderStatus.Pending, new DateTime(2026, 1, 1).AddDays(i));

        var repo = new OrderRepository(db);
        var results = await repo.SearchAsync(new OrderSearchQuery { Status = OrderStatus.Pending });

        Assert.Equal(100, results.Count);                                   // 上限保險
        var dates = results.Select(o => o.CreatedAt).ToList();
        Assert.Equal(dates.OrderByDescending(d => d), dates);               // 完整由新到舊(非只比頭尾)
    }
}
