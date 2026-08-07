using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderHub.Core.Ai;

namespace OrderHub.Infrastructure.Gemini;

/// <summary>
/// 裸 HttpClient 呼叫 Gemini 的 generateContent API
/// (POST …/v1beta/models/{model}:generateContent),用 structured output 取 JSON。
///
/// 註:活動範本示範的是假想的 /v1/interactions 形狀;為了讓真實金鑰能跑起來,
/// 這裡對齊 Google 現行 API(contents/parts + generationConfig.responseSchema,
/// 回應取 candidates[0].content.parts[0].text)。其餘安全處理與活動一致。
///
/// 免費層一定會撞 429:重試時優先尊重回應附帶的建議等待時間,再退而用指數退避;
/// 重試耗盡擲 AiServiceUnavailableException,讓 Web 層回 503 而不是 500。
/// </summary>
public class GeminiGenerateContentClient : IGeminiJsonClient
{
    private readonly HttpClient _http;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiGenerateContentClient> _logger;

    public GeminiGenerateContentClient(HttpClient http, IOptions<GeminiOptions> options, ILogger<GeminiGenerateContentClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GenerateJsonAsync(string input, string responseSchemaJson, CancellationToken cancellationToken = default)
    {
        var apiKey = _options.ApiKey ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AiServiceUnavailableException("Gemini API key 未設定:user-secrets 的 Gemini:ApiKey 或環境變數 GEMINI_API_KEY");

        using var schema = JsonDocument.Parse(responseSchemaJson);
        var body = JsonSerializer.Serialize(new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = input } } }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = schema.RootElement
            }
        });

        var url = $"{_options.Endpoint.TrimEnd('/')}/{_options.Model}:generateContent";

        TimeSpan? delay = null;
        for (var attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            if (delay is not null)
            {
                _logger.LogWarning("Gemini 暫時失敗,{Seconds:0.#} 秒後重試(第 {Attempt}/{Max} 次)",
                    delay.Value.TotalSeconds, attempt, _options.MaxRetries);
                await Task.Delay(delay.Value, cancellationToken);
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-goog-api-key", apiKey);

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException)
            {
                delay = ExponentialBackoff(attempt);   // 網路層錯誤,退避後重試
                continue;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // HttpClient 逾時會擲 TaskCanceledException(而非 HttpRequestException):
                // 視為可重試的暫時失敗,而不是讓它變成 500。真正的呼叫端取消則往外傳。
                delay = ExponentialBackoff(attempt);
                continue;
            }

            using (response)
            {
                var payload = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                    return ExtractModelOutput(payload);

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    throw new AiServiceUnavailableException("Gemini 拒絕存取:API key 無效或專案權限不足");

                // 記下上游狀態與回應片段,方便診斷(不含金鑰)
                _logger.LogWarning("Gemini 回應 {Status}:{Body}", (int)response.StatusCode,
                    payload.Length > 500 ? payload[..500] : payload);

                // 429 / 5xx:可重試。429 優先尊重 error details 的建議等待時間
                delay = response.StatusCode == HttpStatusCode.TooManyRequests
                    ? SuggestedRetryDelay(payload) ?? ExponentialBackoff(attempt)
                    : ExponentialBackoff(attempt);
            }
        }

        throw new AiServiceUnavailableException($"Gemini 重試 {_options.MaxRetries} 次後仍失敗,請稍後再試");
    }

    private static TimeSpan ExponentialBackoff(int attempt) => TimeSpan.FromSeconds(Math.Pow(2, attempt));

    /// <summary>429 的 error details 會附 RetryInfo(例如 "retryDelay": "17s")。</summary>
    private static TimeSpan? SuggestedRetryDelay(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("details", out var details))
            {
                foreach (var detail in details.EnumerateArray())
                {
                    if (detail.TryGetProperty("retryDelay", out var retryDelay) &&
                        retryDelay.GetString() is { } text &&
                        text.EndsWith("s") &&
                        double.TryParse(text.TrimEnd('s'), NumberStyles.Any, CultureInfo.InvariantCulture, out var seconds))
                    {
                        return TimeSpan.FromSeconds(seconds);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // 回應不是 JSON 就走指數退避
        }
        return null;
    }

    /// <summary>從 generateContent 回應撈出第一個 candidate 的文字內容(即符合 schema 的 JSON 字串)。</summary>
    private static string ExtractModelOutput(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        if (doc.RootElement.TryGetProperty("candidates", out var candidates))
        {
            foreach (var candidate in candidates.EnumerateArray())
            {
                if (candidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts))
                {
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var text) && text.GetString() is { Length: > 0 } json)
                            return json;
                    }
                }
            }
        }
        throw new AiServiceUnavailableException("Gemini 回應中沒有可用的 candidates/text,無法取得結果");
    }
}
