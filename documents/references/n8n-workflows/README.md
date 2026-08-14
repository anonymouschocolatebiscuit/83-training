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
