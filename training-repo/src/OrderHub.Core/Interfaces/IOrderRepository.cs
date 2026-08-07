using OrderHub.Core.Ai;
using OrderHub.Core.Common;
using OrderHub.Core.Domain;

namespace OrderHub.Core.Interfaces;

public interface IOrderRepository
{
    Task<PagedResult<Order>> GetPagedAsync(int page, int pageSize, OrderStatus? status);
    Task<Order?> GetWithDetailsAsync(int id);
    Task<IReadOnlyList<Order>> GetByCustomerAsync(int customerId);

    /// <summary>依白名單參數查訂單(自然語言查詢用)。SQL 由 EF Core 生成,不吃模型字串。</summary>
    Task<IReadOnlyList<Order>> SearchAsync(OrderSearchQuery query);

    /// <summary>自 sinceUtc 起、排除 Cancelled 訂單的各商品售出總量（productId → 數量）。</summary>
    Task<IReadOnlyDictionary<int, int>> GetSoldQuantitiesSinceAsync(DateTime sinceUtc);

    Task AddAsync(Order order);
    Task SaveChangesAsync();
}
