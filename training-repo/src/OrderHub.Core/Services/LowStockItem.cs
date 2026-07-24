using OrderHub.Core.Domain;

namespace OrderHub.Core.Services;

/// <summary>低庫存頁的一列：商品本身 + 近 30 天售出數量（排除 Cancelled 訂單）。</summary>
public record LowStockItem(Product Product, int SoldLast30Days);
