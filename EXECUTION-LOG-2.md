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

---

## 練習 1 — 建立 OrderHub MCP Server(stdio)　✅（commit f508621）

**① Asked**:建一個 C# console 專案,透過 stdio 對外提供 3 個唯讀工具(get_order / low_stock / customer_orders),照專案分層注入 service / repository,金額重用 `IOrderService`,不重複折扣規則。

**② Done**:

*計畫(先計畫再動手)*:`src/OrderHub.Mcp` console 專案 → 加入 sln → 加 `ModelContextProtocol` + `Microsoft.Extensions.Hosting` 套件 → 參照 Core/Infrastructure → Program.cs(log 走 stderr、`AddDbContext` 用 `Default` 連線字串 + fallback、DI 接 repo/service、`AddMcpServer().WithStdioServerTransport().WithTools<OrderHubTools>()`)→ `OrderHubTools.cs` 三個工具投影成匿名物件。

*計畫驗證子代理(唯讀、對抗式)結論*:逐條比對程式碼後**全 CONFIRMED**,並抓出/提醒:
- `IOrderService` 在 `OrderHub.Core.Services` 命名空間、repo 介面在 `OrderHub.Core.Interfaces`——**兩個 using 都要**(已照做)。
- `GetOrderAsync` → `GetWithDetailsAsync` 有 `Include(Customer).Include(Items).ThenInclude(Product)`(`OrderRepository.cs:45-50`),投影不會 NRE。
- Core/Infrastructure 為 `net8.0`、EF Core SqlServer `8.0.11` 會**透過 Infrastructure 傳遞**,新專案不必自己加 EF 套件;建議 Mcp 也用 `net8.0` 對齊(已照做,見下)。
- 匿名投影已切斷 Order↔Customer↔Items 循環參照,序列化安全。
- 三個地雷:stdout 要乾淨(log→stderr)、DbContext 是 Scoped(工具解析要 per-call scope)、外部 NuGet API 需實測。

*實作與計畫的差異(照實記)*:
1. `dotnet new console` 在本機(有 SDK 10）預設吐出 **net10.0** 且抓到 **ModelContextProtocol 2.0.0**(活動寫 `--prerelease`,但 2.0.0 已是**正式版**)。我把 TargetFramework 改回 **net8.0**、`Microsoft.Extensions.Hosting` 釘 **8.0.1**,與全 solution 的 .NET 8 / EF 8.0.11 對齊(採納計畫驗證子代理建議)。
2. 首次 build 失敗:`GetConnectionString` 找不到多載(CS1501)——漏了 `using Microsoft.Extensions.Configuration;`(活動範本原本就有,我謄漏)。補上後 build 成功。
3. 三個工具、Program.cs 其餘均照活動範本;log 走 stderr;工具名由 SDK 自動轉 snake_case。

**③ Result**:

- `dotnet build src/OrderHub.Mcp` → **0 警告 / 0 錯誤**。
- **執行期實測(關鍵:編譯過 ≠ 能跑)**:寫了一支 Node 的 MCP stdio JSON-RPC client,直接對「已編譯的 DLL」(不用 `dotnet run`,避免 build 訊息污染 stdout)跑 `initialize → tools/list → tools/call`:
  - `initialize` 回 serverInfo `OrderHub.Mcp`。
  - `tools/list` 列出 **3 個工具**,name 為 `get_order` / `low_stock` / `customer_orders`,description 與參數說明如所寫,`low_stock` 的 `threshold` 帶 `default:10`。
  - `low_stock(threshold=10)` → 5 筆,與基線 `/Products/LowStock` **完全一致**:SKU-1048(2)、1005(3)、1023(3)、1014(4)、1032(4),依庫存升冪。(庫存並列 4 的 1014/1032 次序與網頁互換——因僅以 StockQuantity 排序,並列者次序不保證;集合與數量相同。)
  - `low_stock(threshold=3)` → 1 筆(SKU-1048,庫存2)✓。
  - `get_order(1)` → 完整明細,Subtotal/Total 由 `IOrderService` 算出(Standard 客戶 DiscountRate=0,Total=Subtotal=12660)✓。
  - `get_order(999999)` → `找不到訂單 999999`(**清楚訊息,非 exception dump**)✓。
  - `customer_orders(1)` → 該客戶 16 筆訂單摘要 ✓。
  - 連續多次工具呼叫都正常 → 驗證了計畫子代理擔心的「Scoped DbContext 被工具捕獲」問題:SDK 對每次呼叫建立獨立 scope,無「second operation on this context」錯誤。
- **實作驗證子代理(唯讀 code review)結論:Ship**。無 Critical/Major。列出的:
  - *Minor* — `LowStock` 用 `GetActiveAsync()` + 記憶體過濾,而 repo 已有 `GetActiveBelowStockAsync(threshold)`(DB 端過濾)。**我的取捨**:此練習照活動範本原文(範本即用 `GetActiveAsync`),不擅自偏離讓學員對不上文件;把此更佳做法記於此(結果等價,差在把過濾/排序下推到 SQL、且更貼合活動自己「重用既有方法」的精神)。
  - *Nit* — 該子代理另指「`using OrderHub.Core.Services;` 未使用」,**此點有誤**:`IOrderService` 正是在 `OrderHub.Core.Services`,此 using 為必要(計畫子代理已確認命名空間)。保留不動。跨代理互相校正,正是雙重驗證的價值。
  - *Nit* — 工具類別無 namespace(全域)——沿用活動範本,保留。

**驗證方式(對照活動)**:
- [x] `dotnet build src/OrderHub.Mcp` 成功
- [x] 三個工具皆可列出、可呼叫,description/參數如所寫
- [x] `low_stock` 結果與 `/Products` 低庫存一致
- [x] `get_order` 用不存在 Id → 清楚錯誤訊息
- [x] 一個獨立 commit(f508621)

---

## 練習 2 — 用 MCP Inspector 除錯　✅（commit 24e1836）

**① Asked**:不接 agent,先用官方 MCP Inspector 手動測工具——這是 MCP 開發的標準除錯流程。列工具、手動呼叫 `low_stock`(與 `/Products` 對照)、用不存在的 Id 呼叫 `get_order`(要清楚錯誤而非 exception dump)。

**② Done**:

*方法(誠實標註)*:活動原文 `npx @modelcontextprotocol/inspector dotnet run ...` 會開瀏覽器 UI,本自動化 session 無法操作 GUI。改用**同一個官方套件的 `--cli` 非互動模式**,對「已編譯的 DLL」執行(不用 `dotnet run`,避免 build 訊息污染 stdout 協定通道)。這是真正跑官方 Inspector,只是走 CLI 而非瀏覽器。本練習不改任何程式碼,是純除錯/驗證步驟;完整的「計畫子代理 + 實作 review 子代理」雙重驗證已於練習 1 前置完成,此處以「官方 Inspector 實跑 + 獨立子代理對照網頁」作為驗證。

*實跑指令與結果*:
- `--method tools/list` → 列出 3 個工具,`inputSchema` 完整:`get_order.id`、`customer_orders.customerId` 為 **required**;`low_stock.threshold` **非 required 且帶 `default:10`**。descriptions 與參數說明如練習 1 所寫。
- `--method tools/call --tool-name low_stock --tool-arg threshold=10` → 5 筆,與基線 `/Products/LowStock` 一致(SKU-1048/1005/1023/1014/1032)。
- `--tool-name get_order --tool-arg id=999999` → `content[0].text = "找不到訂單 999999"`,**無 isError、無 stack trace**——證明錯誤處理是「給 agent 讀的清楚訊息」。

**③ Result**:

- 官方 Inspector CLI 三項檢查全過。所有 server log 走 stderr,stdout 只有乾淨的 JSON-RPC(協定通道未被污染)。
- **獨立驗證子代理**(從零自己抓網頁 + 自己跑 Inspector CLI)結論 **CONFIRMED**:`low_stock` 於 threshold=10 與 threshold=3 兩檔,輸出與網頁**以集合比對完全一致**(SKU + 庫存量相同),皆依庫存升冪;`threshold=3` 僅回 SKU-1048(庫存2),證明門檻是**嚴格小於(`<`,exclusive)**。
- **唯一差異(已記錄)**:庫存並列 4 的 SKU-1014 / SKU-1032,網頁次序是 1032→1014、工具是 1014→1032。因兩邊都只以 `StockQuantity` 排序,並列項的次序不保證——集合成員與數量序列相同,不影響正確性。子代理獨立觀察到同一現象。

**驗證方式(對照活動)**:
- [x] 三個工具都列得出來,description、參數說明如所寫
- [x] 手動呼叫 `low_stock`(threshold=10),回傳與 `/Products` 低庫存商品一致
- [x] `get_order` 用不存在的 Id,回應是清楚錯誤訊息而非 exception dump

---

## 練習 3 — 註冊給 agent,做 before/after 對照　✅（commit e521448）

**① Asked**:把 server 接進 CLI(`training-repo/.mcp.json`,進 git 全隊共用),親眼看「有工具 vs 沒工具」的差異;問「哪些商品庫存低於 5?」對照 MCP 關 / 開。

**② Done**:

*實作*:新增 `training-repo/.mcp.json`(活動範本原文,相對路徑、可全隊共用):
```json
{ "mcpServers": { "orderhub": { "command": "dotnet", "args": ["run", "--project", "src/OrderHub.Mcp"] } } }
```

*一個真實踩到的坑(值得記)*:先用 Inspector CLI 測 `dotnet run --project ...` → 回 `Connection closed`。追查發現是 **Inspector CLI 會把 `--project` 當成自己的旗標吃掉**,導致實際 spawn 的是沒有 `--project` 的 `dotnet run`(錯誤訊息:「找不到要運行的項目」)。這是**測試工具的 arg 解析怪癖,不是 `.mcp.json` 的問題**——真正的 agent client(Claude Code / Codex)會**原封不動**地 spawn 指令。為了驗證這點,我寫了一支小 client **直接照 `.mcp.json` 的 command/args 原樣 spawn**(cwd=training-repo),結果:`initialize` 回 `OrderHub.Mcp`、`tools/list` 三工具、`low_stock(5)` 正常回資料。**證明 config 對真實 client 可用**。

*before/after 對照*(問題:「哪些商品庫存低於 5?」):
- **沒有 MCP(繞遠路)**:本 session 自己就是活教材——我的 Claude Code toolset **並未掛上 orderhub MCP**,所以要回答低庫存,我得**繞路**:寫 JSON-RPC client、或用 Inspector CLI、或 `curl` 網頁再解析 HTML。等於 agent 得「自己想辦法查 DB / 讀碼 / 爬頁面」,多步且需要額外知識(連線字串、頁面結構)。
- **有 MCP(一步到位)**:`low_stock(threshold=5)` 一次工具呼叫即答完 → SKU-1048(2)、1005(3)、1023(3)、1014(4)、1032(4)(皆 <5)。agent 不需要知道 DB、不需要讀碼,description 就告訴它「這工具列低庫存在售商品」。

**③ Result**:

- `.mcp.json` 建立並經**照 config 原樣 spawn** 驗證可用(initialize + tools/list + low_stock(5) 全過)。
- **驗證子代理**(唯讀,審 config 的**團隊健壯性**)結論:JSON 合法、schema 正確、相對路徑可攜、無密鑰/絕對路徑/機器名;`dotnet run` 對訓練 repo 是對的選擇(自癒、不會用到過期 binary、與範本一致),唯一風險是**首次冷啟動 build 可能讓 client 連線逾時**——緩解法:先 `dotnet build` 一次再開 client(活動本身也這樣建議)。子代理另提醒:工具要回資料,teammate 端仍需 .NET 8 SDK + 本機 SQL Server + `OrderHubTraining`(handshake 不需 DB,查詢才需)。
- 誠實標註:真正在 Claude Code UI 裡 `/mcp` 看到 orderhub 三工具、以及「停用/啟用」的即時對照,需**互動式 client**;本 session 以「照 config 原樣 spawn 成功」+「本身沒掛 MCP 得繞路的親身經驗」作為等價證據。

**驗證方式(對照活動)**:
- [x] `.mcp.json` 進 git,一個獨立 commit(見下)
- [x] 對照實驗完成且記錄(有工具一步到位 vs 沒工具繞路)
- [~] Claude Code `/mcp` 看到 orderhub 三工具 —— **需互動式 client**;以「照 config 原樣 spawn 成功」等價驗證

---

## 練習 4 — 會改資料的工具:cancel_order　✅（commit a5e9b08）

**① Asked**:前三個工具都是唯讀;這題給一個**會改資料庫**的 `cancel_order`,體會授權與人工確認變成設計的一部分。工具只做轉接(規則在 `OrderService.CancelOrderAsync`),並回頭把三個唯讀工具補上 `ReadOnly` 標註。

**② Done**:

*計畫驗證(先確認外部 API 再動手,呼應計畫子代理提醒的「NuGet API churn」)*:活動範本用 `[McpServerTool(Destructive = true, Idempotent = false)]` 與 `(ReadOnly = true)`。我先查 `ModelContextProtocol.Core 2.0.0` 的 XML doc,確認 `McpServerToolAttribute` **確實有** `Destructive` / `Idempotent` / `ReadOnly` / `Name` / `Title` / `OpenWorld` 等屬性——範本在 2.0.0 仍適用,才動手。並讀 `OrderService.CancelOrderAsync`(`OrderService.cs:117-138`)確認:null→Fail「找不到指定的訂單」、非 Pending/Confirmed→Fail「狀態為 X 的訂單不可取消」、否則**先回補庫存再設 Cancelled**(即活動 1 客訴 3 修好的行為)。工具只需轉接 `result.Success ? 成功訊息 : 取消失敗:{ErrorMessage}`。

*實作*:`OrderHubTools.cs` 加 `CancelOrder` 工具(`Destructive=true, Idempotent=false`);三個唯讀工具加 `ReadOnly=true`。`dotnet build` → 0/0。

**③ Result**(以官方 Inspector CLI 實跑,DB 為真實 SQL Server):

- **標註正確**(`tools/list` 的 annotations):`get_order` / `low_stock` / `customer_orders` → `{"readOnlyHint":true}`;`cancel_order` → `{"destructiveHint":true,"idempotentHint":false}`。印證活動的「標註預設會反咬」:唯讀工具不標 ReadOnly 就會被當成可能破壞性。
- **破壞性 + 庫存回補(end-to-end)**:
  - 前:`get_order(1)` = Pending,含 SKU-1032 數量 1;`low_stock(10)` 顯示 SKU-1032 庫存 **4**。
  - `cancel_order(1)` → `訂單 1 已取消,庫存已回補`。
  - 後:`get_order(1)` = **Cancelled**;`low_stock(10)` 顯示 SKU-1032 庫存 **5**(4+1,透過工具本身就看到庫存被回補)。
- **清楚的拒絕訊息(非 exception dump)**:
  - 對同一筆再取消:`取消失敗:狀態為 Cancelled 的訂單不可取消`。
  - 取消一筆已出貨訂單(id=129,Shipped):`取消失敗:狀態為 Shipped 的訂單不可取消`。
- **回歸**:`dotnet test` 仍 **34 綠**(改動只在 Mcp 專案,未影響 Core/Infra/Tests)。
- **實作 review 子代理**結論:**SHIP**。逐條確認:thin pass-through(不重複規則)、標註預設已正確覆寫、真正的狀態守衛在 service 層(標註只是 hint)、失敗回可讀訊息無 stack trace、缺 id 不丟例外而回 Fail。僅一個 cosmetic nit(成功訊息對「零品項訂單」也寫「庫存已回補」)。

*誠實標註*:活動要求「對 agent 說取消訂單 X,觀察**權限確認提示**,按允許前資料不會被動到」——那個確認 UI 是 **client 端**依 `destructiveHint` 決定的行為(Claude Code 會參考、Codex 由 `approval_policy` 管)。我已驗證 server **有正確送出 `destructiveHint`**(這是觸發確認的依據);確認提示本身需互動式 client。
*資料副作用*:本練習確實改了訓練 DB(取消了訂單 1、SKU-1032/1044/1009 庫存回補)。可用 README 的重置指令還原種子資料。

**驗證方式(對照活動)**:
- [x] Inspector 中 `cancel_order` annotations 為 destructiveHint,三唯讀工具為 read-only
- [~] 對 agent 說「取消訂單 X」觀察**權限確認提示** —— server 已送 destructiveHint;確認 UI 需互動式 client
- [x] 取消一筆待處理訂單成功,庫存有回補(SKU-1032 4→5)
- [x] 再取消同一筆 / 取消已出貨訂單 → 清楚拒絕訊息而非 exception dump
- [x] 獨立 commit;EXECUTION-LOG-2 記錄

---

## 練習 5 — MCP 不是只有 tools:Resources 與 Prompts　✅（commit 待填）

**① Asked**:各做一個 Resource(server 提供的唯讀資料,由 client 決定何時放進 context)與 Prompt(預定義提示範本,像 slash command),體會它們和 Tool 的分工。

**② Done**:

*計畫驗證(先確認 2.0.0 API)*:查 `ModelContextProtocol.Core 2.0.0` XML doc,確認 `McpServerResourceType` / `McpServerResourceAttribute`(有 `UriTemplate`/`Name`/`MimeType`)、`McpServerPromptType` / `McpServerPromptAttribute`(有 `Name`)、以及主套件的 `WithResources` / `WithPrompts` 皆存在;`Microsoft.Extensions.AI.Abstractions 10.8.3`(提供 `ChatMessage`/`ChatRole`)已透過相依傳遞。範本在 2.0.0 適用,才動手。

*實作*:
- `OrderHubResources.cs` — `discount-rules` resource(`orderhub://discount-rules`,text/markdown,會員折扣規則)。
- `OrderHubPrompts.cs` — `low_stock_report` prompt(帶 `threshold` 參數,預設 10;內容引導 agent 用 `low_stock` 工具再產採購建議表)。
- `Program.cs` 接 `.WithResources<OrderHubResources>().WithPrompts<OrderHubPrompts>()`。`dotnet build` → 0/0。

**③ Result**(官方 Inspector CLI 實跑):
- `resources/list` → `會員折扣規則`(uri `orderhub://discount-rules`,mimeType text/markdown)✓
- `resources/read orderhub://discount-rules` → 回完整 markdown(Standard 不打折 / Silver 95折 / Gold 9折)✓
- `prompts/list` → `low_stock_report`,參數 `threshold`(required:false)✓
- `prompts/get low_stock_report threshold=5` → 展開成 User 訊息,`threshold=5` 已代入,且**內容叫 agent 去用 `low_stock` 工具**(prompt 引導 tool,兩原語合體)✓

*實作 review 子代理結論:SHIP*。分工分類正確(discount-rules 當 Resource、low_stock_report 當 Prompt)、註冊正確(三者鏈在同一 builder)、static 用得恰當。兩個 follow-up:
- *MEDIUM(設計)*:折扣率**硬寫在 resource 字串**,與 `OrderService.GetDiscountRate`(`OrderService.cs:143-145`:Gold 0.10、Silver 0.05、Standard 0)是**兩份真相**。子代理逐條比對確認**今天數值相符**,但程式改版會 drift。**我的取捨**:照活動範本用靜態字串;活動自己的地雷區也點名此事並說「resource 也可以動態組出內容」——列為改進項(可從 `GetDiscountRate` 動態組出,單一真相)。
- *子代理另一發現被我以實測反駁(交叉校正)*:子代理說「SDK 用方法名原樣 `LowStock`,prompt 寫 `low_stock` 是錯的」。**但 `tools/list` 實測顯示註冊名就是 snake_case `low_stock`**(練習 2/4 的輸出、活動也明載「LowStock → low_stock」)。故 prompt 的 `low_stock` 參照**正確**;此發現不成立。

**5c 思考題(記入 PROCESS.md)**:折扣規則用 Resource 給 vs 讓 agent 自己讀 `OrderService.cs`——差在**團隊共用、版本控制、規則改版只改一處**(且 agent 不必有讀原始碼的能力/權限就能拿到「權威背景知識」)。prompt 範本放 server vs 每人自己打一段——差在**一致性與可維護**:採購同事每週那句話統一版本,改流程改一次全隊生效,不會每人一個版本。

*誠實標註*:Claude Code 裡用 `@` 選 resource、`/mcp__orderhub__low_stock_report` 當 slash command 執行,需**互動式 client**;Codex CLI 目前只接 MCP 的 **tools**(resources/prompts 無 `@`/slash 介面)。故本 session 以官方 Inspector 驗證這兩個原語(正是活動對 Codex 用戶建議的路徑)。

**驗證方式(對照活動)**:
- [x] Inspector:Resources 讀得到 `orderhub://discount-rules`;Prompts 能帶 `threshold` 取得展開訊息
- [~] Claude Code `@` 選 resource / 一鍵 slash command —— **需互動式 client**;以 Inspector 等價驗證
- [x] PROCESS.md 記錄 5c 思考(見 PROCESS.md 更新);獨立 commit
