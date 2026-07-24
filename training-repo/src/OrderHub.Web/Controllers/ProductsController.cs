using Microsoft.AspNetCore.Mvc;
using OrderHub.Core.Services;
using OrderHub.Web.ViewModels;

namespace OrderHub.Web.Controllers;

public class ProductsController : Controller
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _productService.GetAllAsync();

        var vm = new ProductListViewModel
        {
            Products = products.Select(p => new ProductRowViewModel
            {
                Sku = p.Sku,
                Name = p.Name,
                UnitPrice = p.UnitPrice,
                StockQuantity = p.StockQuantity,
                IsActive = p.IsActive
            }).ToList()
        };

        return View(vm);
    }

    public async Task<IActionResult> LowStock(LowStockViewModel vm)
    {
        // threshold <= 0 由 ViewModel 的 DataAnnotations 擋下；此時只回表單顯示錯誤、不查資料。
        if (ModelState.IsValid)
        {
            var items = await _productService.GetLowStockAsync(vm.EffectiveThreshold);

            vm.Products = items
                .Select(i => new LowStockRowViewModel
                {
                    Sku = i.Product.Sku,
                    Name = i.Product.Name,
                    StockQuantity = i.Product.StockQuantity,
                    SoldLast30Days = i.SoldLast30Days
                })
                .ToList();
        }

        return View(vm);
    }
}

