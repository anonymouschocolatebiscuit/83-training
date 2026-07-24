# OrderHub 訓練練習 — 執行紀錄 (Execution Log)

> 格式：每一步記錄 **① 被要求做什麼 (Asked) → ② 我做了什麼 (Done) → ③ 結果 (Result)**。
> 由 Claude Code (Opus 4.8) 於 session 中執行。原則：先重現、再修、每步驗證、絕不改測試遷就程式。

---

## 環境基線 (Baseline)

- .NET SDK: 8.0.408 / 9.0.x / 10.0.202（專案 target net8.0）
- `dotnet ef` 10.0.9 已安裝
- SQL Server：`MSSQLSERVER`（預設實例，localhost）狀態 = Running
- 連線字串：`Server=localhost;Database=OrderHubTraining;Trusted_Connection=True;...`
- 網站 URL（http profile）：http://localhost:5150
- **基線建置**：`dotnet build` → 0 warnings / 0 errors
- **基線測試**：`dotnet test` → 28 passed / 0 failed
- Git：remote = 原始 repo（anonymouschocolatebiscuit/83-training），branch = main。**只做本地 commit，不 push。**

---

## 全域摘要：這個 repo 要我做什麼

這是一套「用 AI agent 完成日常開發」的實戰訓練。專案 `training-repo/` 是一個
ASP.NET Core MVC + EF Core + SQL Server 的內部訂單管理系統 (OrderHub)。
四個練習（`documents/PROCESS.md` 有自我驗證清單）：

| 練習 | 主題 | 交付物 |
| --- | --- | --- |
| 1 | Agent 初始設置 | CLAUDE.md、.claude/settings.json、hooks、subagents、fix-bug skill；commit |
| 2 | 修 3 個 bug | 每個 bug：重現→定位→修→回歸測試→獨立 commit（症狀/根因/修法）|
| 3 | 新功能：低庫存警示頁 | `GET /Products/LowStock?threshold=10`，跨 6 層，≥3 測試，1 commit |
| 4 | 小型重構 | 抽出 `CreateOrderAsync` 驗證邏輯，行為不變，測試全綠，1 commit |

### 三個 bug 的根因（我的分析，待 subagent 獨立驗證）

1. **訂單列表分頁 (客訴 1)** — `OrderRepository.GetPagedAsync` 用 `.Skip(page * pageSize)`，
   但 page 是 1-based → 第 1 頁跳過前 20 筆（最新的訂單），最後一頁 overshoot 變空白。
   修法：`.Skip((page - 1) * pageSize)`。
2. **Gold 會員金額 (客訴 2)** — `CreateOrderAsync` 對 Gold 先把 `UnitPriceSnapshot` 打了 9 折，
   `CalculateTotal` 又依 tier 打一次 9 折 → Gold 變 0.81（雙重折扣）；Silver 快照沒打折所以正常。
   修法：移除 create 時的 Gold 折扣，快照存原價，折扣集中在 `CalculateTotal`（符合 CLAUDE.md 慣例）。
3. **取消訂單庫存 (客訴 3)** — `CancelOrderAsync` 先 `order.Status = Cancelled` 才判斷
   `if (Status == Pending || Confirmed)` → 條件恆假，還原庫存的區塊是死碼，退單後庫存從不加回。
   修法：在設為 Cancelled 之前還原庫存（頂部 guard 已保證只有 Pending/Confirmed 會走到這）。

### 為什麼原本 28 個測試沒抓到

- Bug1：`GetOrders_*` 只驗 `TotalCount`/`TotalPages`，從不檢查 `Items` 的內容或筆數（空集合上 `Assert.All` 恆真）。
- Bug2：`CalculateTotal_*` 用手工建的 order 單獨測，沒走 create→total 路徑；`CreateOrder_SnapshotsCurrentUnitPrice` 用 Standard 客戶，Gold 分支從未執行。
- Bug3：`CancelOrder_*` 只驗結果 Status，從不檢查 `product.StockQuantity`。

---

## 執行步驟紀錄

### 練習 2 — Bug 1：訂單列表分頁　✅（commit 0915c4c）

**① Asked**：客訴 1「新訂單在第一頁找不到、最後一頁常空白」。重現→定位→修→回歸測試→獨立 commit。

**② Done**：
- 頁面重現（app on :5150，種子 200 筆）：`GET /Orders?page=1` = 20 列但非最新；`?page=10`（最後頁）= **0 列**，顯示「沒有符合條件的訂單」而表頭仍寫「10/10，共 200 筆」。
- 加回歸測試 `GetOrders_FirstPage_IncludesNewest_AndLastPageNotEmpty`（25 筆、驗第 1 頁滿頁+含最新、最後一頁非空）。**先跑 → FAIL**（Expected 20, Actual 5）。
- 修 `OrderRepository.GetPagedAsync`：`Skip(page * pageSize)` → `Skip((page - 1) * pageSize)`。

**③ Result**：全套件 29 passed（28+1）。以 commit 0915c4c 提交（含測試）。頁面層最終重啟再驗（見文末）。

### 練習 2 — Bug 2：Gold 會員重複折扣　✅（commit 391dda3）

**① Asked**：客訴 2「Gold 會員應付總額比手算少一截，Silver 正常」。

**② Done**：
- 定位：`CreateOrderAsync` 對 Gold 先把快照打 9 折，`CalculateTotal` 又依 tier 打一次 → 0.81 倍。
  雙重折扣只在「重載含 Customer」時顯現（明細頁走 `GetWithDetailsAsync`），故測試需重載訂單。
- 加回歸測試 `CreateOrder_GoldCustomer_AppliesDiscountOnce`（建 Gold 單→驗快照=原價 1000、重載後總額=900）。**先跑 → FAIL**（快照 Expected 1000, Actual 900）。
- 修 `OrderService.CreateOrderAsync`：移除 Gold 預先折扣，快照存 `product.UnitPrice`。

**③ Result**：全套件 30 passed（+1）。以 commit 391dda3 提交（含測試）。

### 練習 2 — Bug 3：取消訂單庫存未還原　✅（commit 5851ab6）

**① Asked**：客訴 3「庫存跟盤點對不上，每次退單後更少」。

**② Done**：
- 定位：`CancelOrderAsync` 先 `order.Status = Cancelled` 才判斷 `if(Status==Pending||Confirmed)`，條件恆假 → 還原庫存區塊是死碼。
- 加回歸測試 `CancelOrder_RestoresProductStock`（建單扣 10→7、取消後驗還原為 10）。**先跑 → FAIL**（Expected 10, Actual 7）。
- 修 `OrderService.CancelOrderAsync`：在設為 Cancelled 前先還原庫存，移除死碼判斷。

**③ Result**：全套件 31 passed（+1）。以 commit 5851ab6 提交（含測試）。**練習 2 完成：3 bug、3 獨立 commit、皆先失敗測試再修。**

---

### 練習 3 — 新功能：低庫存警示頁（計畫）

規格：`GET /Products/LowStock?threshold=10`；列 `Stock < threshold` 且 `IsActive`、庫存升冪；
欄位 Sku/名稱/現有庫存/近30天售出（排除 Cancelled）；庫存 `<5` 標 `table-danger`；
未帶 threshold 預設 10、`<=0` 顯示表單驗證錯誤（非 500）；導覽列加「低庫存」；≥3 service 測試。

計畫（沿用既有分層）：Core `LowStockItem` record；Repo 加
`IProductRepository.GetActiveBelowStockAsync` 與 `IOrderRepository.GetSoldQuantitiesSinceAsync`（單一 group 查詢避免 N+1）；
`ProductService.GetLowStockAsync` 注入 IOrderRepository 合併；`LowStockViewModel`（`int? Threshold`+`[Range(1,..)]`）；
Controller 薄轉接 + ModelState 驗證；`LowStock.cshtml`；`_Layout` 導覽列；3 個 service 測試。

**③ Result（✅ commit ed5b345）**：全套件 34 passed（+3）。頁面實測（app on :5150，種子資料）：
- 預設 `/Products/LowStock` → 5 筆低庫存在售商品（SKU-1005/1014/1023/1032/1048），依庫存升冪、含近30天售出、庫存<5 標紅。
- `?threshold=3` → 結果變為 1 筆（SKU-1048，庫存2）。
- `?threshold=0`、`?threshold=-1`、`?threshold=abc` → HTTP 200（非 500），顯示「庫存門檻必須大於 0」驗證錯誤、表格空。
- 導覽列「低庫存」連結存在。
以 commit ed5b345 提交（13 檔）。

### 練習 1 — Agent 初始設置　✅（commit 7ebeaef）

**① Asked**：依 `agent-configuration.md` 建立 Claude Code 專案設定檔並 commit：CLAUDE.md、
`.claude/settings.json`（權限）、hooks、subagents、fix-bug skill。

**② Done**：在 `training-repo/` 下建立 7 個檔案（內容依指南範例，已逐條對照程式碼確認正確）：
- `CLAUDE.md`（專案記憶：技術棧、分層慣例、常用指令、危險檔案、Don'ts）
- `.claude/settings.json`（permissions：deny `rm -rf`/`git push --force`/`git reset --hard`/讀機密檔/改 Migrations；ask `dotnet ef database drop`/`git push`；allow build/test/run/git 日常。hooks：PreToolUse 擋 DROP/TRUNCATE、PostToolUse 記錄編輯）
- `.claude/hooks/block-destructive-sql.ps1`、`log-edits.ps1`
- `.claude/agents/code-reviewer.md`、`test-runner.md`
- `.claude/skills/fix-bug/SKILL.md`

**③ Result**：`settings.json` 通過 JSON 驗證；7 檔以一個 commit（7ebeaef）提交。
（註：這些 hook 只有在以 `training-repo/` 為專案根開啟 Claude Code 時才會實際觸發；
本 session 的專案根是上層 repo 根目錄，故此處僅交付設定檔本身。）

---

### 驗證子代理結論（在動 bug 前先跑）

獨立子代理（唯讀）逐條比對程式碼後：3 個 bug 根因、4 個練習定義、以及「為何測試沒抓到」
**全部 CONFIRMED**（附行號證據），並確認三個修法都不會弄壞現有測試（含移除 Gold 分支）。
重點提醒：回歸測試必須補上原測試缺的斷言（第 1 頁內容 / Gold 建單總額 / 取消後庫存還原）；
Ex3 注意 `<` 非 `<=`、近 30 天要 join `Order.CreatedAt`、只排除 Cancelled、`IsActive` 過濾、
單一 group 查詢避免 N+1、`threshold<=0` 走 ModelState 不可 500。

---

### 練習 4 — 小型重構 CreateOrderAsync　✅（commit aeed2e3）

**① Asked**：`CreateOrderAsync` 驗證邏輯越長越大，請提案並執行小型重構，行為完全不變、測試全綠、獨立 commit。

**② Done**：抽出兩個私有方法（保持原有順序與訊息，不動搬到 mutation 前後，避免改變行為）：
- `ValidateOrderRequest(customer, lines)`：請求層級驗證（客戶存在/明細非空/數量>0/不重複），回 null 或錯誤訊息。
- `BuildOrderItemsAsync(order, lines)`：逐項驗證商品→扣庫存→建立明細快照，失敗彙整錯誤。
`CreateOrderAsync` 本體縮短為：取客戶 → 驗請求 → 建 Order → 建明細 → 存檔。

**③ Result**：`dotnet build` 0 error；`dotnet test` 34 全綠（行為不變）。以 commit aeed2e3 提交。

---

## 最終總結

| 練習 | commit | 內容 | 驗證 |
| --- | --- | --- | --- |
| 1 | 7ebeaef | Claude Code 設定（CLAUDE.md、權限、hooks、subagents、fix-bug skill）| JSON 驗證通過 |
| 2-1 | 0915c4c | Bug 分頁 off-by-one | 失敗測試→修→29 綠 + 頁面重現/回驗 |
| 2-2 | 391dda3 | Bug Gold 重複折扣 | 失敗測試→修→30 綠 |
| 2-3 | 5851ab6 | Bug 取消未還原庫存 | 失敗測試→修→31 綠 |
| 3 | ed5b345 | 低庫存頁（6 層 + 3 測試）| 34 綠 + 頁面實測（門檻/驗證/排序/售出）|
| 4 | aeed2e3 | 重構 CreateOrderAsync 驗證 | 34 綠（行為不變）|

- 測試：基線 28 →最終 **34**（+6：bug×3、低庫存×3）全綠。
- 建置：0 warning / 0 error。
- 兩次獨立子代理（唯讀）：開工前驗證分析、收工前 review 全 diff。
- **只做本地 commit，未 push**（remote 為原始 repo，無寫入權；且 push 屬對外動作需經同意）。
- 心得記於 `documents/PROCESS.md`；此檔 `EXECUTION-LOG.md`（repo 根目錄）為完整執行流水帳，一併提交供 PiC / reviewer 參考。

### 收尾：全 diff review + 微調

- 收工前用獨立唯讀子代理 review 整份 `git diff origin/main`：**結論 ship as-is**，無 Critical/Major。
- 依 review 的兩個 Minor/Nit 建議微調低庫存頁（commit 29467d1）：
  1. 移除 Controller `DefaultThreshold` 常數，統一用 `ViewModel.EffectiveThreshold`（單一預設來源）。
  2. 驗證失敗時不再渲染空表格，只顯示錯誤（避免誤導）。實測 threshold=0/-1 → 200、無表格、有驗證錯誤。
- `documents/PROCESS.md` 心得已填寫並提交（commit 491bc5a）。

### 完整 commit 序（origin/main 之後）

```
491bc5a 練習心得: 填寫 PROCESS.md
29467d1 練習3 收尾: 依 code review 微調低庫存頁
aeed2e3 練習4: 重構 CreateOrderAsync 的驗證邏輯（行為不變）
ed5b345 練習3: 新增低庫存警示頁 GET /Products/LowStock
5851ab6 練習2 bug3: 取消訂單時把庫存加回
391dda3 練習2 bug2: 修正 Gold 會員重複折扣
0915c4c 練習2 bug1: 修正訂單列表分頁 off-by-one
7ebeaef 練習1: 加入 Claude Code 專案設定
```
