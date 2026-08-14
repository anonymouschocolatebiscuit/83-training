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
