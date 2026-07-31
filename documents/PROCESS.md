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
