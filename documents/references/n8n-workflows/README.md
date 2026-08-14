# 活動 4 — n8n 匯入用 workflow JSON + 手動步驟

這裡放**可 Import 進 n8n 的 workflow JSON**,對應活動 4 的練習 1–3。因為 n8n 全靠瀏覽器 GUI(`http://localhost:5678`)操作,自動化 session 無法點擊,所以改用「產可匯入的 JSON + 寫清楚剩下要真人做的步驟」的方式交付;凡**只能由真人在 GUI 完成**的步驟(建帳號、填憑證、按 Execute、看執行紀錄)都標 `[~]` 並註明原因,不偽造截圖、不假稱「跑起來了」。

## 怎麼匯入

1. `npx n8n` 啟動,瀏覽器開 `http://localhost:5678`,首次進入建立 owner 帳號(存本機)。`[~]` 建帳號需真人在 GUI。
2. 左上 **⋮ / Create Workflow → 右上 ⋮ → Import from File**,選這個資料夾裡的 `.json`。
3. 匯入後,**憑證(credential)不會跟著 JSON 進來**(n8n 設計上不匯出金鑰),需照下面各練習的說明**手動新建並掛上**。
4. 節點的 `typeVersion` 若與你的 n8n 版本略有出入,n8n 會照樣匯入,打開節點再存一次即可(n8n 對匯入很寬容;必須精確的是 `type` 字串與連線,已對齊官方 source 驗證)。

> **模型名稱**:活動文件寫 `gemini-3.5-flash`,但活動 3 實測本金鑰該模型不存在、`gemini-2.0-flash` 免費配額為 0,落地用 **`gemini-2.5-flash`**。Gemini 節點的 `modelName` 已填 `models/gemini-2.5-flash`;若你的金鑰對某模型無配額,改這欄即可。

---

## 練習 1 — Hello Webhook（`01-hello-webhook.json`）

**目標**:最小迴路 trigger → 節點 → 回應。三個節點:Webhook(POST `/hello`,Respond 設 **Using Respond to Webhook Node**)→ Edit Fields(加 `receivedAt = {{ $now.toISO() }}`、**Include Other Input Fields** 打開)→ Respond to Webhook(**First Incoming Item**)。

JSON 裡這些都設好了:`responseMode: "responseNode"`(就是「Using Respond to Webhook Node」,活動特別提醒這步最容易漏——預設 _Immediately_ 會忽略 Respond 節點)、`includeOtherFields: true`(不開的話你送進來的 body 會被丟掉)。

### 匯入後要真人做的步驟

1. `[~]` 匯入 `01-hello-webhook.json`。
2. `[~]` 打開 **Webhook** 節點,複製 **Test URL**(上方有 Test / Production 兩分頁,現在用 **Test**)。
3. `[~]` 回畫布按 **Execute Workflow**(或 Webhook 節點的 _Listen for test event_)——按下去 Test URL 才開始監聽,**只活 120 秒、收到一發即停**。
4. `[~]` 趁監聽中,另開一個 PowerShell 打:

   ```powershell
   Invoke-RestMethod -Method Post -Uri "<n8n給的Test URL>" -Body '{"text":"hello"}' -ContentType "application/json"
   ```

   應回 `text=hello` 加上 `receivedAt` 時間戳,畫布每個節點亮綠勾。

**為何全標 `[~]`**:Import 之後的每一步(複製 Test URL、按 Execute 進入 120 秒監聽、看節點綠勾)都是 n8n 編輯器內的互動操作,自動化 session 無瀏覽器可點。JSON 已把三個節點與連線、以及最容易漏的 `responseMode`/`includeOtherFields` 都設定完成,真人只需匯入後按 Execute + 打一發 request。

**驗證清單(對照活動)**:

- [~] 回應含你送的內容 + 時間戳 — 需真人按 Execute 後打 request
- [~] 理解 Test URL vs Production URL 差別:Test 要在編輯器按 Listen 才活(120 秒、一發即停);**Activate** 後才有常駐 Production URL(練習 2 的通知節點會用到),紀錄看 **Executions** 分頁

---

## 練習 2 — 退單巡檢日報（`02-退單巡檢日報.json`）

**目標**:一條端到端流程——排程觸發 → 查近 30 天取消的訂單(打**活動 3 的 `/api/orders/search`**,零新程式碼)→ AI Agent(Gemini)寫中文日報 → 有退單開 GitHub issue + 通知,沒退單記進 Data Table 歸檔。

**流程(9 節點)**:`Schedule Trigger` → `查退單 (search API)` → `整理筆數`(Code) → `AI Agent`(掛 `Google Gemini Chat Model`) → `IF 退單筆數>0` → true:`開 GitHub Issue` → `通知 (打練習1 webhook)`;false:`歸檔 (Data Table)`。

JSON 裡已設好活動點名的幾個關鍵:
- HTTP Request 的 **Always Output Data**(`alwaysOutputData: true`,節點頂層)——不開的話 API 查無資料回 `[]`、節點輸出 0 個 item,下游(含 IF)不會執行,歸檔分支永遠走不到。
- `整理筆數` Code 節點:先濾掉 Always Output Data 的空 item 再算 `count`,輸出單一 item `{count, orders}`。**節點名必須是「整理筆數」**——IF 的 expression 用 `$('整理筆數')` 跨節點回頭拿。
- IF 左值 `={{ $('整理筆數').first().json.count }}`、型別 Number、operator _is greater than_、右值 `0`(AI Agent 輸出已無 `count`,所以跨節點拿)。
- GitHub 標題 `={{ $json.output.split('\n')[0] }}`(日報第一行)、內文 `={{ $json.output }}`。
- 通知節點帶 `report`(`={{ $('AI Agent').first().json.output }}`)與 `issueUrl`(`={{ $json.html_url }}`,GitHub 剛開的 issue 連結)。
- Data Table 插一列 `date={{ $now.toFormat('yyyy-MM-dd') }}`、`note=本日無退單`。
- Gemini `modelName` = `models/gemini-2.5-flash`(見上方模型名稱說明)。

### 匯入後要真人做的步驟（憑證/表/URL 無法隨 JSON 匯入）

1. 先把網站跑起來:`dotnet run --project src/OrderHub.Web`(埠 5150)——`查退單` 節點打的就是這個。
2. `[~]` **Gemini 憑證**:打開 `Google Gemini Chat Model` 節點 → Credential → _Create new_ → 填活動 3 的 API key(憑證型別 `Google Gemini(PaLM) Api`)。金鑰**不會**隨 JSON 匯入,必須真人在 GUI 填。
3. `[~]` **GitHub 憑證 + repo**:打開 `開 GitHub Issue` 節點 → Credential _Create new_ 填 GitHub PAT(classic、勾 `repo` scope);把 `owner`/`repository` 兩欄從占位字串(`你的GitHub帳號`/`你的training-repo名稱`)改成你的真實 repo(或改 mode 用 _By URL_ 貼網址)。
4. `[~]` **建 Data Table**:左側 **Overview → Data tables → Create Data table**,表名 `巡檢紀錄`,加兩欄 `date`(String)、`note`(String);回到 `歸檔 (Data Table)` 節點把 Data Table 選成剛建的表(JSON 裡的 `value:"巡檢紀錄"` 是占位,真人需在 _From list_ 重選以綁到真實 `dataTableId`)。
5. `[~]` **通知 URL**:把 `通知 (打練習1 webhook)` 節點的 `url` 從占位字串改成**練習 1 workflow Activate 後的 Production URL**(先去練習 1 按 **Activate**,正好體會 Production URL 的用途)。
6. `[~]` **測資 + 執行**:在網站取消一筆待處理訂單(或用活動 2 的 `cancel_order`),然後按 **Execute Workflow** 手動觸發(開發期不必等排程)。看 GitHub 是否開出 issue、Data Table 是否留痕。

**為何這些標 `[~]`**:金鑰、GitHub PAT、Data Table 的實體 `dataTableId`、練習 1 的 Production URL 都是**執行環境/帳號綁定、且 n8n 不匯出憑證**,必須真人在 GUI 建立;按 Execute 看結果也是編輯器互動。JSON 已把 9 個節點、所有連線(含 AI 子節點 `ai_languageModel` 連線、IF 的 true/false 兩路)、以及所有 expression 與易漏參數(`alwaysOutputData`、`整理筆數` 濾空、跨節點 `count`)都設定完成。

**驗證清單(對照活動)**:

- [x] workflow JSON 可匯入、流程結構與 expression 正確(schema 對齊官方 source、review 子代理驗證)
- [~] Execute 後開出 GitHub issue、收到通知、日報數字與 `/Orders` 篩「已取消」一致 — 需真人填憑證後執行
- [~] 改成查不到的條件 → 存進 Data Table、不開 issue — 需真人執行
- [x] 思考題「查什麼、怎麼查也交給 AI 自由發揮會失去什麼」→ 已答於 `documents/PROCESS.md` 第四階段
