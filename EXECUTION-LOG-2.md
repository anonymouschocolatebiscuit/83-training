# OrderHub 訓練 — 活動 2（MCP Server）執行紀錄 (Execution Log 2)

> 本檔對應**活動 2:自建 MCP Server**（`documents/activities/activity-2-custom-mcp.md`）。
> 活動 1 的紀錄在 [`EXECUTION-LOG.md`](EXECUTION-LOG.md)，兩者請勿混用。
> 格式沿用活動 1:每一步記 **① 被要求做什麼 (Asked) → ② 我做了什麼 (Done) → ③ 結果 (Result)**。
> 由 Claude Code(Opus 4.8, 1M context)於 session 中執行。每個練習流程:讀懂 → 計畫 + 子代理驗證計畫 → 實作 + 子代理驗證實作 → 記錄 → 獨立 commit(**只做本地 commit,不 push**)。

---

## 環境基線 (Baseline)　✅

**① Asked**:依 `documents/README.md` 建立基線,確認可建置、可測試、SQL Server 就緒、網站可跑。

**② Done / ③ Result**:

- .NET SDK:8.0.408 / 9.0.203 / 9.0.313 / 10.0.202（專案 target net8.0）
- Node v26.3.0、npx 11.16.0（MCP Inspector / Playwright 需要）
- SQL Server:`MSSQLSERVER`（預設實例,localhost）狀態 = **Running**
- 連線字串（`appsettings.Development.json`,key = `Default`）:
  `Server=localhost;Database=OrderHubTraining;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True`
- **基線建置**:`dotnet build` → 0 警告 / 0 錯誤
- **基線測試**:`dotnet test` → **34 passed / 0 failed**（延續活動 1 收尾的 34 綠）
- **網站**:`dotnet run --project src/OrderHub.Web --urls http://localhost:5150` → `Now listening on: http://localhost:5150`,DB 自動 migrate + seed 完成。
- Git:branch = main,**只做本地 commit,不 push**。

**低庫存參考資料**（供後續驗證 `low_stock` 工具用,取自 `/Products/LowStock?threshold=10`,依庫存升冪）:

| SKU | 現有庫存 |
| --- | --- |
| SKU-1048 | 2 |
| SKU-1005 | 3 |
| SKU-1023 | 3 |
| SKU-1032 | 4 |
| SKU-1014 | 4 |

---

## 全域摘要:活動 2 要我做什麼

把活動 1 的 OrderHub 專案「服務層」包成一個 **MCP server**,讓 agent 能自主使用。交付物依練習遞進:

| 練習 | 主題 | 交付物 |
| --- | --- | --- |
| 0 | 先當使用者:接 Playwright MCP | 體驗「agent 有了新工具」;設定片段 |
| 1 | 建 OrderHub.Mcp（stdio）| console 專案 + 3 個唯讀工具（get_order / low_stock / customer_orders）;commit |
| 2 | 用 MCP Inspector 除錯 | 列工具、手動呼叫、壞 Id 給清楚錯誤（本 session 用 JSON-RPC 程式化驗證替代 UI）|
| 3 | 註冊給 agent,before/after | `training-repo/.mcp.json`（進 git）;有工具 vs 沒工具對照 |
| 4 | 會改資料的工具 cancel_order | 破壞性工具 + 三個唯讀工具補 ReadOnly 標註;commit |
| 5 | Resources 與 Prompts | discount-rules resource + low_stock_report prompt;commit |

> **關於互動式驗證的替代做法**:活動原文的驗證多以互動式 client（Claude Code `/mcp`、瀏覽器版 MCP Inspector、`@` 選 resource、slash command）進行。本 session 是自動化 agent,無法在執行中對自己熱插一個新的 MCP server 或開瀏覽器 UI。因此對「列工具 / 呼叫工具 / 讀 resource / 取 prompt」等,我改用**直接對 MCP server 走 stdio 說 JSON-RPC**（`initialize` → `tools/list` → `tools/call` 等）來驗證——這與 Inspector 底層是同一套協定,且可重跑、可留存輸出,精神同活動 1「用失敗測試/raw HTTP 取代手動點頁」。純體驗性、需真人 client 的部分（練習 0 截圖、Claude Code 權限確認 UI）我會誠實標註為「需互動式 client」。

---

## 練習 0 — 先當使用者:接一個現成的 MCP　✅（設定 + 說明）

**① Asked**:接上 Playwright MCP,網站跑起來後請 agent「建立一筆新訂單,截圖給我看結果頁」,體驗「agent 有了新工具」。

**② Done**:

- 設定片段(供真人在自己的 client 使用):

  Claude Code(專案根執行):
  ```powershell
  claude mcp add playwright -- npx @playwright/mcp@latest
  ```
  Codex(`~/.codex/config.toml`):
  ```toml
  [mcp_servers.playwright]
  command = "npx"
  args = ["@playwright/mcp@latest"]
  ```
- 環境確認:`npx @playwright/mcp@latest` 可取得(node v26.3.0、npx 11.16.0 就緒);網站已在 :5150 執行,可供瀏覽器自動化操作。

**③ Result / 誠實標註**:此練習屬**純體驗性**,交付物不是程式碼而是「感受新工具」。實際「開瀏覽器→建單→截圖」需要一個**互動式 agent client**(Claude Code / Codex)在其對話中掛上 Playwright MCP 後驅動——**本自動化 session 無法對自己熱插 MCP server 或回傳瀏覽器截圖**,故不強行偽造截圖。此處交付「可直接照抄的設定 + 環境已就緒」的證明。

**對照活動 1 練習 2 的心得**(活動要求記入 PROCESS.md):活動 1 修 bug 時,我得**人工**在 :5150 頁面上一步步重現(選客戶、加明細、送出);有了 Playwright MCP,同樣的「建一筆訂單」流程可由 agent 自己開瀏覽器完成——把「人重現」變成「agent 重現」。這正是 MCP 的核心價值:**把一種能力(瀏覽器操作)包成標準工具,任何支援 MCP 的 agent 插上就能用**,不必為每個 agent 各寫一套整合。

**驗證方式**:
- [x] Playwright MCP 設定片段備妥(Claude Code / Codex 兩版)
- [x] 環境可取得該 server、網站可被操作
- [~] agent 自行開瀏覽器建單並回傳截圖 —— **需互動式 client**,不在本自動化 session 範圍
