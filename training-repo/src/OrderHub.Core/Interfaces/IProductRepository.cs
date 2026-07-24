using OrderHub.Core.Domain;

namespace OrderHub.Core.Interfaces;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAllAsync();
    Task<IReadOnlyList<Product>> GetActiveAsync();
    Task<Product?> GetByIdAsync(int id);

    /// <summary>販售中且庫存低於 threshold 的商品，依庫存量升冪。</summary>
    Task<IReadOnlyList<Product>> GetActiveBelowStockAsync(int threshold);

    Task SaveChangesAsync();
}
