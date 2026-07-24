using System.ComponentModel.DataAnnotations;

namespace OrderHub.Web.ViewModels;

public class LowStockViewModel
{
    // 用 int? 讓「未帶參數」時為 null → 通過 Range → 由 EffectiveThreshold 套預設 10；
    // 帶了 <= 0 才會觸發 Range 驗證錯誤（顯示在表單上，不會變成 500）。
    [Range(1, int.MaxValue, ErrorMessage = "庫存門檻必須大於 0")]
    [Display(Name = "庫存門檻")]
    public int? Threshold { get; set; }

    public int EffectiveThreshold => Threshold ?? 10;

    public IReadOnlyList<LowStockRowViewModel> Products { get; set; } = Array.Empty<LowStockRowViewModel>();
}

public class LowStockRowViewModel
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public int SoldLast30Days { get; set; }
}
