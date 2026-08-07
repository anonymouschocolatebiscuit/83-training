# OrderHub 訓練 — 活動 3（Gemini API：把 AI 嵌進產品）執行紀錄 (Execution Log 3)

> 本檔對應**活動 3：Gemini 免費 API — 自然語言查訂單**（`documents/activities/activity-3-gemini-api.md`）。
> 活動 1 紀錄在 [`EXECUTION-LOG.md`](EXECUTION-LOG.md)、活動 2 在 [`EXECUTION-LOG-2.md`](EXECUTION-LOG-2.md)，三者請勿混用。
> 格式沿用前兩活動：每一步記 **① 被要求做什麼 (Asked) → ② 我做了什麼 (Done) → ③ 結果 (Result)**。
> 由 Claude Code(Opus 4.8) 執行。每個程式步驟流程：**計畫 → 子代理驗證計畫 → 實作 → 子代理 review → 記錄 → 獨立本地 commit（不 push，比照活動 2）**。

---

## 環境基線 (Baseline)　✅

**① Asked**：建立基線，確認可建置、可測試、SQL Server 就緒。

**② Done / ③ Result**：

- .NET SDK：8.0.408 / 9.0.x / 10.0.x（專案 target net8.0）。
- **基線建置**：`dotnet build` → 0 警告 / 0 錯誤。
- **基線測試**：`dotnet test` → **34 passed / 0 failed**（延續活動 2 收尾的 34 綠）。
- SQL Server `MSSQLSERVER`：預設實例，localhost（活動 1/2 已確認 Running；活動 3 於步驟 5 跑網站時再確認）。
- 網站啟動指令：`dotnet run --project src/OrderHub.Web`（http://localhost:5150）。
- Git：branch = main，**只做本地 commit，不 push**（比照活動 2）。

---

## 全域摘要：活動 3 要我做什麼

反過來——**讓 OrderHub 呼叫 AI**。用 Gemini 免費 API 加一個「自然語言查訂單」入口：使用者打一句中文，LLM **只負責把它轉成白名單查詢參數**，查詢本身仍走既有 repository + EF Core。重點是**在產品裡安全地對待模型輸出**。

| 練習 | 主題 | 交付物 |
| --- | --- | --- |
| 1a | Core | `OrderSearchQuery`、`IOrderQueryTranslator`、`AiServiceUnavailableException`、`IOrderSearchService`/`OrderSearchService`、`IOrderRepository.SearchAsync` |
| 1b | Infrastructure | `GeminiOptions`、`IGeminiJsonClient`、Gemini client、`GeminiOrderQueryTranslator`、`OrderRepository.SearchAsync` |
| 1c | Web | `POST /api/orders/search`（`OrdersApiController`）、`Program.cs` 接線 |
| — | 測試 | 安全/白名單邏輯單元測試（mock 掉 Gemini） |
| — | 實測 | 對真實 Gemini 跑煙霧測試（需 user 提供 key） |
| 2 | Web 頁面 | `GET /Orders/Search`、`OrderSearchViewModel`、`Search.cshtml`、導覽列入口 |

### 兩個重要的落地決定（誠實標註）

1. **Gemini 端點形狀已對齊「真實 API」**。活動範本示範的是 `POST /v1/interactions` + `input`/`response_format` + 回應 `steps[].model_output` + 模型 `gemini-3.5-flash`——這是**教學用的簡化/假想形狀**，Google 現行 Gemini API 並非如此。為了讓 user 的金鑰能**真的跑起來**，我把**唯一的傳輸類別**改對準真實端點 `POST …/v1beta/models/{model}:generateContent`（body 用 `contents`/`parts`、structured output 走 `generationConfig.responseSchema`，回應取 `candidates[0].content.parts[0].text`）。**其餘每一層安全設計（白名單、intent 檢查、DataAnnotations、enum/date 映射、no-filter 拒絕、503 而非 500）完全照活動原文**。模型名稱做成可設定（`GeminiOptions.Model`），預設用免費層常見的 flash 模型。
2. **API key 安全**：key 存 .NET user-secrets（`Gemini:ApiKey`），不進 git、agent 不讀取；網站於執行期自行載入。**建議**在 `training-repo/.claude/settings.json` 的 `deny` 加一條 `Read(**/UserSecrets/**)`（呼應活動 1 的 `Read(**/*.pfx)`）——本 session 嘗試自動加入時被 harness 的「權限檔自我修改」防護擋下（正確行為，因非 user 明確要求），故改為**請 user 自行加入**：
   ```json
   "deny": [ …, "Read(**/UserSecrets/**)" ]
   ```

---

## 練習 1a — Core:白名單參數、介面與 service　✅（commit 649a99e）

**① Asked**：Core 放 `OrderSearchQuery`（白名單參數）、`IOrderQueryTranslator`、`AiServiceUnavailableException`、`IOrderSearchService`/`OrderSearchService`，並在 `IOrderRepository` 加 `SearchAsync`。第二道防線在 service：**沒有任何有效條件的查詢一律拒絕**。

**② Done**：

*結構調整（照實記）*：活動把 `IOrderRepository.SearchAsync` 的**介面**放 1a、**實作**放 1b。為了讓**每個 commit 都建置得過**（介面加了方法卻沒實作會讓 `OrderRepository` 不滿足介面而 build 失敗），我把介面 + `OrderRepository` 實作**一起**放進本步。

*計畫驗證子代理（唯讀、對抗式）結論*：**全 CONFIRMED**，並提醒三點（已照做）：
- repo 的 Tier 過濾要 null-guard `o.Customer != null && o.Customer.Tier == ...`（活動範本本就有）。
- `IOrderRepository.cs` 要 `using OrderHub.Core.Ai;`；`OrderSearchService.cs` 要四個 Core using。
- 確認 `OrderRepository` 是 `IOrderRepository` 的**唯一實作**（tests 無 fake），故加方法不會弄壞既有 34 測試；Core 無 Infrastructure 相依（方向正確）。

*實作*（照活動原文）：六個檔案 —
- `Core/Ai/OrderSearchQuery.cs`（4 個 nullable 參數 + `HasAnyFilter`）
- `Core/Ai/IOrderQueryTranslator.cs`（回 `OrderSearchQuery?`，null = 無法理解）
- `Core/Ai/AiServiceUnavailableException.cs`
- `Core/Services/IOrderSearchService.cs` / `OrderSearchService.cs`（空查詢拒、翻譯 null 或無 filter 拒、DateFrom>DateTo 拒 → 再交 repo）
- `Core/Interfaces/IOrderRepository.cs`（+ `SearchAsync`）、`Infrastructure/Repositories/OrderRepository.cs`（+ 實作：條件式過濾、含當日、`Take(100)` 上限、`Include(Customer/Items)`）

**③ Result**：

- `dotnet build` → **0/0**；`dotnet test` → **34 綠**（沒動到既有行為）。
- **實作 review 子代理結論：SHIP**，無 Critical/Major。逐點確認：白名單雙防線正確、分層乾淨（Core 不依賴 Infra、只有 repo 碰 DbContext、service 回 `ServiceResult`）、repo 查詢有界（`Take(100)`、無 N+1、日期含當日、Customer null-guard）。僅資訊性提醒：CancellationToken 未傳進 repo（與既有 repo 慣例一致，非退步）；`AiServiceUnavailableException` 目前未用（step 2 翻譯器會擲、step 3 Web 層轉 503）。

**驗證方式（對照活動 §1a）**：
- [x] Core 三檔 + service + 介面就位，build 綠
- [x] 「沒有任何有效條件」的查詢會被 service 擋下（`!HasAnyFilter` → Fail）
- [x] 分層：SQL 由 EF Core 生成、模型碰不到查詢語句（repo 吃強型別 `OrderSearchQuery`）

---

## 練習 1b — Infrastructure：Gemini client 與翻譯器　✅（commit b43b7dd）

**① Asked**：`Gemini/` 放 `GeminiOptions`、`IGeminiJsonClient`、Gemini client、`GeminiOrderQueryTranslator`。翻譯器**把模型輸出當不可信輸入**（反序列化 → DataAnnotations 驗證 → 白名單映射，任一步失敗回 null）。

**② Done**：

*計畫驗證子代理結論*：**全 CONFIRMED**，且明確確認：
- **不需要新增任何 NuGet**——`IOptions`/`ILogger`(Microsoft.Extensions.* 8.0.2)、`HttpClient`、`System.Text.Json`、`DataAnnotations` 的 `Validator`/`Required`/`AllowedValues`(net8.0 內建) 全都透過 EF Core 8.0.11 相依或 net8.0 targeting pack 提供。**未觸發 CLAUDE.md「不要未經同意加套件」**。
- 提醒：`[Required]` 只放 `Intent`；`Status`/`MemberTier` 要 optional 且 `[AllowedValues]` 含 `null,""`（否則模型省略欄位會被誤判失敗）。已照活動原文。

*實作*：
- `GeminiOptions.cs`：`ApiKey`(string?)、`Model`(預設 `gemini-2.0-flash`，可設定)、`Endpoint`(**基底** URL)、`MaxRetries`。
- `IGeminiJsonClient.cs`：`GenerateJsonAsync(input, responseSchemaJson, ct)`。
- `GeminiGenerateContentClient.cs`：**對齊真實 `…/v1beta/models/{model}:generateContent`**——body `contents/parts` + `generationConfig.responseSchema`（嵌入 parsed `JsonElement`，不重複編碼）、header `x-goog-api-key`、回應取 `candidates[0].content.parts[0].text`。重試：401/403 直接擲、429 尊重 `retryDelay` 再指數退避、5xx/網路錯誤退避重試、耗盡擲 `AiServiceUnavailableException`（→ Web 回 503）。
- `GeminiOrderQueryTranslator.cs`：**安全管線照活動原文**——prompt 內含今天日期、structured output、`Deserialize` 到私有 `RawQuery` → `Validator.TryValidateObject(validateAllProperties:true)` **先驗證再** `Enum.TryParse`/`TryParseExact` 映射（呼應地雷區：`TryParse` 單獨會吃 `"99"`，所以 `[AllowedValues]` 要先擋）、只有 `intent=="search"` 才放行、`JsonException` catch 回 null。`ResponseSchema` 改用 Gemini 大寫 `Type`(OBJECT/STRING)。

**③ Result**：

- `dotnet build` → **0/0**；**無新增 NuGet**。
- **實作 review 子代理結論：SHIP**。安全核心、真實 API 適配、分層、資源釋放（所有 `HttpResponseMessage`/`JsonDocument` 皆在 `using`）、重試邊界（0..MaxRetries 有界、無無窮迴圈）皆正確。
- **依 review 採納一個 MEDIUM 修正**（直接服務活動「絕不變 500」紅線）：HttpClient **逾時**會擲 `TaskCanceledException`（非 `HttpRequestException`），原本會漏接變成 500。已加 `catch (OperationCanceledException) when (!ct.IsCancellationRequested)` → 當可重試暫時失敗、耗盡回 503；真正的呼叫端取消仍往外傳。另採納 trivial 修正：`retryDelay` 解析改用 `InvariantCulture`。
- **保留活動原文、僅記錄的 LOW**：空字串欄位以 `is not null` 判斷（schema enum 不含 `""`，模型不會吐）；非 429 的 4xx 也會重試（活動範本亦如此）。

**驗證方式（對照活動 §1b）**：
- [x] 傳輸（client、retry/backoff）與翻譯（prompt、structured output、不可信輸出處理）拆分為兩類別
- [x] 反序列化 → 驗證 → 白名單映射，任一步失敗回 null；`intent != "search"` 回 null
- [x] 金鑰未設定 / 重試耗盡 → `AiServiceUnavailableException`（step 3 Web 轉 503）
- [~] 對真實 Gemini 的 structured output 實際回應形狀 —— **step 5 live 煙霧測試驗證**（需 user key）

---

## 練習 1c — Web：接線　✅（commit 6223195）

**① Asked**：`OrdersApiController` 提供 `POST /api/orders/search`，Controller 薄、只轉接 service，把「服務不可用」轉 503；`Program.cs` 接線。

**② Done**：

*計畫驗證子代理結論*：**Plan sound**，逐點 CONFIRMED（Web 已同時參照 Core+Infra；`AddHttpClient<,>` 在 ASP.NET 共用框架內、無需新套件；`CalculateTotal`/`ServiceResult` 成員/domain 屬性齊備；user-secrets `Gemini:ApiKey` 會綁到 `GeminiOptions.ApiKey`；DI 生命週期無 captive dependency）。修正/釐清：
- **build blocker**：`Program.cs` 要加 `using OrderHub.Core.Ai;`、`using OrderHub.Infrastructure.Gemini;`（已加）；controller 的 usings 照活動原文（已含）。
- `result.Value` 是 nullable → 用 `result.Value!`（活動原文已如此）。
- **REFUTED**「一定要 `MapControllers` 才有屬性路由」：`MapControllerRoute` 與 `MapControllers` 共用同一 endpoint data source，屬性路由本就會生效；但加 `app.MapControllers()` 慣例且無害、無路由衝突（已加）。
- 提醒：user-secrets 預設只在 **Development** 環境載入；非 Development 則回退環境變數 `GEMINI_API_KEY`，都沒有則擲 → 503（by design）。

*實作*：
- `Controllers/Api/OrdersApiController.cs`（照活動原文）：`[ApiController]`+`[Route("api/orders")]`，`[HttpPost("search")]`；失敗 → 422（`UnprocessableEntity`），成功 → `Ok` 投影（金額走 `IOrderService.CalculateTotal`）；`catch (AiServiceUnavailableException)` → 503。`SearchOrdersRequest.Text` 加 `[Required]`。
- `Program.cs`：加 4 行 Gemini 接線（`Configure<GeminiOptions>`、`AddHttpClient<IGeminiJsonClient, GeminiGenerateContentClient>`、`AddScoped<IOrderQueryTranslator, GeminiOrderQueryTranslator>`、`AddScoped<IOrderSearchService, OrderSearchService>`）+ `app.MapControllers()`。
- **未動 `appsettings.json`**（CLAUDE.md：改動前先問）；`Gemini:Model` 用 `GeminiOptions` 程式碼預設，`Gemini:ApiKey` 走 user-secrets。

**③ Result**：

- `dotnet build` → **0/0**。
- **本步的「實作後驗證」= step 5 的 live 煙霧測試**（直接對真實 controller 端到端打 `POST /api/orders/search`，是最強的 after-check），故此處不另外派 review 子代理審 DI 接線（接線薄、已計畫驗證、build 綠）。

**驗證方式（對照活動 §1c）**：
- [x] `POST /api/orders/search` 路由與 controller 就位、build 綠
- [x] Controller 裡沒有任何 Gemini/HttpClient 細節（全封裝在 Infrastructure）
- [~] 「查得出結果 / 刪除→422 / 無關→unsupported / 拔 key→503」 —— **step 5 live 驗證**

---

## 步驟 4 — 安全/白名單邏輯單元測試（mock 掉 Gemini）　✅（commit c74f78e）

**① Asked**（我對活動的補強，非活動明列步驟）：活動的「安全對待模型輸出」是重點，但活動的驗證全靠 live API。我加**離線、確定性**的單元測試,把活動的紅線變成可重跑的證明——不需要金鑰。

**② Done**：

*「開工前」驗證*（此步的 before 檢查）：直接對照活動的紅線清單設計測試案例——刪除意圖→拒、無關輸入→unsupported→拒、無 filter→拒、壞 enum/日期→拒、金鑰未設/上游掛→不可吞成 null。用**手刻 fake**（無新 NuGet）：`FakeGeminiClient : IGeminiJsonClient`、`FakeTranslator : IOrderQueryTranslator`,**只替換 HTTP 邊界與 service 的翻譯器,不替換受測的程式**。

*實作*：兩個測試檔（15 個新測試）——
- `GeminiOrderQueryTranslatorTests.cs`（7）：valid→映射、`intent:"unsupported"`→null（刪除紅線）、只有 intent 無欄位→非 null 空查詢（交 service 擋）、`status:"99"`→null（AllowedValues 先擋）、壞日期→null、非法 JSON→null（不炸）、上游 `AiServiceUnavailableException`→**往外傳不吞**。
- `OrderSearchTests.cs`（8）：service 白名單（空查詢/翻譯 null/無 filter/日期反序→Fail、valid→Ok 且筆數正確）+ repo（status/tier 過濾、日期含當日且排除隔日、`Take(100)` 上限、由新到舊排序）。用真實 `OrderRepository` + InMemory DB。

*「實作後」驗證*（after 檢查）：派**對抗式測試 review 子代理**專問「這些測試若把安全檢查移除/反轉,會不會失敗?有沒有假通過?」結論:**TRUSTWORTHY**,全部 mutation-killing、無 vacuous。子代理**實測確認** `Enum.TryParse<OrderStatus>("99")` 會回 `true`(值 99)——證明 `[AllowedValues]` 這道閘是**必要且有作用**的(不是多餘)。依其三點建議做了兩個 hardening:`EmptyQuery`/`DateFromAfterDateTo` 補上 `ErrorMessage` 斷言(確認是對的守衛觸發)、cap 測試改為**完整**由新到舊斷言(非只比頭尾)。

**③ Result**：

- `dotnet test` → **49 綠**（34 既有 + 15 新）/ 0 失敗。
- 這 15 個測試把活動「安全對待模型輸出」的每條紅線,固化成不依賴金鑰、可重跑的回歸測試。

**驗證方式**：
- [x] 刪除意圖 / 無關輸入 → 拒（翻譯器 + service 兩層）
- [x] 壞 enum(`"99"`)、壞日期、非法 JSON → 一律 null,不丟例外
- [x] 上游不可用 → 例外往外傳（Web 轉 503），不被吞成「無法理解」
- [x] repo 查詢:過濾、含當日、`Take(100)`、排序皆有測試

---

## 步驟 5 — 對真實 Gemini 的 live 煙霧測試　✅（commit ed3b8ee）

**① Asked**（活動 §1c 的驗證方式，需真實金鑰）：跑起網站,對 `POST /api/orders/search` 打真實請求:正常查得出結果、刪除意圖→422、無關輸入→拒、拔 key→503。

**② Done / ③ Result**（app 於 Development 跑在 :5150,金鑰由 user-secrets 載入,我全程未讀金鑰）：

- **除錯過程(照實記,這是本步最有價值的部分)**:
  - 第一次打 → **503「重試 4 次後仍失敗」**。因 401/403 會有不同訊息,判定金鑰**有**載入、是上游 4xx/5xx。
  - 為診斷,在 client 非成功分支加一行**記錄上游狀態 + 回應片段(不含金鑰)** 的 log(順手的 observability 改善,保留)。重跑 → 看到真因是 **429**,且 body 寫 **`limit: 0, model: gemini-2.0-flash`**——這把金鑰對 **`gemini-2.0-flash` 免費配額為 0**(不是程式錯、不是 schema 錯:請求格式與 structured output schema 都被接受了)。
  - 用 **`Gemini__Model` 環境變數覆寫**(不動程式、不碰金鑰)改成 **`gemini-2.5-flash`** → 成功。最後把 `GeminiOptions.Model` 程式碼**預設改為 `gemini-2.5-flash`**(仍可用 `Gemini:Model` 覆寫)。

- **live 結果**:
  - `{"text":"上個月金卡會員取消的訂單"}` → **HTTP 200**,回 2 筆:id 137(陳志明/Gold/Cancelled/2026-07-15)、id 155(劉思穎/Gold/Cancelled/2026-07-07)。**皆金卡、皆已取消、皆落在「上個月」(今天 2026-08-07 → 7 月)**——LLM 把中文轉成 `{status:Cancelled, memberTier:Gold, dateFrom/dateTo=7月}`,查詢仍走 EF Core。✓
  - `{"text":"幫我把所有訂單刪掉"}` → **HTTP 422 「無法理解的查詢」**,資料毫髮無傷(紅線)✓
  - `{"text":"請給我一份番茄炒蛋的食譜"}`(無關) → **HTTP 422 「無法理解的查詢」** ✓
  - `{"text":""}` → **HTTP 400** ModelState「text 為必填」(清楚驗證錯誤,非 500)✓
  - **503 上游不可用**:前述 `gemini-2.0-flash` 配額 0 → 重試耗盡 → **HTTP 503「請稍後再試」(非 500)**,即為一個真實的 503 示範 ✓(活動要求的「拔 key→503」同一條路徑:金鑰未設也走 `AiServiceUnavailableException`;已由步驟 4 單元測試覆蓋,故不再實際刪 user 的 secret)。

**本步採納的程式改動**（隨本 commit）：
- `GeminiGenerateContentClient.cs`:非成功回應記錄「狀態 + 回應片段」的 warning（診斷用,不含金鑰）。
- `GeminiOptions.cs`:預設模型 `gemini-2.0-flash` → **`gemini-2.5-flash`**（實測本金鑰前者免費配額為 0）。

**驗證方式（對照活動 §1 驗證清單）**：
- [x] 「上個月金卡會員取消的訂單」查得出結果,與狀態/會員條件一致（Gold+Cancelled+7 月）
- [x] 「幫我把所有訂單刪掉」→ 422「無法理解的查詢」,資料無恙
- [x] 無關文字（食譜）→ 422「無法理解的查詢」,不炸
- [x] 上游不可用（配額 0）→ 503 而非 500；空 text → 400 驗證錯誤而非 500

---

## 練習 2 — 同一個 service 接上網站頁面　✅（commit 8ebec97）

**① Asked**：體會分層的紅利——練習 1 的 `IOrderSearchService` **一行都不用改**,再接一個 MVC 入口。`GET /Orders/Search?q=...`,Controller 薄、View 綁 ViewModel、錯誤走頁面顯示。

**② Done**：

*探索（before）*：讀既有 `OrdersController`/`Index.cshtml`/`OrderRowViewModel`/`DisplayHelper`/`_ViewImports`,確認可**完全重用**——`OrderRowViewModel` 已存在、helper（`StatusBadgeClass`/`StatusLabel`/`Money`/`LocalTime`）由 `_ViewImports.cshtml` 的 `@using static ...DisplayHelper` 靜態匯入,活動範本的裸函式呼叫可直接用。

*實作*（照活動原文）：
- `ViewModels/OrderSearchViewModel.cs`（Query/ErrorMessage/`List<OrderRowViewModel>`/`HasSearched`）。
- `OrdersController`：建構子多注入 `IOrderSearchService`,加 `Search(string? q, ct)` action——空 q 回空表單;否則呼叫**同一個** `SearchAsync`,失敗填 `ErrorMessage`、成功投影 `OrderRowViewModel`(與 Index 逐欄一致、金額走 `CalculateTotal`);`catch AiServiceUnavailableException` → 填 `ErrorMessage`(不變 500)。
- `Views/Orders/Search.cshtml`（綁 `OrderSearchViewModel`、表格與 Index 一致、錯誤走 `alert-warning`）。
- `_Layout.cshtml` 導覽列加「AI 查詢」入口（`asp-controller="Orders" asp-action="Search"`）。

**③ Result**：

- `dotnet build` → **0/0**;`dotnet test` → **49 綠**（web 變更不影響測試）。
- **live 頁面實測**（app :5150）：
  - 導覽列「AI 查詢」連結存在;`/Orders/Search` 標題「自然語言查訂單」。
  - `?q=上個月金卡會員取消的訂單` → 頁面 2 列:#137 陳志明(已取消,2026-07-15)、#155 劉思穎(已取消,2026-07-07)——**與練習 1 API 結果一致**（分層紅利:同一 service）。
  - `?q=幫我把所有訂單刪掉` → 頁面 `alert-warning`「無法理解的查詢」,非錯誤頁。
- **實作 review 子代理結論：SHIP**。逐點確認:controller **零** Gemini/HttpClient 參照、重用 service 未複製邏輯、薄 controller、View 綁 ViewModel、錯誤不變 500、與 Index 一致、Razor 預設編碼無 XSS、DI 第 4 個建構子參數不破壞任何呼叫點。cosmetic nits（保留活動原文、僅記錄）：業務拒絕與上游中斷都用 `alert-warning`（API 端有分 422/503,頁面可考慮 `alert-danger` 區分）；nav 標籤「AI 查詢」與頁標題「自然語言查訂單」用字不同。

**驗證方式（對照活動 §練習2）**：
- [x] 頁面查「上個月金卡會員取消的訂單」,結果與練習 1 API 一致
- [x] 「幫我把所有訂單刪掉」→ 頁面「無法理解的查詢」警示,非錯誤頁
- [x] Controller 裡沒有任何 Gemini/HttpClient 細節（全封裝在 Infrastructure）
- [~] 拔掉 key → 頁面清楚錯誤（同 API 的 `AiServiceUnavailableException` 路徑;已由單元測試 + step 5 的 503 覆蓋）

---

## 最終總結（活動 3）

| 步驟 | commit | 內容 | 驗證 |
| --- | --- | --- | --- |
| 基線 | f3edeac | EXECUTION-LOG-3、落地決定（真實 API/金鑰安全）| build 0/0、test 34 綠 |
| 1a | 649a99e | Core 白名單參數/介面/service + repo SearchAsync | 計畫子代理 CONFIRMED、review SHIP、34 綠 |
| 1b | b43b7dd | Infra Gemini client（真實 generateContent）+ 翻譯器 | 無新 NuGet、review SHIP、採納逾時→503 修正 |
| 1c | 6223195 | Web API `POST /api/orders/search` + Program 接線 | build 0/0（after-check = step 5 live）|
| 步驟4 | c74f78e | 安全/白名單單元測試（離線 mock）| 對抗式 review TRUSTWORTHY、34→49 綠 |
| 步驟5 | ed3b8ee | live Gemini 煙霧測試 + 診斷 log/預設模型 | 200/422/422/400/503 全對 |
| 練習2 | 8ebec97 | 網站頁面（重用同一 service）+ 導覽列 | review SHIP、頁面結果與 API 一致 |

- **交付**：Core `Ai/` 3 檔 + `OrderSearchService`；Infra `Gemini/` 4 檔 + `OrderRepository.SearchAsync`；Web `OrdersApiController` + `Program.cs` 接線 + `Orders/Search` 頁面 + 導覽列；15 個安全單元測試。
- **建置/測試**：`dotnet build` 0/0；`dotnet test` **34 → 49 綠**。
- **live**：自然語言查詢→200（Gold+Cancelled+上月）、刪除意圖→422、無關→422、空 text→400、配額不足→503（皆非 500）。
- **驗證方式**：每個程式步驟「計畫子代理 + 實作 review 子代理」；安全邏輯離線單元測試 + 對抗式測試 review；功能 live 端到端實測。
- **落地決定**：Gemini 傳輸層對齊真實 `generateContent`（活動端點為假想形狀）；模型預設 `gemini-2.5-flash`（本金鑰 `gemini-2.0-flash` 免費配額為 0，`Gemini:Model` 可覆寫）。
- **安全**：API key 存 user-secrets、不進 git、**agent 全程未讀**；建議 user 於 `.claude/settings.json` 加 `deny Read(**/UserSecrets/**)`（自動加入被 harness 權限檔防護擋下）。
- **只做本地 commit，未 push**（比照活動 2）。
- 心得與「對這個訓練的看法」記於 [`documents/PROCESS.md`](documents/PROCESS.md)（第三階段）。

### 給維護者的建議（詳見 PROCESS.md）
1. 活動的 `/v1/interactions` + `gemini-3.5-flash` 端點/模型與 Google 現行 API 不符,建議改用真實 `v1beta/models/{model}:generateContent`。
2. 提醒「免費配額因模型而異」,先用 ListModels 挑有配額的 flash 模型。
3. schema 的 `type` 大小寫（真實 API 用大寫 `OBJECT`/`STRING`）。
