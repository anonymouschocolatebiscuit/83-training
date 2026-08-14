# OrderHub 訓練 — 活動 4（n8n 自動化：把人抽離流程）執行紀錄 (Execution Log 4)

> 本檔對應**活動 4：n8n 自動化**（`documents/activities/activity-4-n8n.md`）。
> 活動 1 紀錄在 [`EXECUTION-LOG.md`](EXECUTION-LOG.md)、活動 2 在 [`EXECUTION-LOG-2.md`](EXECUTION-LOG-2.md)、活動 3 在 [`EXECUTION-LOG-3.md`](EXECUTION-LOG-3.md)，四者請勿混用。
> 格式沿用前三活動：每一步記 **① 被要求做什麼 (Asked) → ② 我做了什麼 (Done) → ③ 結果 (Result)**。
> 由 Claude Code(Opus 4.8) 執行。每個程式步驟流程：**計畫 → 對抗式子代理驗證計畫 → 實作 → 子代理 review → 記錄 → 獨立本地 commit（不 push，比照活動 2/3）**。

---

## 全域摘要：活動 4 要我做什麼，以及誠實的可行性切分

活動 4 把前三活動合體：用 n8n 搭一條「排程巡檢 → 查訂單（活動 3 API）→ AI Agent（Gemini）寫日報 → 分流處置 → MCP（活動 2 工具）深挖明細」的自動化流程。它有四個區塊：

| 區塊 | 性質 | 本 session 能做到的程度 |
| --- | --- | --- |
| **補齊：MCP server 加開 HTTP transport** | **真程式碼**（`src/OrderHub.Mcp`） | ✅ **完整實作 + HTTP 端到端驗證**（最強證據） |
| 練習 1：Hello Webhook | n8n 瀏覽器 GUI | 產生可 Import 的 workflow JSON + 手動步驟；按 Execute 屬 `[~]` |
| 練習 2：退單巡檢日報 | n8n 瀏覽器 GUI | 產生可 Import 的 workflow JSON + 手動步驟；憑證/GitHub PAT/Execute 屬 `[~]` |
| 練習 3：MCP 合體 | n8n 瀏覽器 GUI | 產生可 Import 的 workflow JSON（加 MCP Client Tool）+ 手動步驟；`[~]` |

### 誠實的可行性切分（開工前與 user 確認，如活動 3 的金鑰決策）

- **自動化 session 無法點擊瀏覽器 GUI**：n8n 的練習 1–3 全靠在 `http://localhost:5678` 拖拉節點、填憑證、按 Execute 完成。我無法建立 n8n owner 帳號、無法點畫布。**不偽造截圖、不假稱「跑起來了」。**
- **唯一的真程式碼交付（補齊 — MCP HTTP transport）我完整做到底**：加套件（版本對齊**鎖住的 `2.0.0` 正式版**，非活動假設的 `2.0.0-preview.2`）、加 `Microsoft.AspNetCore.App` FrameworkReference、`Program.cs` 改雙 transport、build、`dotnet run -- --http` 跑起來、用 **JSON-RPC over HTTP** 對 `http://localhost:3001` 實測（initialize → tools/list → resources/list → prompts/list → 呼叫 `get_order`）。stdio 路徑行為不變。
- **n8n 練習 1–3**：經 user 同意（Recommended 選項），我**產生可 Import 的 workflow JSON**（存 `documents/references/n8n-workflows/`）+ 逐字手動步驟；凡真正需要真人在 GUI 的部分（建帳號、填 Gemini/GitHub 憑證、按 Execute、看執行紀錄）一律標 `[~]` 並註明原因，比照活動 2/3 標互動式步驟的做法。
- **模型名稱**：活動寫 `gemini-3.5-flash`，但活動 3 實測本金鑰該模型不存在/`gemini-2.0-flash` 免費配額為 0，落地用 **`gemini-2.5-flash`**——workflow JSON 一律用這顆。

---

## 環境基線 (Baseline)　✅

**① Asked**：建立基線，確認可建置、可測試。

**② Done / ③ Result**：

- **基線建置**：`dotnet build`（於 `training-repo/`）→ **0 警告 / 0 錯誤**。
- **基線測試**：`dotnet test` → **49 passed / 0 failed**（延續活動 3 收尾的 49 綠）。
- 專案 target net8.0；`src/OrderHub.Mcp/OrderHub.Mcp.csproj` 現有套件：`Microsoft.Extensions.Hosting 8.0.1`、`ModelContextProtocol 2.0.0`（**正式版，非 preview**）。
- 網站啟動：`dotnet run --project src/OrderHub.Web`（http://localhost:5150）。MCP stdio 啟動：`dotnet run --project src/OrderHub.Mcp`。
- Git：branch = main，**只做本地 commit，不 push**（比照活動 2/3）。程式在 `training-repo/`，執行紀錄在 repo 根目錄。

---

## 補齊 — MCP server 加開 HTTP transport（活動 2 的延伸）　✅（commit 見下）

**① Asked**：活動 2 的 server 走 stdio;但 n8n 的 MCP 節點只支援 SSE / streamable HTTP,不支援 stdio,所以練習 3 之前要幫**同一個** server 加開 HTTP 入口。工具、Resource、Prompt 一行都不改,只換 transport。帶 `--http` 走 HTTP(:3001),預設照舊走 stdio。

**② Done**：

*落地決定（誠實標註，與活動文件的差異）*：活動文件假設 csproj 的 `ModelContextProtocol` 鎖在 `2.0.0-preview.2`,要我用 `dotnet add ... --version 2.0.0-preview.2`。但**本 repo 實際鎖的是 `2.0.0` 正式版**(`OrderHub.Mcp.csproj:10`)。若照文件加 preview.2 會與正式版打架、restore NU1605 降版錯。故 `ModelContextProtocol.AspNetCore` 版本對齊 **`2.0.0` 正式版**(非 `--prerelease`、非 preview.2)。

*計畫驗證子代理（唯讀、對抗式）結論*:**全 CONFIRMED**,關鍵確認:
- `ModelContextProtocol.AspNetCore 2.0.0` 是**真實已發布版本**,其 nuspec 對 net8.0 宣告 `<dependency id="ModelContextProtocol" version="[2.0.0]" />`——**精確鎖 `[2.0.0]`,與本機 2.0.0 相符,不會 NU1605**(本機 nuget cache 尚無此套件,restore 需連網,已成功)。
- API 面實測(反射真實 net8 assembly):`WithHttpTransport(Action<HttpServerTransportOptions>)`、`HttpServerTransportOptions.Stateless`(bool)、`MapMcp(IEndpointRouteBuilder, string pattern="")` **在 2.0.0 都存在**;這兩個方法只在 AspNetCore assembly、不在 core(正如假設)。
- `MapMcp()` 預設 pattern=`""` → streamable HTTP 端點掛在**根路徑 `/`**;JSON-RPC client 要 POST 到 `http://localhost:3001/`(非 `/mcp`、非 `/sse`;`/sse` 需 `EnableLegacySse=true` 且與 `Stateless=true` 互斥)。
- 機器有 `Microsoft.AspNetCore.App 8.x` 共用框架;`FrameworkReference` 在 net8 console Exe 可用(且 AspNetCore 套件已隱含帶入,顯式保留只為對齊活動文件、無害)。
- **必守**:stderr-logging 那行(`LogToStandardErrorThreshold`)是 **stdio 分支的命脈**(stdout 是協定通道),只能留在 stdio 分支,不可進共用 helper、不可進 HTTP 分支。

*實作*：
- `OrderHub.Mcp.csproj`:加 `<PackageReference Include="ModelContextProtocol.AspNetCore" Version="2.0.0" />` + `<FrameworkReference Include="Microsoft.AspNetCore.App" />`。
- `Program.cs`:`if (args.Contains("--http"))` → `WebApplication.CreateBuilder` + `AddMcpServer().WithHttpTransport(o => o.Stateless = true).WithTools/Resources/Prompts` + `app.MapMcp()` + `app.Run("http://localhost:3001")`;`else` → **stdio 原樣**(含 stderr-logging 那行)。兩分支共用 `static void AddOrderHubServices(...)`(DbContext + 3 repo + IOrderService,全 Scoped,與原本逐字等價)。工具/資源/提示註冊兩邊**字元級相同**。

**③ Result**：

- `dotnet build` → **0/0**;`dotnet test` → **49 綠**(stdio 分支行為不變,測試不受影響)。restore 成功、**無 NU1605**。
- **實作 review 子代理結論:SHIP-WITH-NITS**——無正確性 bug、無 stdio 退步、無 DI/scope 風險(singleton server 在 streamable HTTP 下每請求開 scope 解析 Scoped DbContext,`get_order` HTTP 實測證實)、無 CLAUDE.md 違規(只有 composition root 碰 DbContext;`AddOrderHubServices` 抽取是為「兩 transport 註冊一致」的任務需求,非無關重構;套件是活動明確授權)。採納其兩個註解 nit:把「port 被占用會自動另選」的**誤導註解**改成「會擲例外、需自行釋放/改 port」;在 HTTP 分支加註「這裡 log 走 stdout 沒問題、勿把 stderr 那行複製過來」。

**HTTP 端到端驗證（最強證據，實際打 `http://localhost:3001/`）**：
- `tools/list` → **4 個工具**:`customer_orders`、`get_order`、`low_stock`(皆 `readOnlyHint:true`)、`cancel_order`(`destructiveHint:true, idempotentHint:false`)——annotations 完整保留。
- `resources/list` → `orderhub://discount-rules`(會員折扣規則,`text/markdown`)。
- `prompts/list` → `low_stock_report`(參數 `threshold`,選填)。
- `tools/call get_order {id:1}` → 完整訂單 JSON:客戶 蔡承翰/Standard、3 個品項、`Subtotal:12660.00`、`DiscountRate:0`、**`Total:12660.00`**——**與活動 2 EXECUTION-LOG-2 記錄的 stdio `get_order(1)` 數值完全一致**(Standard、Total 12660)。
- Stateless 模式下**免 initialize 握手**即可直接 `tools/list`/`tools/call`(每請求獨立),符合 `Stateless = true` 設計。

**stdio 未變驗證（不帶 `--http`，JSON-RPC over stdio，比照活動 2）**：
- `initialize` → `notifications/initialized` → `tools/list` → **列出全部 4 個工具**(`customer_orders, get_order, low_stock, cancel_order`)。
- `tools/call get_order {id:1}` → 回完整訂單(含各 LineTotal 與 `Total:12660`)。→ stdio 路徑一切正常,`.mcp.json`/Codex 設定完全不用動。

**驗證方式（對照活動「補齊」清單）**：
- [x] Streamable HTTP、URL `http://localhost:3001`:四個工具、resource、prompt 都列得出來(用 JSON-RPC 直打,非瀏覽器 Inspector——自動化 session 無 GUI,同活動 2 用 `--cli`/自寫 client 的精神)
- [x] 不帶 `--http` 照舊走 stdio:`tools/list` + `get_order` 皆正常
- [x] 獨立 commit

---
