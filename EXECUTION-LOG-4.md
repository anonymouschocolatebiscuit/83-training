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

## n8n 練習 1–3 的共同前置:schema 研究（一次性 before 驗證）

**誠實的方法論調整**:活動的 n8n 練習 1–3 全靠瀏覽器 GUI 操作,無真程式碼可 build/test。三個練習**共用同一套 n8n 節點詞彙**(且練習 3 = 練習 2 + 一個 MCP 節點,是遞增的)。故我把「開工前對抗式驗證」對這三步**合併成一次**——派子代理用 WebSearch/WebFetch 對官方 n8n source/docs 查證我要用的每個節點的 `type` 字串、`typeVersion`、參數形狀、以及最容易錯的 **AI Agent 子節點連線形狀**(比照活動 3 把 1a/1b 合併的精神)。每個練習仍各自「實作 → 子代理 review → 記錄 → 獨立 commit」。

**schema 研究子代理結論(關鍵已對齊官方 source 驗證)**:
- 匯入最小需求:`name`/`nodes`/`connections`/`settings{executionOrder:"v1"}`;`id`/`versionId` 可省(n8n 匯入時自動配)。
- 節點層開關(`alwaysOutputData` 等)在**節點頂層**,不在 `parameters` 內。
- **AI 子節點連線**:以**子節點名為 key**,方向是**子節點 → agent**:`connections["<Gemini/MCP 節點名>"]["ai_languageModel"|"ai_tool"][0][0] = {node:"AI Agent", type:..., index:0}`(這是最易錯處,已明確驗證)。
- 各節點:Webhook `responseMode:"responseNode"`;Set `assignments.assignments[]`+`includeOtherFields:true`;HTTP `sendBody/contentType:"json"/specifyBody:"json"/jsonBody`;Code `mode:"runOnceForAllItems"/language:"javaScript"/jsCode`;Agent `promptType:"define"/text`;Gemini `modelName` + 憑證型別 **`googlePalmApi`**;IF `conditions`(filter)+`{type:"number",operation:"gt"}`;GitHub resource/operation + owner/repo 為 resourceLocator + 憑證 `githubApi`;**Data Table `n8n-nodes-base.dataTable` 確實存在**(newer 節點,需先在 UI 建表拿 `dataTableId`);MCP Client Tool `@n8n/n8n-nodes-langchain.mcpClientTool`,`endpointUrl`/`serverTransport:"httpStreamable"`/`authentication:"none"`/`include:"selected"`/`includeTools:[...]`。
- n8n 對匯入寬容:`typeVersion` 略有出入仍可匯入、打開再存即可;**必須精確的是 `type` 字串與連線 key**(已驗證)。

產物一律放 `documents/references/n8n-workflows/`,並附 `README.md` 逐字手動步驟;GUI-only 步驟標 `[~]`。

---

## 練習 1 — Hello Webhook　✅（commit 見下）

**① Asked**：n8n 最小迴路 trigger → 節點 → 回應。Webhook(POST、Respond 設「Using Respond to Webhook Node」)→ Edit Fields(加 `receivedAt={{ $now.toISO() }}`、Include Other Input Fields 開)→ Respond to Webhook(First Incoming Item)。

**② Done**：產出 `01-hello-webhook.json`(3 節點 + 連線)+ README 練習 1 段落。JSON 已設好活動兩個最易漏的點:`responseMode:"responseNode"`(預設 _Immediately_ 會忽略 Respond 節點)、`includeOtherFields:true`(不開會丟掉送進來的 body)、`receivedAt` 值 `={{ $now.toISO() }}`(`=` 前綴才是 expression)。

**③ Result**：

- `ConvertFrom-Json` 驗證:**valid JSON**。
- **實作 review 子代理結論:IMPORTABLE**。逐點 OK:三個 `type` 字串正確、三個關鍵參數值(`responseMode`/`includeOtherFields`/`respondWith`)正確、`receivedAt` expression 有 `=` 前綴、連線 keyed by source name 且 `main`/`index`/方向正確、節點名唯一無 dangling、envelope key 齊、每節點有 `id`/`position`/`typeVersion`。唯一 cosmetic nit:`webhookId:"hello"` 用了 path 而非 UUID(無害、不影響匯入)。
- **GUI-only 部分誠實標 `[~]`**:匯入後複製 Test URL、按 Execute 進 120 秒監聽、打 request、看綠勾——都需真人在編輯器互動,自動化 session 無瀏覽器可點。JSON 已把節點/連線/易漏參數設完,真人只需匯入 + 按 Execute + 打一發。

**驗證方式（對照活動 §練習1）**：
- [x] workflow JSON 可匯入(schema 對齊官方 source、review 子代理判 IMPORTABLE)
- [~] 回應含送的內容 + 時間戳 —— 需真人按 Execute 後打 request(GUI-only)
- [~] 理解 Test URL vs Production URL 差別 —— 概念已寫進 README;實際監聽需真人操作

---

## 練習 2 — 退單巡檢日報（主菜）　✅（commit 見下）

**① Asked**：端到端流程——排程 → 查近 30 天取消訂單(打**活動 3 的 `/api/orders/search`**,零新程式碼)→ AI Agent(Gemini)寫日報 → IF 退單筆數>0 → true 開 GitHub issue + 通知、false 存 Data Table 歸檔。

**② Done**：產出 `02-退單巡檢日報.json`(**9 節點**)+ README 練習 2 段落。流程:`Schedule Trigger → 查退單(HTTP) → 整理筆數(Code) → AI Agent(+Gemini) → IF → {true: 開 GitHub Issue → 通知; false: 歸檔 Data Table}`。JSON 已處理活動點名的每個地雷:
- HTTP `alwaysOutputData:true` 放**節點頂層**(放錯進 parameters 會被忽略、歸檔分支永不執行)。
- `整理筆數` Code 先濾掉 Always Output Data 的空 item 再算 `count`,輸出單一 `{count, orders}`;**節點名精確為「整理筆數」**供 IF 跨節點引用。
- IF 左值 `={{ $('整理筆數').first().json.count }}`(AI 輸出已無 count,跨節點回頭拿)、Number `gt` `0`;`main[0]`=true→GitHub、`main[1]`=false→Data Table。
- Gemini 子節點以 `ai_languageModel`、方向**子節點→agent** 連進 AI Agent。
- GitHub 標題 `={{ $json.output.split('\n')[0] }}`(JSON 內寫 `\\n` 才會得到 JS 能 split 的真換行)、通知帶 `report`/`issueUrl`。
- Gemini `modelName=models/gemini-2.5-flash`(活動寫的 3.5-flash 本金鑰不可用,沿用活動 3 落地決定)。
- 憑證/表/URL 一律不隨 JSON 匯入,以占位字串標示並在 README 寫清楚真人步驟。

**③ Result**：

- `ConvertFrom-Json` 驗證:**valid JSON**,9 節點;實測 GitHub 標題 expression 含 literal `\n`、IF 左值跨節點引用正確、IF true→GitHub/false→Data Table 未接反。
- **實作 review 子代理結論:IMPORTABLE(無 defect)**。8 個 critical check 全 OK,逐點確認:`alwaysOutputData` 在節點頂層✓、Gemini→Agent 用 `ai_languageModel` 且方向/命名一致✓、`整理筆數` 名稱與 IF 引用逐字相同✓、IF true/false 兩路未接反✓、所有連線引用的節點名(含中文名)都存在無 typo✓、所有 expression `=` 前綴正確✓、資料來源正確(`$json` 來自上游整理筆數、通知用跨節點 `$('AI Agent')` + GitHub 自身 `$json.html_url`)✓、每節點 type/typeVersion/position/id/name 齊✓。
- **GUI-only 部分誠實標 `[~]`**:Gemini 金鑰、GitHub PAT + repo、Data Table 實體表/`dataTableId`、練習 1 Production URL——都是帳號/環境綁定且 n8n 不匯出憑證,必須真人在 GUI 建;按 Execute 看 issue/歸檔也是編輯器互動。JSON 已把 9 節點、所有連線(含 AI 子節點連線與 IF 兩路)、所有 expression 與易漏參數設定完成。

**驗證方式（對照活動 §練習2）**：
- [x] workflow 結構與 expression 正確、可匯入(schema 對齊官方 source、review 判 IMPORTABLE)
- [~] Execute 後開 GitHub issue、收通知、日報數字與 `/Orders` 篩「已取消」一致 —— 需真人填憑證後執行
- [~] 改成查不到的條件 → 存 Data Table、不開 issue —— 需真人執行
- [x] 思考題「查什麼、怎麼查也交給 AI 自由發揮會失去什麼」→ 已答於 `documents/PROCESS.md` 第四階段

---
