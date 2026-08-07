using Microsoft.Extensions.Logging.Abstractions;
using OrderHub.Core.Ai;
using OrderHub.Core.Domain;
using OrderHub.Infrastructure.Gemini;
using Xunit;

namespace OrderHub.Tests;

/// <summary>
/// 翻譯器把「模型輸出」當不可信輸入處理:反序列化 → DataAnnotations 驗證 → 白名單映射。
/// 用假的 IGeminiJsonClient 餵入各種模型輸出,不呼叫真實 Gemini。
/// </summary>
public class GeminiOrderQueryTranslatorTests
{
    private sealed class FakeGeminiClient : IGeminiJsonClient
    {
        private readonly string? _json;
        private readonly Exception? _throw;
        public FakeGeminiClient(string json) => _json = json;
        public FakeGeminiClient(Exception ex) => _throw = ex;

        public Task<string> GenerateJsonAsync(string input, string responseSchemaJson, CancellationToken cancellationToken = default)
            => _throw is not null ? Task.FromException<string>(_throw) : Task.FromResult(_json!);
    }

    private static GeminiOrderQueryTranslator Translator(string modelJson) =>
        new(new FakeGeminiClient(modelJson), NullLogger<GeminiOrderQueryTranslator>.Instance);

    [Fact]
    public async Task ValidSearch_MapsToWhitelistedQuery()
    {
        var t = Translator("""{"intent":"search","status":"Cancelled","memberTier":"Gold","dateFrom":"2026-06-01","dateTo":"2026-06-30"}""");

        var q = await t.TranslateAsync("上個月金卡會員取消的訂單");

        Assert.NotNull(q);
        Assert.Equal(OrderStatus.Cancelled, q!.Status);
        Assert.Equal(CustomerTier.Gold, q.MemberTier);
        Assert.Equal(new DateTime(2026, 6, 1), q.DateFrom);
        Assert.Equal(new DateTime(2026, 6, 30), q.DateTo);
    }

    [Fact]
    public async Task UnsupportedIntent_ReturnsNull()   // 紅線:「幫我把所有訂單刪掉」→ 模型判 unsupported
    {
        var t = Translator("""{"intent":"unsupported"}""");
        Assert.Null(await t.TranslateAsync("幫我把所有訂單刪掉"));
    }

    [Fact]
    public async Task SearchWithNoFields_ReturnsEmptyButNonNullQuery()   // 交由 service 的 no-filter 防線擋
    {
        var t = Translator("""{"intent":"search"}""");
        var q = await t.TranslateAsync("查訂單");
        Assert.NotNull(q);
        Assert.False(q!.HasAnyFilter);
    }

    [Fact]
    public async Task StatusNotInWhitelist_ReturnsNull()   // AllowedValues 先擋,Enum.TryParse 吃不到 "99"
    {
        var t = Translator("""{"intent":"search","status":"99"}""");
        Assert.Null(await t.TranslateAsync("狀態 99 的訂單"));
    }

    [Fact]
    public async Task BadDateFormat_ReturnsNull()
    {
        var t = Translator("""{"intent":"search","dateFrom":"June 1st"}""");
        Assert.Null(await t.TranslateAsync("六月一號以後的訂單"));
    }

    [Fact]
    public async Task MalformedJson_ReturnsNull()   // 模型吐非法 JSON → JsonException → null,不炸
    {
        var t = Translator("this is not json at all");
        Assert.Null(await t.TranslateAsync("隨便"));
    }

    [Fact]
    public async Task UpstreamUnavailable_Propagates()   // 服務不可用要往外傳,由 Web 轉 503(不可被吞成 null)
    {
        var t = new GeminiOrderQueryTranslator(
            new FakeGeminiClient(new AiServiceUnavailableException("上游掛了")),
            NullLogger<GeminiOrderQueryTranslator>.Instance);

        await Assert.ThrowsAsync<AiServiceUnavailableException>(() => t.TranslateAsync("任何查詢"));
    }
}
