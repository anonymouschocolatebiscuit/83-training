using OrderHub.Core.Domain;
using OrderHub.Core.Interfaces;

namespace OrderHub.Core.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IOrderRepository _orderRepository;

    public ProductService(IProductRepository productRepository, IOrderRepository orderRepository)
    {
        _productRepository = productRepository;
        _orderRepository = orderRepository;
    }

    public Task<IReadOnlyList<Product>> GetAllAsync() => _productRepository.GetAllAsync();

    public Task<IReadOnlyList<Product>> GetActiveAsync() => _productRepository.GetActiveAsync();

    public async Task<IReadOnlyList<LowStockItem>> GetLowStockAsync(int threshold)
    {
        var products = await _productRepository.GetActiveBelowStockAsync(threshold);

        var since = DateTime.UtcNow.AddDays(-30);
        var soldByProduct = await _orderRepository.GetSoldQuantitiesSinceAsync(since);

        return products
            .Select(p => new LowStockItem(p, soldByProduct.TryGetValue(p.Id, out var qty) ? qty : 0))
            .ToList();
    }
}
