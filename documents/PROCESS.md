# PROCESS.md — 我的練習心得

> 一個原則：**寫「具體發生的事」，不寫感想文。**
> 貼上當時真實的 prompt、真實的數字、真實的錯誤訊息——三個月後的你（和你的同事）才用得上。

#### 使用的 agent 與模型：

Claude Code，模型 Opus 4.8（1M context）。用兩個唯讀子代理做交叉驗證（開工前驗分析、收工前 review 全 diff）。

> 📄 **完整執行紀錄**：每一步「被要求做什麼 → 我做了什麼 → 結果」的逐步流水帳（含每個 bug 修復前的失敗測試輸出、頁面實測數據、以及 commit 對照表）另存於專案根目錄的 [`EXECUTION-LOG.md`](../EXECUTION-LOG.md)，供 PiC / reviewer 參考。

---

## 通用四問

### 1. 我的任務拆解

（開工前你把任務拆成哪幾步？實際做的時候順序有變嗎？為什麼變？）

- 先讀完 `documents/`（README、PROCESS、activity-guideline、references）與整個 `training-repo/`，建立基線（`dotnet build`、`dotnet test` = 28 綠、確認 SQL Server 已啟動）。
- 依練習順序 1→2→3→4，每個練習「先計畫、再動手、每步驗證、獨立 commit」。
- 順序上唯一調整：**練習 1（設定檔）與「跑起網站」我提前並行做**——設定檔和 bug 分析無相依，網站啟動要時間建庫植種，先啟動可省等待。

### 2. AI 幫上大忙的地方

（哪件事 agent 做得又快又好？**貼上當時的提問原文**，說明為什麼這樣問有效。）

- **一次讀懂三個 bug 的根因**：把三個 bug 一起交給一個「唯讀、對抗式」子代理獨立比對程式碼，要求逐條給 `file:line` 證據。提問重點：
  > 「不要相信以下說法，用你自己的程式碼閱讀確認或反駁每一點，附 file:line。」
  這樣問有效是因為它**強制列證據**、且獨立於我的假設，等於免費一次 code review。它回報三個 bug 全 CONFIRMED，並提醒「回歸測試要補上原測試缺的斷言」，直接影響我後面測試怎麼寫。

### 3. AI 誤導我的地方，與我如何發現

（agent 說錯／改錯／過度自信的時刻。你靠什麼抓到——對照程式碼？頁面實測？跑測試？）

- **我自己的 HTTP 驗證腳本誤報**：驗低庫存頁 `?threshold=0` 時，腳本回報「沒有顯示驗證訊息」，一度以為驗證壞了。實際抓原始 HTML 才發現——Razor 把中文訊息 HTML-encode 成 `&#x5EAB;...` 數值實體，我的 regex 比對中文字面所以誤判。**靠直接看 raw HTML（`field-validation-error` class 與 encode 後的訊息都在）抓到**，功能其實正常。教訓：驗證「畫面上有沒有某段中文」時，別用字面比對，改比對 class（如 `field-validation-error`）或先解碼。
- **雙重折扣只在「重載訂單」時才顯現**：一開始的直覺是「建單後直接 `CalculateTotal` 就會看到 0.81 倍」，但 `CreateOrderAsync` 沒有設 `order.Customer` 導航屬性 → 剛建立的 order 算總額時 tier 當成 Standard。實際雙重折扣發生在**明細頁重新載入（`GetWithDetailsAsync` 有 Include Customer）**時。對照程式碼才修正測試寫法（先重載再驗總額）。

### 4. 我會帶回日常工作的一招

（一個具體、可複製的做法，不要寫「要多驗證」這種口號——寫出**操作步驟**。）

- **「失敗測試先行」的修 bug 節奏**，每個 bug 都照做：
  1. 先寫一個回歸測試，斷言「正確」行為（例：取消後庫存應還原為 10）。
  2. `dotnet test --filter <該測試>` **確認它在修復前會 FAIL**（貼上錯誤：Expected 10, Actual 7）——這證明測試真的咬到 bug。
  3. 改原始碼（最小變更）。
  4. 全套件 `dotnet test` 轉綠。
  5. 一個 bug 一個 commit，message 寫「症狀→根因→修法」。
  這比「改完宣稱修好」可靠：測試先失敗才有意義，也順手補上了原本缺的斷言。

## 自我驗證（做到哪個階段答哪題）

### 第一階段 — Agentic Coding

練習 1

1. [x] 我能不看筆記說出三個專案（Web/Core/Infrastructure）各自的職責
       — Web：Controller/View/ViewModel，只接線與顯示；Core：Domain/Service 介面與商業邏輯（折扣、庫存、狀態轉移）；Infrastructure：EF Core DbContext、repository、migration、種子。
2. [x] 我核對過 agent 描述的建單流程，且**至少找出一處不精確或過度簡化的說法**
       — 「Gold 雙重折扣建單後就看得到」不精確：`CreateOrderAsync` 未設 `Customer` 導航，總額要在**重載含 Customer**（明細頁）時才呈現雙重折扣。
3. [x] 我知道商業邏輯應該放在哪一層、新增頁面要動哪些地方
       — 邏輯放 Core service；新頁要動 Controller→Service→Repository→ViewModel→View(+導覽列)→測試。

練習 2

1. [~] 三個 bug 我都先在頁面上重現過，才開始找程式
       — Bug1 在頁面（HTTP）重現：`/Orders?page=10` 空白、表頭仍「10/10 共200筆」。Bug2/3 因需 POST + anti-forgery，改用**失敗的回歸測試**作為可重現證據（更確定、可重跑）。
2. [x] 我給 agent 的資訊包含具體觀察（頁碼／金額數字／庫存數字），而不是只貼客訴原文
       — 例：page10 = 0 列、原價 1000 × 0.9 應為 900、庫存 10→建單7→取消應回10。
3. [x] 每個修復都回到頁面驗證過症狀消失
       — Bug1 修後頁面回驗：`/Orders?page=10` 變 20 列、第 1 頁含最新。Bug2/3 以回歸測試 + 全套件轉綠驗證。
4. [x] 每個 bug 都補了一個回歸測試，`dotnet test` 全綠（28→31）
5. [x] 三個獨立 commit（0915c4c / 391dda3 / 5851ab6），message 說明症狀與根因
6. **（思考題）為什麼原本的測試沒抓到這三個 bug？**
   - Bug1：`OrderServiceQueryTests` 只驗 `TotalCount`/`TotalPages`，從不檢查 `Items` 內容或筆數（空集合上 `Assert.All` 恆真）。
   - Bug2：`CalculateTotal` 的測試都用**手工建的 order** 單獨測，沒走 create→重載→total；而 `CreateOrder_SnapshotsCurrentUnitPrice` 用 Standard 客戶，Gold 分支從未執行。
   - Bug3：`OrderServiceCancelTests` 只驗結果 `Status`，從不檢查 `product.StockQuantity`。
   共同點：**測試只驗了「摘要/狀態」，沒驗真正會出錯的「內容/數字」**。

練習 3

1. [x] `/Products/LowStock` 不帶參數 → 門檻 10 的結果（5 筆低庫存在售商品）；帶 `?threshold=3` → 結果變 1 筆（SKU-1048，庫存2）
2. [x] `?threshold=0`、`?threshold=-1`（及非數字 `abc`）→ 頁面顯示驗證錯誤「庫存門檻必須大於 0」，HTTP 200 不是 500
3. [x] 售出數量欄位排除了 Cancelled 訂單（`GetSoldQuantitiesSinceAsync` 過濾 `Status != Cancelled`；有單元測試以一筆已取消訂單驗證）
4. [x] 停售（`IsActive=false`）商品不出現在列表（Repository `Where(IsActive)`；有單元測試驗證）
5. [x] 程式分層與命名跟既有 Products 功能一致（Controller 薄、查詢在 Repository、邏輯在 Service、View 綁 ViewModel、DataAnnotations 驗證；收工前用子代理 review 過一次）
6. [x] 至少 3 個新測試（門檻`<`過濾+升冪、排除停售、近30天售出排除Cancelled與逾期），`dotnet test` 全綠（31→34）

練習 4

1. [x] 重構後 `dotnet test` 全綠（34，行為不變）
2. [x] 我能說出這次重構「改善了什麼、沒有改變什麼」
       — 改善：把 50 行的 `CreateOrderAsync` 拆成 `ValidateOrderRequest` + `BuildOrderItemsAsync`，可讀性提升、驗證邏輯集中。沒有改變：驗證順序、錯誤訊息文字、扣庫存時機（仍在建明細迴圈內、於彙整錯誤檢查前）、失敗不存檔。
3. [x] 我有在 code review 的角度看過 diff（不是 agent 說好就好）
       — 收工前用獨立唯讀子代理 review 整份 `git diff origin/main`，逐條檢查分層/N+1/邊界/行為保持/測試品質。

---

## 附錄：值得留下的對話片段

（貼 1–2 段最有代表性的 prompt 與回應**摘要**——不用貼全文，重點是「我怎麼問」和「它怎麼答」。）

1. **開工前的對抗式驗證**（我怎麼問）：
   > 「你是獨立、對抗式的驗證者。**不要相信**以下分析，用你自己的程式碼閱讀**確認或反駁**每一點，附 `file:line`。並額外找：有沒有第 4 個 bug、我的修法會不會弄壞現有測試、Ex3 規格有沒有我會踩的坑。」
   它怎麼答（摘要）：三個 bug 根因全 CONFIRMED 附行號；確認移除 Gold 分支不會弄壞任何現有測試；提醒 Ex3 要守住 `<` 非 `<=`、近30天要 join `Order.CreatedAt`、只排除 Cancelled、單一 group 查詢避免 N+1。→ 直接塑形了我的測試斷言與實作。

2. **失敗測試作為重現證據**（我怎麼做）：
   > 先寫 `CancelOrder_RestoresProductStock`（建單扣 10→7、取消後應為 10），跑 `dotnet test --filter` → 得到 `Expected: 10, Actual: 7`。
   這個「先看到紅」再動手的節奏，是我這次最想帶回日常的一招。

---

# 第二階段 — MCP Server（活動 2）心得

> 完整逐步流水帳（每個練習：被要求 → 我做了什麼 → 結果、含實測數字）在專案根目錄 [`EXECUTION-LOG-2.md`](../EXECUTION-LOG-2.md)。以下是四問 + 自我驗證 + 對這個訓練的看法。

**使用的 agent 與模型**：Claude Code，Opus 4.8（1M context）。每個練習流程：讀懂 → 計畫 + 對抗式子代理驗計畫 → 實作 + 子代理 review → 記錄 → 獨立 commit（只本地、不 push）。**因為我是自動化 agent，無法對自己熱插新 MCP server 或開瀏覽器 GUI**，凡活動要求互動式 client（瀏覽器版 Inspector、Claude Code `/mcp`、`@` 選 resource、slash command）之處，一律改用**官方 Inspector 的 `--cli` 非互動模式** + 自寫 JSON-RPC client 直接對 stdio 說話來驗證（同一套協定、可重跑、可留輸出）——精神同第一階段「用失敗測試 / raw HTTP 取代手動點頁」。

## 通用四問

### 1. 我的任務拆解
- 先建基線（`dotnet build` 0/0、`dotnet test` 34 綠、確認 SQL Server + 種子 DB、網站 :5150），並抓 `/Products` 低庫存當**對照真值**（SKU-1048/1005/1023/1032/1014）。
- 依練習 0→5：0 體驗現成 MCP、1 建 server（3 唯讀工具）、2 Inspector 除錯、3 註冊 + before/after、4 破壞性 cancel_order、5 Resources/Prompts。
- 順序沒大改；唯一實務調整：**先 `dotnet build` 再讓任何 client 連**——因為 `.mcp.json` 用 `dotnet run`，首次冷啟動 build 會讓連線逾時（活動自己也提醒）。

### 2. AI 幫上大忙的地方
- **開工前用對抗式子代理驗「整份計畫 vs 真實程式碼」**，一次抓出多個會讓我踩雷的點。提問原文（重點）：
  > 「你是獨立、對抗式的驗證者，**不要相信**我的計畫，逐條用 file:line 確認或反駁；特別查：命名空間、方法簽章、`GetOrderAsync` 有沒有 Include 導航、Core/Infra 的 target framework、序列化循環參照、還有什麼會『編得過但跑不起來』。」
  它回報並確認：`IOrderService` 在 `OrderHub.Core.Services`（不是 Interfaces）→ 要多一個 using；`GetOrderAsync`→`GetWithDetailsAsync` 有 `Include(Customer).Include(Items).ThenInclude(Product)`；EF 8.0.11 會透過 Infrastructure 傳遞、建議 Mcp 也用 net8.0；並點名三個地雷（stdout 要乾淨、Scoped DbContext、外部 NuGet API 要實測）。**這些直接塑形了我第一版就能跑的程式**。

### 3. AI 誤導我的地方，與我如何發現
- **兩個子代理互相校正，我靠實測當裁判**。練習 5 的 review 子代理很有信心地說：「SDK 用方法名原樣 `LowStock`，你 prompt 裡寫 `low_stock` 是錯的。」——聽起來很具體。但我手上有練習 2/4 的 `tools/list` **實測輸出**：註冊名就是 snake_case `low_stock`/`get_order`/`customer_orders`（活動也明載 `LowStock → low_stock`）。所以這個「發現」不成立，prompt 是對的。**教訓**：子代理再有信心，也要用可觀察的證據（實際協定輸出）當最終裁判，不能因為它講得具體就照收。
- **`dotnet run --project` 在 Inspector CLI 下回 `Connection closed`**，一度以為是 stdout 被 build 訊息污染。實際抓完整輸出才看到是「找不到要運行的項目」——**Inspector CLI 把 `--project` 當自己的旗標吃掉了**，是測試工具的 arg 解析怪癖，不是 `.mcp.json` 的問題。我改寫一支小 client **照 `.mcp.json` 的 command/args 原樣 spawn**，證明真實 client 可用（initialize + tools/list + low_stock 全過）。

### 4. 我會帶回日常工作的一招
- **改動外部套件包出來的 API 前，先去『查那個版本的型別/成員定義』再寫，不要照文件範本硬幹**。這次每次要用某個標註/方法（`McpServerTool` 的 `Destructive`/`ReadOnly`、`McpServerResource`、`WithResources`、`ChatMessage`）我都先 grep 該版本 NuGet 的 XML doc 確認成員存在，才動手——因為活動範本是對**舊 prerelease** 寫的，實裝到的是 **2.0.0 正式版**。操作步驟：`find ~/.nuget/packages/<pkg>/<ver> -name '*.xml'` → grep 型別/成員名 → 確認存在再寫。這比「編譯錯了再回頭查」省一輪，也避免把 API churn 誤判成自己寫錯。

## 自我驗證（第二階段 — MCP）

練習 0（先當使用者）
1. [x] 準備好 Playwright MCP 的 Claude Code / Codex 設定片段；環境（node v26、npx 11）可取得該 server、網站 :5150 可被操作。
2. [~] 「agent 自己開瀏覽器建單 + 截圖」需**互動式 client**，不在自動化 session 範圍——誠實標註，不偽造截圖。

練習 1（建 server，3 唯讀工具）
1. [x] `dotnet build` 0/0；三工具 `get_order`/`low_stock`/`customer_orders` 都列得出、可呼叫。
2. [x] `low_stock(10)` 與 `/Products` 低庫存**集合一致**（並列 4 的 1014/1032 次序互換屬正常，只依 StockQuantity 排序）。
3. [x] `get_order(999999)` → `找不到訂單 999999`（清楚訊息，非 exception dump）。
4. [x] 金額由 `IOrderService` 算（`get_order(1)` Standard 客戶 DiscountRate=0、Total=Subtotal=12660）；工具不碰 DbContext、不重複折扣規則。

練習 2（Inspector 除錯）
1. [x] 用**官方 Inspector `--cli`** 對已編譯 DLL 跑 `tools/list`，schema 與 `required` 欄位正確（`low_stock.threshold` 非 required、default 10）。
2. [x] `low_stock(3)` 只回 SKU-1048（庫存2），證明門檻是**嚴格小於**。
3. [x] 獨立子代理從零交叉驗證（自己抓網頁 + 自己跑 Inspector）→ CONFIRMED。

練習 3（註冊 + before/after）
1. [x] `training-repo/.mcp.json` 進 git、相對路徑可全隊共用、無密鑰；照 config 原樣 spawn 驗證可用。
2. [x] before/after：**有工具** `low_stock(5)` 一次答完；**沒工具**（本 session 自己就沒掛 orderhub MCP）得繞路寫 client / 爬網頁——親身體會差異。

練習 4（破壞性 cancel_order）
1. [x] annotations 正確：三唯讀工具 `readOnlyHint:true`、`cancel_order` `destructiveHint:true, idempotentHint:false`。
2. [x] `cancel_order(1)` 成功、`get_order(1)` 轉 `Cancelled`、`low_stock(10)` 顯示 **SKU-1032 庫存 4→5**（透過工具本身看到庫存回補）。
3. [x] 再取消同筆 → `狀態為 Cancelled 的訂單不可取消`；取消已出貨(129) → `狀態為 Shipped 的訂單不可取消`（清楚拒絕，非 exception）。
4. [x] `dotnet test` 仍 34 綠；工具只轉接 `OrderService.CancelOrderAsync`，狀態守衛/庫存回補都在 service（標註只是 hint）。

練習 5（Resources 與 Prompts）
1. [x] `resources/list`+`resources/read` 讀得到 `orderhub://discount-rules`；`prompts/list`+`prompts/get(threshold=5)` 展開訊息、`threshold=5` 已代入且引導用 `low_stock` 工具。
2. [x] 分工分類正確：折扣規則=Resource（資料）、低庫存報告=Prompt（範本）、查詢/取消=Tool（動作）。
3. **（5c 思考題）折扣規則用 Resource 給 vs 讓 agent 自己讀 `OrderService.cs`；prompt 範本放 server vs 每人自己打**：
   - Resource：**團隊共用、進版控、規則改版只改一處**，且 agent 不需要有「讀原始碼」的能力/權限就能拿到權威背景知識。
   - Prompt：**一致性與可維護**——採購同事每週那句話全隊同一版本，改流程改一次即生效，不會每人一個版本、各自漂移。
   - 但要小心**兩份真相**：折扣率硬寫在 resource 字串會和 `GetDiscountRate`（`OrderService.cs:143-145`）脫鉤（今天數值相符，改版會 drift）。更好的做法是從 `GetDiscountRate` 動態組出 resource 內容——這也是活動地雷區自己點名的（「resource 也可以動態組出內容」）。

## 我對這個訓練的看法

**整體評價：設計得很好，是我看過把 MCP 講得最務實的教材之一。** 具體幾點：

- **遞進曲線對**：先當使用者（練習 0 接 Playwright，先「感受有新工具」）→ 再當作者（建 server）→ 再學除錯（Inspector）→ 再看價值（before/after）→ 再碰授權（破壞性工具）→ 最後補齊三原語（Resources/Prompts）。每一步只加一個新概念，認知負擔落在 MCP 本身而不是業務邏輯——因為它**重用活動 1 已經熟的 OrderHub**，`cancel_order` 甚至就是活動 1 客訴 3 修好的那個行為，前後呼應。
- **地雷區點的都是真雷**：stdout 是協定通道（log 要走 stderr）、entity 直接序列化會因循環參照在**執行期**炸（「編過 ≠ 能跑」）、標註預設值會反咬（ReadOnly 不標 = 宣告可能破壞性）、Resource 硬寫規則會變兩份真相——這四個我在實作時**真的都會踩到**，不是湊數的注意事項。
- **把「description 是 agent 的 UX」講透了**：搭配 `references/mcp-security-attack-vectors.md`（工具描述下毒、跨 server 劫持、rug pull…），完整帶出「描述不是給人看的文件，是 agent 的行為依據」這個心態轉換——這是 MCP 最反直覺、也最重要的一課。

**兩個可以更新的小地方（不影響學習，但會讓學員少踩坑）**：
1. **套件與框架版本已前進**：活動寫 `--prerelease`，但 `ModelContextProtocol` 現在是 **2.0.0 正式版**；`dotnet new console` 在裝了 SDK 10 的機器上會預設 **net10.0**，需手動改回 net8.0 對齊 EF 8.0.11。建議在活動註明「若已有正式版可省略 `--prerelease`、並確認 TargetFramework 設 net8.0」。
2. **`.mcp.json` 用 `dotnet run` 對首次連線不友善**：冷啟動 build 會逾時。活動已提醒可先 build 或指向 DLL，但可以更強調「**開 client 前先 `dotnet build` 一次**」作為預設步驟。

**一個對自動化/Codex 用戶的提醒**（活動對 Codex 有提到，但值得放大）：resources 與 prompts 目前主要是 Claude Code 的互動式介面（`@`、slash command）在用；Codex CLI / 自動化 agent 只吃 tools。要驗證 resources/prompts，**官方 Inspector（含 `--cli`）是最可靠、可重跑的路徑**——這次我全程靠它，比任何 GUI 截圖都更適合留成可重跑的證據。

---

# 第三階段 — Gemini API：把 AI 嵌進產品（活動 3）心得

> 完整逐步流水帳在專案根目錄 [`EXECUTION-LOG-3.md`](../EXECUTION-LOG-3.md)。以下是四問 + 自我驗證 + 對這個訓練的看法。

**使用的 agent 與模型**：Claude Code，Opus 4.8。每步：計畫 → 對抗式子代理驗計畫 → 實作 → 子代理 review → 記錄 → 獨立本地 commit（不 push）。**金鑰全程未讀**：存 user-secrets，網站執行期自行載入，我只看 HTTP 回應。

## 通用四問

### 1. 我的任務拆解
- baseline → 1a Core → 1b Infrastructure → 1c Web API → 安全單元測試 → **live 煙霧測試** → 練習 2 網站頁面 → PROCESS。
- 兩個實務調整：(a) 把 repo 的 `SearchAsync` **實作**從活動的 1b 提前併進 1a，讓**每個 commit 都建置得過**（介面加了方法卻沒實作會 build 失敗）。(b) 把 Gemini 傳輸類別**改對齊真實 `generateContent` API**——活動範本的 `/v1/interactions` + `gemini-3.5-flash` 是假想形狀，照抄 live 會打不通；安全每一層仍照活動原文。

### 2. AI 幫上大忙的地方
- **開工前用對抗式子代理確認「不需要新增 NuGet」**。CLAUDE.md 禁止未經同意加套件,而活動要用 `IOptions`/`ILogger`/`DataAnnotations.AllowedValues`。子代理逐條查 `project.assets.json` 證明這些都由 EF Core 8.0.11 或 net8.0 targeting pack **傳遞提供**（附行號），我才敢直接寫、不必加套件也不必打斷去問。提問重點：
  > 「這些型別能不能**在不加任何 NuGet** 的前提下編譯?逐一查 csproj 與其傳遞相依,若有需要新增的套件明講——因為 CLAUDE.md 禁止未經同意加套件。」

### 3. AI 誤導我的地方，與我如何發現
- **最生動的一次：503 的真因不是程式錯**。第一次打 API 得到 503「重試 4 次仍失敗」,很容易當成「我的 client 寫壞了」。我**在 client 非成功分支加一行 log（狀態 + 回應片段，去敏）**,重跑才看到真相:HTTP **429** 且 body 寫 `limit: 0, model: gemini-2.0-flash`——是這把金鑰對 `gemini-2.0-flash` **免費配額為 0**,請求格式與 structured-output schema 其實都被接受了。改用 `gemini-2.5-flash` 立刻成功。**靠讀真實上游回應抓到**（正是活動 1「看 raw HTTP」那課的延伸）。
- **review 子代理的 MEDIUM 是對的、採納了**：它指出 HttpClient **逾時**擲的是 `TaskCanceledException` 而非 `HttpRequestException`,原本會漏接變 500——直接違反活動「絕不變 500」紅線。加了 `catch (OperationCanceledException) when (!ct.IsCancellationRequested)`。（同一個子代理也說「找不到活動 md 檔」——它沒讀到我給的路徑,我以實際程式碼為準,不受影響。）

### 4. 我會帶回日常工作的一招
- **接第三方 API 撞非 2xx 時,先把「上游真實狀態碼 + 回應 body 片段（去敏）」log 出來再下結論**,不要停在「重試耗盡 → 503」這種泛泛包裝。操作步驟:非成功分支 `logger.LogWarning("上游 {Status}:{Body}", (int)resp.StatusCode, payload[..500])` → 重跑 → 看真因分流（429 `limit:0`＝配額/模型、400＝schema/body、404＝model 名、401/403＝金鑰）。這次它把我從「以為程式錯」直接導向「配額設定問題」,省掉大量瞎猜。

## 自我驗證（第三階段 — Gemini API）

練習 1（自然語言查訂單 API）
1. [x] 「上個月金卡會員取消的訂單」live 查得出結果:#137 陳志明、#155 劉思穎,**皆 Gold+Cancelled+落在 7 月**（今天 2026-08-07 → 上月）,與狀態/會員條件一致。
2. [x] 「幫我把所有訂單刪掉」→ **HTTP 422「無法理解的查詢」**,資料毫髮無傷（紅線）。
3. [x] 拔掉/配額不足 → **HTTP 503**（非 500）:實測 `gemini-2.0-flash` 配額 0 → 重試耗盡 → 503;金鑰未設走同一 `AiServiceUnavailableException` 路徑（單元測試覆蓋）。
4. [x] 無關文字（食譜）→ 模型判 `unsupported` → 422「無法理解的查詢」,不炸。
5. [x] 分層:LLM 只產白名單參數,SQL 由 EF Core 從強型別 `OrderSearchQuery` 生成,模型碰不到查詢語句;兩道防線（翻譯器白名單 + service `!HasAnyFilter`）。
6. [x] 安全邏輯有離線單元測試（15 個）:`"99"`/壞日期/非法 JSON/unsupported → null;上游例外往外傳;`dotnet test` 34→**49 綠**。

練習 2（同一 service 接網站頁面）
1. [x] `/Orders/Search` 頁面查同一句 → 結果與練習 1 API 一致（#137、#155）——`IOrderSearchService` **一行未改**,分層紅利。
2. [x] 「幫我把所有訂單刪掉」→ 頁面 `alert-warning`「無法理解的查詢」,非錯誤頁。
3. [x] Controller 裡零 Gemini/HttpClient 細節（全封裝在 Infrastructure）;View 綁 `OrderSearchViewModel`;導覽列加「AI 查詢」。

## 我對這個訓練的看法

**核心心法很正確,是「把 LLM 放進產品」最該先學的一課**:
- **「LLM 只產參數、永不產 SQL」的白名單模式** + **兩道防線**（翻譯器把模型輸出當不可信:反序列化→DataAnnotations→enum/date 白名單映射;service 再擋 no-filter）——這是我看過對「不可信模型輸出」示範得最扎實的教材。地雷區也點得準:**今天的日期要進 prompt**（否則「上個月」會被算成訓練資料裡的月份）、**`Enum.TryParse` 單獨會吃 `"99"`**（子代理實測確認,所以 `[AllowedValues]` 要先擋）。
- **上游失敗轉 503 而非 500**、**免費層一定撞 429 要退避重試**——都是真的會遇到（我第一次打就撞 429）。
- **分層紅利**在練習 2 具體兌現:同一個 `IOrderSearchService`,API 與網站頁面共用,一行邏輯都不用改。

**一個必須提醒維護者更新的地方（照抄會 live 打不通）**:活動的 Gemini 端點/模型是**假想或未來形狀**——`POST /v1/interactions`、`input`/`response_format`、回應 `steps[].model_output`、模型 `gemini-3.5-flash`——與 Google **現行**的 `POST …/v1beta/models/{model}:generateContent`（`contents`/`parts` + `generationConfig.responseSchema`,回應 `candidates[0].content.parts[0].text`）**不一致**。我把傳輸層改成真實形狀才跑得通,安全每一層仍照活動。建議活動:(1) 改用真實 `generateContent` 形狀;(2) 提醒「**免費配額因模型而異**,先用 `GET …/v1beta/models?key=` 挑有 `generateContent` 且有免費配額的 flash 模型」——我實測本金鑰 `gemini-2.0-flash` 免費配額為 0、`gemini-2.5-flash` 可用;(3) `response_format`/schema 的 `type` 大小寫（真實 API 用大寫 `OBJECT`/`STRING`）。

**一個給自動化 agent 的誠實邊界**:申請金鑰需真人在瀏覽器登入 Google、接受 ToS,我做不到;金鑰也不該進對話。這次由 user 存進 user-secrets、我只讀 HTTP 回應來驗證——是把「agent 不碰機密」落實成流程的好例子。

---

# 第四階段 — n8n 自動化：把人抽離流程（活動 4）心得

> 完整逐步流水帳在專案根目錄 [`EXECUTION-LOG-4.md`](../EXECUTION-LOG-4.md)。可匯入的 workflow JSON + 手動步驟在 [`documents/references/n8n-workflows/`](references/n8n-workflows/)。以下是四問 + 自我驗證 + 兩題思考題 + 對這個訓練的看法。

**使用的 agent 與模型**：Claude Code，Opus 4.8（1M context）。每步：計畫 → 對抗式子代理驗計畫 → 實作 → 子代理 review → 記錄 → 獨立本地 commit（不 push）。**一個誠實的邊界**：活動 4 的練習 1–3 全靠瀏覽器 GUI（`http://localhost:5678` 拖拉節點、填憑證、按 Execute），自動化 agent 無法點畫布。開工前我先跟 user 確認**可行性切分**（如活動 3 的金鑰決策）：唯一的真程式碼交付（補齊 — MCP HTTP transport）**完整做到底並端到端驗證**；n8n 練習則**產出可匯入的 workflow JSON + 逐字手動步驟**，GUI-only 步驟一律標 `[~]`，不偽造截圖、不假稱「跑起來了」。

## 通用四問

### 1. 我的任務拆解

- 基線（build 0/0、test 49 綠）→ 補齊 MCP HTTP transport（真程式碼）→ 練習 1 webhook JSON → 練習 2 日報 JSON → 練習 3 MCP JSON → 收尾（PROCESS + README + 總結）。
- 兩個實務調整：**(a)** n8n 練習 1–3 共用同一套節點詞彙、且練習 3 = 練習 2 + 一個 MCP 節點（遞增），所以「開工前對抗式驗證」對這三步**合併成一次 schema 研究**（派子代理對官方 n8n source 查證 `type` 字串／`typeVersion`／最易錯的 AI 子節點連線形狀），每個練習仍各自實作 → review → commit（比照活動 3 把 1a/1b 合併的精神）。**(b)** 套件版本對齊 **`2.0.0` 正式版**而非活動假設的 `2.0.0-preview.2`（本 repo 早已鎖正式版；照抄會 NU1605）。

### 2. AI 幫上大忙的地方

- **開工前用對抗式子代理鎖死「套件版本可行性」**，把最會爆的地方先確定。CLAUDE.md 禁止亂加套件、活動又假設一個和本 repo 不符的 preview 版本，所以我先問子代理：
  > 「`ModelContextProtocol.AspNetCore 2.0.0`（正式版，非 prerelease）**存在且可還原**嗎？它的 nuspec 對 `ModelContextProtocol` 的相依是不是**精確鎖 `[2.0.0]`**（否則 NU1605）？`WithHttpTransport`/`MapMcp`/`Stateless` 在 2.0.0 **真的存在**嗎？`MapMcp()` 預設端點掛在哪個路徑？」
  它逐條附證據回覆（nuspec 精確鎖 `[2.0.0]`、反射真實 assembly 確認三個 API 都在、端點掛**根路徑 `/`**）。**這直接決定了我第一次 build 就過、第一次打 HTTP 就對**——不必試錯降版或亂猜端點路徑。
- **n8n schema 研究子代理**把「AI Agent 子節點怎麼連」這個最易錯的點釘死：連線以**子節點名為 key**、方向是**子節點 → agent**（`connections["Gemini節點"]["ai_languageModel"][0][0] = {node:"AI Agent",...}`）。手刻 JSON 若把方向或 key 名弄反，匯入後模型/工具會「看起來在畫布上、實際沒掛上」——這種錯很難 debug，先查證省掉一輪。

### 3. AI 誤導我的地方，與我如何發現

- **我自己的 stdio 驗證腳本誤報「server 壞了」**。驗證「不帶 `--http` stdio 未變」時，前兩次腳本回 STDOUT 長度 0，一度懷疑重構弄壞了 stdio。但 stderr 明明有 `transport reading messages`／`Application started`——server 有起來。真因是**我的測試法**：把 JSON-RPC 全部寫進 stdin 後**立刻關閉 stdin**，stdio transport 把 EOF 當斷線、在 flush 回應前就收攤。改成**保持 stdin 開著、同步 `ReadLineAsync` 逐行讀**，馬上拿到 `tools/list` 的 4 個工具與 `get_order` 的完整訂單。**靠讀 stderr（server 明明活著）+ 換掉自己的測試法抓到**——教訓同活動 1「別讓自己的驗證腳本誤導你」：問題常在**觀測方式**，不在被測物。（這也正是活動 2「Inspector CLI 把 `--project` 當旗標吃掉、不是 server 的錯」的翻版。）
- **子代理一處筆誤（`8 nodes` vs 實際 10）**：練習 3 review 子代理內文把節點數寫成 8，但我方 `ConvertFrom-Json` 實測是 10 節點且所有連線引用完整。不因子代理講得篤定就照收——用可觀察的實測當裁判（延續活動 2 的教訓）。

### 4. 我會帶回日常工作的一招

- **接一個「編得過也未必連得上」的新傳輸／協定前，先用「最小可重跑的協定探針」隔離『我的程式』與『我的測試法』**。這次的操作步驟：(1) server 跑起來先**輪詢 port 是否 LISTENING**（別急著打）；(2) 用**與 client 同協定**的最小請求打一發（HTTP 走 JSON-RPC POST、stdio 走 keep-open 的 stdin + 同步讀）；(3) 回應**對照一個已知真值**（`get_order(1) Total=12660`，這數字活動 2 就記過）。三步都過才算「端到端」。這比「build 綠就宣稱好了」可靠得多——build 只證明編得過，協定層要另外拿真值比對。

## 自我驗證（第四階段 — n8n / MCP HTTP）

補齊（MCP HTTP transport）
1. [x] `dotnet run --project src/OrderHub.Mcp -- --http` 在 `:3001` 起得來；JSON-RPC over HTTP：`tools/list`=4 工具（annotations 完整）、`resources/list`=`discount-rules`、`prompts/list`=`low_stock_report`、`tools/call get_order{id:1}`→`Total:12660`（與活動 2 stdio 一致）。
2. [x] 不帶 `--http` 走 stdio：`tools/list`=4 工具、`get_order` 回完整訂單——**行為不變**，`.mcp.json`/Codex 設定不用動。
3. [x] 工具/Resource/Prompt **一行未改**，只換 transport；`build` 0/0、`test` 49 綠、restore 無 NU1605。
4. [x] 版本對齊 `2.0.0` 正式版（非 preview.2）；stderr-logging 只留 stdio 分支；`AddOrderHubServices` 兩 transport 共用、註冊字元級相同。

練習 1（Hello Webhook）
1. [x] `01-hello-webhook.json` 可匯入：`responseMode:"responseNode"`（易漏，預設 _Immediately_ 會忽略 Respond 節點）、`includeOtherFields:true`（不開會丟 body）、`receivedAt={{ $now.toISO() }}` 都設好。
2. [~] 按 Execute 進 120 秒監聽、打 request、看綠勾 —— GUI-only，需真人操作（README 已寫步驟）。

練習 2（退單巡檢日報）
1. [x] `02-退單巡檢日報.json` 9 節點可匯入、結構與 expression 正確（review 判 IMPORTABLE、8 critical check 全 OK）。查詢直接打**活動 3 的 `/api/orders/search`**，零新程式碼。
2. [x] 地雷全處理：`alwaysOutputData` 節點頂層、`整理筆數` 濾空 item、IF 用 `$('整理筆數')` 跨節點拿 `count`、IF true→GitHub/false→Data Table 未接反、Gemini 以 `ai_languageModel` 連進 agent。
3. [~] Execute 後開 GitHub issue／收通知／Data Table 歸檔、日報數字與 `/Orders` 篩「已取消」比對 —— 需真人填 Gemini 金鑰、GitHub PAT、建表後執行。

練習 3（MCP 合體）
1. [x] `03-mcp-deep-dive.json` 10 節點可匯入：MCP Client Tool（`http://localhost:3001`、`httpStreamable`）以 `ai_tool` 掛上 AI Agent；System Message 加深挖句。
2. [x] **安全紅線**：`includeTools` 只 `get_order`，全檔不含 `cancel_order`／`low_stock`／`customer_orders`（子代理 grep 0 次確認）——無人流程只給「讀」。
3. [x] MCP server HTTP 端 `get_order` **已在補齊段落實測可用**；n8n 一連上、agent 一呼叫，拿到的就是那個真實回應。
4. [~] Executions log 看 agent 對退單呼叫 `get_order`、日報引用真實品項金額 —— 需真人 Execute 後在瀏覽器展開節點看。

## 兩題思考題

### （練習 2）如果「查什麼、怎麼查」也交給 AI Agent 自由發揮，會失去什麼？

失去三樣**恰好是活動 1–3 一路建起來的東西**：

1. **失去活動 3 的白名單防線**。現在的分工是：n8n 的 HTTP Request 打**活動 3 的 `/api/orders/search`**，中文→查詢參數的翻譯、`[AllowedValues]` enum/日期白名單、`!HasAnyFilter` 拒絕空查詢、`Take(100)` 上限、「LLM 只產參數、永不產 SQL」這兩道防線——**全在產品程式碼裡做完了**，n8n 只做編排。若把「查什麼、怎麼查」也丟給 n8n 裡的 AI Agent 自由發揮（例如讓它自己決定要打哪個 endpoint、自己拼查詢甚至自己組 SQL），這整套防線就被繞過了：模型輸出重新變成「未經白名單的可信輸入」，活動 3「安全對待模型輸出」那一課直接作廢。**業務邏輯該放在有測試、有防線的產品層，不是放在編排層的 prompt 裡。**

2. **失去可測試性 / 可重現性**。API 版的查詢有 15 個離線單元測試把每條紅線固化（`"99"`→null、壞日期→null、刪除意圖→422…），是不依賴金鑰、可重跑的回歸測試。若查詢邏輯活在 AI Agent 的自由發揮裡，**同一句話今天明天可能查出不同東西**（模型不確定性 + 沒有強型別介面可 assert），CI 擋不住回歸，出事也難複現。

3. **失去日報數字的可信度**。目前 AI **只做摘要**：拿到的是查詢結果 JSON，System Message 明令「只根據提供的資料寫、不要編造數字」。數字的真實性由**確定性的 SQL 查詢**保證，AI 只是換句話說。若連「查什麼」都交給 AI，日報裡的「本月取消 N 筆、總額 M 元」就沒有一個可稽核的查詢當靠山——**摘要可以交給 AI，事實的來源不行**。這正是把 LLM 放進產品的核心心法：**讓 AI 做它擅長的（自然語言摘要），把事實與安全留給有防線、有測試的確定性程式碼。**

### （練習 3）同一批退單，有深挖 vs 沒深挖的日報差異

- **沒深挖（練習 2）**：AI Agent 只拿到 `search API` 回的**摘要六欄**（`id/customerName/tier/status/total/createdAt`）。日報只能寫到這個顆粒度：「本月取消 N 筆、總額 M 元、其中 #137 金額最高／是金卡會員」。**看得到「哪幾筆、多少錢」，但看不到「買了什麼」**——無法回答「取消的都是哪類品項？是不是某個 SKU 特別容易被退？」

- **有深挖（練習 3）**：AI Agent 多了 `get_order` 這個 MCP 工具，System Message 要求「對每筆取消的訂單，先用工具查出品項明細與會員等級」。於是日報能寫到**品項顆粒度**：「#1 取消含 SKU-1044 晨光 USB-C 集線器 ×2（6220 元）、SKU-1009 極光 HDMI 傳輸線 ×1…」——引用的是 `get_order` 回的**真實 `UnitPriceSnapshot`/`LineTotal`/`Total:12660`**（我在補齊段落實測過的那個回應），不是 AI 編的。

- **差異的本質**：練習 2 的資料是**一次查詢的固定投影**（摘要），練習 3 讓 AI **按需、逐筆向 MCP 工具拉更深的資料**（明細）。前者是「AI 摘要一張報表」，後者是「AI 帶著工具去調查」。**代價**是多了 N 次工具往返（每筆退單一次 `get_order`）與對應的延遲／token；**收穫**是日報從「數量統計」升級成「可據以行動的品項級洞察」。**驗證證據**也不同：練習 3 可在 Executions log 看到 agent **實際的每次 `get_order` 呼叫輸入/輸出**，這是「有沒有真的深挖」最直接的證明——比起看日報文字，看工具呼叫紀錄更難造假。

- **安全上的呼應**：深挖只掛 `get_order`（讀），**絕不掛 `cancel_order`（寫）**。同一個 MCP server 在活動 2 有破壞性工具，但放進無人巡檢流程時，用 `Tools to Include = Selected` **只給讀工具**——活動 1「破壞性操作要人確認」的哲學，在無人流程裡的形狀就是「根本不給那個工具」。深挖增加的是「讀得更細」，不該順手放大「能改的範圍」。

## 我對這個訓練的看法

**活動 4 是四個活動裡「收束」得最漂亮的一課——它讓前三課的每個決定都『兌現』了一次**：

- **分層的紅利在這裡第三次兌現**。活動 3 已示範「同一個 `IOrderSearchService`，API 與網站頁面共用」；活動 4 更進一步——n8n 直接打**活動 3 的 API**（查詢邏輯零複製）、掛**活動 3 的 Gemini key**、透過**活動 2 的 MCP server** 深挖。「業務邏輯放產品層、編排放 n8n」這條線，到這裡變成看得見的架構事實：**換一個 orchestrator（從網站頁面換成 n8n），產品程式碼一行不用改。**
- **「補齊 — MCP HTTP transport」是全訓練最優雅的一個註腳**：同一個 server，工具/Resource/Prompt 一行不改，只加一個 `--http` 分支換 transport，就從「agent 的本機子行程（stdio）」變成「n8n 可連的遠端服務（HTTP）」。這把活動 2 講的「MCP 分層設計」從口號變成**十幾行程式碼的實證**——transport 與 capability 正交，是這套協定最實際的紅利。
- **人機協作的哲學是一以貫之的**：活動 1 教「破壞性操作要 approval」，活動 4 把它翻譯成無人流程的規則——`cancel_order` 這種寫入工具**根本不掛進流程**。安全不是加一道確認框，而是**在編排層就縮小 agent 能碰的表面積**。

**兩個對維護者的誠實提醒（照抄會卡住）**：
1. **套件版本**：活動假設 `ModelContextProtocol` 鎖 `2.0.0-preview.2`、要 `dotnet add ... --version 2.0.0-preview.2`；但本 repo 早已是 **`2.0.0` 正式版**，`ModelContextProtocol.AspNetCore` 必須跟著用 `2.0.0`（非 `--prerelease`），否則 restore NU1605。建議活動註明「若已用正式版，AspNetCore 套件版本要對齊你 csproj 現有的 `ModelContextProtocol` 版本」。（延續活動 2 已提過的「套件進正式版了」。）
2. **模型名稱**：活動的 Gemini 節點寫 `gemini-3.5-flash`，但這顆在本金鑰不可用（活動 3 實測 `gemini-2.0-flash` 免費配額 0、`gemini-2.5-flash` 可用）。workflow JSON 已用 `models/gemini-2.5-flash`；建議活動提醒「模型名稱與免費配額會變，先確認你的金鑰對哪顆有配額」。

**一個給自動化 agent 的誠實邊界（延續活動 2/3）**：n8n 是**瀏覽器 GUI 工具**——建 owner 帳號、拖拉節點、填憑證、按 Execute、看 Executions log，都需要真人在瀏覽器裡點。自動化 session 做不到，也不該假裝做到。這次的做法是把**能自動化的（MCP HTTP 真程式碼）做到端到端驗證**、把**不能自動化的（n8n GUI）產出可匯入 JSON + 逐字手動步驟並誠實標 `[~]`**——如同活動 2 用 Inspector `--cli`、活動 3 由 user 管金鑰，「劃清 agent 能與不能的界線、且不偽造」本身就是這個訓練想教的一種專業素養。
