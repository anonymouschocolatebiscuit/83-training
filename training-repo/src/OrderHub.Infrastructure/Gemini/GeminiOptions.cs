namespace OrderHub.Infrastructure.Gemini;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>來自 user-secrets 的 Gemini:ApiKey;沒設時 client 會退回環境變數 GEMINI_API_KEY。</summary>
    public string? ApiKey { get; set; }

    // 預設用免費層有配額的 flash 模型;實測本專案金鑰對 gemini-2.0-flash 免費配額為 0、
    // 對 gemini-2.5-flash 可用。可用 Gemini:Model(user-secrets/env/appsettings)覆寫。
    public string Model { get; set; } = "gemini-2.5-flash";

    /// <summary>
    /// generateContent 端點的「基底」(不含 model)。實際 URL 由 client 組成
    /// {Endpoint}/{Model}:generateContent。
    /// 說明:活動範本示範的是假想的 /v1/interactions;此處對齊 Google 現行真實 API。
    /// </summary>
    public string Endpoint { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models";

    public int MaxRetries { get; set; } = 4;
}
