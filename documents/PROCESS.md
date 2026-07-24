# PROCESS.md — 我的練習心得

> 一個原則：**寫「具體發生的事」，不寫感想文。**
> 貼上當時真實的 prompt、真實的數字、真實的錯誤訊息——三個月後的你（和你的同事）才用得上。

#### 使用的 agent 與模型：

Claude Code，模型 Opus 4.8（1M context）。用兩個唯讀子代理做交叉驗證（開工前驗分析、收工前 review 全 diff）。

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
