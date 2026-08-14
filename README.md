# OrderHub

練習說明入口請看 **[documents/README.md](documents/README.md)**

## 活動 4 心得（n8n 自動化）

活動 4 把前三課合體成一條 n8n 巡檢流程,是整個訓練「收束」得最漂亮的一課——它讓前三課的每個決定都**兌現**了一次:n8n 直接打**活動 3 的 `/api/orders/search`**(查詢邏輯零複製)、掛活動 3 的 Gemini key、透過**活動 2 的 MCP server** 深挖明細。換一個 orchestrator(從網站頁面換成 n8n),產品程式碼一行不用改——分層的紅利看得見。最優雅的註腳是「補齊」那段:同一個 MCP server,工具/Resource/Prompt 一行不改,只加一個 `--http` 分支換 transport,就從 stdio 子行程變成 n8n 可連的 HTTP 服務(在 `:3001` 用 JSON-RPC 端到端實測過,`get_order` 回 `Total:12660`,與活動 2 一致)。安全哲學也一以貫之:無人流程只掛 `get_order`(讀)、**絕不掛 `cancel_order`(寫)**——活動 1 的 approval 精神在這裡的形狀就是「根本不給那個工具」。

**誠實邊界**:n8n 練習 1–3 全靠瀏覽器 GUI,自動化 agent 無法點畫布,故改為產出**可匯入的 workflow JSON + 逐字手動步驟**(見 [`documents/references/n8n-workflows/`](documents/references/n8n-workflows/)),GUI-only 步驟誠實標 `[~]`,不偽造截圖。完整心得與兩題思考題在 [`documents/PROCESS.md`](documents/PROCESS.md) 第四階段、逐步流水帳在 [`EXECUTION-LOG-4.md`](EXECUTION-LOG-4.md)。

## 練習規則

請 fork 專案到自己的帳號進行練習。

## Fork 流程

1. 點右上角 **Fork** 建立自己帳號下的複本。

2. Clone 你 fork 出來的專案並進入目錄（把 `你的帳號` 換成你的 GitHub 帳號）：

   ```powershell
   git clone https://github.com/你的帳號/traning.git
   cd traning
   ```

3. 在你的 fork 上進行練習並 commit：

   ```powershell
   git add .
   git commit -m "你的 commit 訊息"
   ```

4. 推上你的 fork：

   ```powershell
   git push
   ```

## 同步原專案最新內容

當原專案 `main` 有更新時，用以下步驟把最新內容拉進你的 fork。

1. 加上原專案為 `upstream` 遠端（只需設定一次，`git remote -v` 可確認）：

   ```powershell
   git remote add upstream https://github.com/sox6769/traning.git
   ```

2. 抓取原專案最新內容並合併到本地 `main`：

   ```powershell
   git switch main
   git fetch upstream
   git merge upstream/main
   ```

   ⚠️ 若有衝突，Git 會列出衝突檔案，解完後 `git add .` 再 `git commit` 完成合併。

3. 把同步後的 `main` 推回你的 fork：

   ```powershell
   git push
   ```
