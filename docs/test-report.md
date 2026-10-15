# FlashLoop 測試報告

| 項目 | 內容 |
|---|---|
| 測試日期 | 2026-10-15（第 2 版，依審查意見補強後重測） |
| 受測版本 | `main` @ `b84a57a`（ci: publish per-project test counts in the job summary） |
| 環境 | Windows 11 Pro 10.0.26200、.NET SDK 9.0.315、時區 UTC+08:00 |
| CI | GitHub Actions `windows-latest`，[run 38708762072](https://github.com/hn83320589/FlashLoop/actions/runs/38708762072)：success（截圖見第 7 節） |

## 1. 結論

| 類別 | 結果 |
|---|---|
| Release 建置 | 0 警告、0 錯誤（`TreatWarningsAsErrors` 開啟） |
| `FlashLoop.Tests`（單元＋資料層整合） | **130 / 130 通過** |
| `FlashLoop.App.Tests`（端到端，驅動真正的 MainForm） | **13 / 13 通過** |
| CI（Windows runner） | 兩個測試專案都在 CI 中執行並通過 |
| 驗收標準 15 條 | 全部有自動化測試佐證；#1、#3、#8、#12 另有涉及系統對話框與實體鍵盤的項目，需人工確認（見第 6 節） |

沒有發現不符合規格的缺陷。

## 2. 第 2 版的變更

| 審查意見 | 處理 | 新增的測試 |
|---|---|---|
| 啟動時設定檔或資料庫損毀 | `Program.Main` 分別攔截兩者的錯誤，讓使用者選「重設」或「離開」（預設離開）。重設時原檔改名為 `<檔名>.<時間>.bak` 保留，資料庫的 journal 一併移走。資料庫被占用（BUSY／LOCKED）時不提供重設 | `DataFileRecoveryTests`（3） |
| 覆蓋還原前自動備份 | 先匯出目前資料到 `<資料目錄>\backups\before-restore-<時間>.json`，成功後才還原；匯出失敗則不還原。自動備份全部保留、不覆寫 | `BackupServiceTests`（+3）、`BackupArchiveTests`（2）、`BackupFlowTests` |
| 備份資料一致性 | `BackupValidator` 新增檢查：到期日與最後複習時間要同時有值或同時為空、新卡的 reps 和 interval 為 0、複習過的卡 interval ≥ 1、interval ≤ 36500 | `Validator_requires_due_date_and_last_reviewed_*`、`Validator_rejects_interval_above_maximum` |
| 排程最大間隔 | interval 上限 36500 天。原本 `interval × ease` 直接轉成 `int`，持續評「簡單」最終會溢位成負數 | `Interval_never_exceeds_maximum`（3）、`Huge_stored_interval_does_not_overflow` |
| 端到端腳本不在 repo | 改寫成 `tests/FlashLoop.App.Tests`（xUnit），納入 CI | 13 個 |
| CI 證據 | CI 的 Job Summary 列出各測試專案的通過數；附上截圖 | — |

## 3. 測試方法

1. **單元／整合測試**（`tests/FlashLoop.Tests`）：時間一律由 `FakeTimeProvider` 控制。資料層測試使用真實的 SQLite 暫存檔，不使用 mock。
2. **端到端測試**（`tests/FlashLoop.App.Tests`）：每個測試從空的暫存資料目錄開始，組裝真實的服務與 `MainForm`。
   - 視窗開在螢幕外，不取得焦點。
   - 按鍵透過 `ProcessCmdKey` 送入。這和實體鍵盤在 WinForms 中走同一段處理程式，但不經過作業系統的鍵盤輸入。
   - 時間由可推進的 `ManualTimeProvider`（固定 UTC+8）控制，本機和 CI 算出相同的日期。
   - 畫面上的數字都和 **SQL 直接查詢資料庫** 的結果比對。
3. **靜態檢查**：專案相依、套件授權、網路呼叫，以及是否有直接取用系統時間的程式碼。

## 4. 驗收標準對照

| # | 標準 | 判定方式 | 證據 | 結果 |
|---|---|---|---|---|
| 1 | 牌組新增、改名、刪除，數量正確，重開後不變 | 實測 | `DeckAndImportFlowTests.Create_rename_delete_and_counts_survive_restart`；`DeckPersistenceTests`（6） | 通過\* |
| 2 | CSV 正確處理逗號、雙引號、換行、有無 BOM | 單元測試 | `CsvParserTests`（13）、`SampleFileTests`；`DeckAndImportFlowTests.Special_character_sample_keeps_every_field_intact` | 通過 |
| 3 | 匯入摘要數量正確，錯誤列指出行號與原因 | 單元＋實測 | `CsvImporterTests`（14）；`DeckAndImportFlowTests.Error_sample_summary_*`（含摘要對話框的內容） | 通過\* |
| 4 | 連續「普通」間隔為 1、6、round(6×ease)… | 單元測試 | `Consecutive_good_follows_1_6_then_interval_times_ease` | 通過 |
| 5 | 重來／困難／簡單逐條一致，ease ≥ 1.3 | 單元測試（含邊界） | `Sm2SchedulerTests`（30，含 ease 下限 8 個、間隔上限 4 個邊界案例） | 通過 |
| 6 | 「重來」的卡同次再出現，評普通以上後不再出現 | 單元測試 | `ReviewSessionTests`；`KeyboardReviewFlowTests.Again_card_returns_at_the_end_and_leaves_after_good` | 通過 |
| 7 | 每日上限生效，超出的卡不遺失、隔天出現 | 單元測試 | `ReviewQueueBuilderTests`（9）；`MultiDayFlowTests` | 通過 |
| 8 | 鍵盤完整操作複習流程 | 實測 | `KeyboardReviewFlowTests`（3） | 通過\* |
| 9 | streak 正確，中斷一天歸零，同日多次只算一天 | 單元測試 | `StreakCalculatorTests`（8）；`MultiDayFlowTests` | 通過 |
| 10 | 14 天預測與資料庫實際到期數一致 | 單元＋實測 | `Forecast_matches_raw_due_counts_in_database`；`MultiDayFlowTests`（比對畫面上圖表的資料與 SQL） | 通過 |
| 11 | Ctrl+Z 後排程欄位與當日統計都還原 | 單元測試 | 9 個與撤銷相關的單元測試；`KeyboardReviewFlowTests.Ctrl_Z_restores_the_database_row_and_today_statistics` | 通過 |
| 12 | JSON 備份完整還原；覆蓋、合併行為正確 | 單元測試 | `BackupMergerTests`（9）、`BackupServiceTests`（9）；`BackupFlowTests`（4） | 通過\* |
| 13 | 核心邏輯至少 25 個測試，`dotnet test` 全過 | 執行測試 | 143 個，全數通過（附錄 A） | 通過 |
| 14 | README 含建置、截圖、模組、演算法與統計說明 | 檢視 | 建置與執行、截圖、架構與模組、排程演算法、統計計算方式各有章節 | 通過 |
| 15 | GitHub Actions 在 Windows runner 綠燈，README 有徽章 | 檢視 Actions | run 38708762072 success；README 第 3 行有徽章；第 7 節截圖 | 通過 |

\* 自動化測試已通過；涉及系統對話框或實體鍵盤的部分請見第 6 節。

## 5. 端到端測試的觀察值

初始條件：資料目錄為空，時間設為 2026-10-15 09:00（+08:00），每日上限為預設的新卡 20、複習 100。以下數值都由測試斷言。

### 牌組與匯入（`DeckAndImportFlowTests`）

- **牌組操作**：新增 3 個、改名 1 個、刪除 1 個後，列表為 `N2 基礎 30/0/20`、`會話 6/0/6`（總數／到期／新卡）。重新啟動後列表不變。
- **名稱驗證**：同名（大小寫不同）與空白名稱都被拒絕。
- **`special_bom.csv`**：新增 6 張，逗號、`""` 跳脫、欄位內換行、非 BMP 字元、標籤整理都正確，BOM 沒有混入欄位。
- **`with_errors.csv`**：新增 4、略過 2、錯誤 6。

| 行號 | 類型 | 原因 |
|---|---|---|
| 4 | 略過 | 正面「勉強」與檔案第 2 行重複 |
| 6 | 略過 | 正面「apple」與檔案第 5 行重複（大小寫、前後空白不同） |
| 7 | 錯誤 | 欄位數為 1，應為 2 或 3 |
| 8 | 錯誤 | 欄位數為 4，應為 2 或 3 |
| 9 | 錯誤 | 正面為空白 |
| 10 | 錯誤 | 背面為空白 |
| 11 | 錯誤 | 結尾雙引號後出現多餘字元「後」 |
| 14 | 錯誤 | 引號未關閉：此行開始的欄位直到檔案結尾都沒有結尾雙引號 |

### 鍵盤複習（`KeyboardReviewFlowTests`）

- 按 Enter 開始複習，正面顯示「曖昧」、背面為空。未翻面時按 3 不會評分。按空白鍵後顯示「あいまい／模糊不清」。
- 第一張評「重來」：這張卡在第 21 次（隊尾）再次出現，評「普通」後就不再出現。整輪沒有任何一張卡連續出現。本次共 20 張不同的卡（新卡上限），資料庫中的值為 ease 2.3、reps 1、interval 1。
- 評分後按 Ctrl+Z：資料庫中的卡片欄位、今日已複習張數、評分次數都回到評分前，並回到同一張卡、正面朝上。

### 多日情境（`MultiDayFlowTests`）

| 時間點 | 觀察值 |
|---|---|
| 第 1 天 | N2 複習 20 張後，「會話」牌組沒有新卡（新卡上限是全域共用）。統計畫面：今日 20 張、連續 1 天，預測圖與 SQL 一致 |
| 第 2 天（上限改為新 5／複習 15） | 列表為 `N2 基礎 30/15/5`，資料庫實際到期 20 張；10 張新卡仍在；複習前 streak 1，複習後 2；超過複習上限的 5 張保留為逾期 |
| 第 4 天（跳過第 3 天） | streak 0；列表為 `N2 基礎 30/10/5`（含逾期的卡）；同一天複習兩個牌組後 streak 1；今日 15 張；預測圖與 SQL 一致 |

### 備份（`BackupFlowTests`）

- **覆蓋**：還原後牌組、卡片排程、評分紀錄逐欄位與來源一致，設定也套用備份中的值。`backups\` 中產生了自動備份，用它再做一次覆蓋還原，資料完全回到覆蓋前的狀態。
- **合併**：保留本機牌組與本機設定，不產生自動備份，同一份備份合併兩次結果不變。
- **損毀的 JSON**：被拒絕，資料庫不變，也不產生自動備份。
- **`samples/backup_example.json`**：可以還原。

## 6. 需要人工確認的項目

以下項目涉及系統的強制回應對話框（輸入框、檔案選擇、TaskDialog、MessageBox）或實體鍵盤。自動化測試無法在不搶焦點的情況下操作，請以實機確認：

| # | 操作 | 預期結果 | 結果 |
|---|---|---|---|
| 1 | Ctrl+N，輸入名稱後按 Enter | 新增牌組並選取該列 | ☐ |
| 1 | F2 改名、Del 刪除（含確認框） | 列表即時更新；關閉重開後不變 | ☐ |
| 3 | Ctrl+I，在檔案選擇視窗選 `samples\with_errors.csv` | 摘要對話框與第 5 節一致 | ☐ |
| 8 | 用實體鍵盤完成一輪複習：Enter → 空白鍵 → 1～4 → Ctrl+Z → 結束後按 Enter | 全程不需滑鼠 | ☐ |
| 8 | 複習中按 Esc | 出現確認框，取消後可繼續 | ☐ |
| 12 | 「備份」選單 → 匯出 → 還原，分別選「覆蓋」與「合併」 | 覆蓋完成的訊息顯示自動備份位置；合併不產生自動備份 | ☐ |
| — | 關閉程式，把 `settings.json` 內容改成 `{ broken` 後啟動 | 出現「重設／離開」；選重設後產生 `.bak`，以預設上限開啟 | ☐ |
| — | 關閉程式，把 `flashloop.db` 內容改成任意文字後啟動 | 出現「重設／離開」；選重設後產生 `.bak`，開啟空白資料庫 | ☐ |

最後兩項建議用 `FLASHLOOP_DATA_DIR` 指到測試用的資料夾，以免動到真正的資料：

```powershell
$env:FLASHLOOP_DATA_DIR = "$env:TEMP\flashloop-manual"
dotnet run --project src/FlashLoop.App
```

## 7. 靜態檢查與 CI

| 項目 | 結果 |
|---|---|
| Core 為純 `net9.0`，不引用 WinForms | `FlashLoop.Core.csproj` 沒有任何套件或 WinForms 參考 |
| 套件授權 | 只用 Microsoft.Data.Sqlite 9.0.10、xunit 2.9.2、xunit.runner.visualstudio 2.8.2、Microsoft.NET.Test.Sdk 17.12.0（皆為 MIT 或 Apache-2.0） |
| 不呼叫外部網路 | `src`、`tests` 中沒有 `HttpClient`、`WebRequest`、`Socket` 或 URL |
| 時間可控 | 沒有直接使用 `DateTime.Now`／`UtcNow`／`Today`，一律透過 `TimeProvider` |
| 資料位置 | `%LocalAppData%\FlashLoop\`（可用 `FLASHLOOP_DATA_DIR` 覆寫） |
| CI | `windows-latest`、`setup-dotnet` 9.0.x；Restore → Build → Test → Test summary → Upload trx，全部成功 |

CI run 38708762072 的頁面截圖：

![CI 綠燈](screenshots/08-ci-actions.png)

GitHub 的執行 log 和 Job Summary 需要登入才能查看。以下證據不需要登入就能從公開 API 取得：

- Jobs API 顯示 Test、Test summary 等每個步驟的 conclusion 都是 success。
- 上傳的 trx 產物從單一測試專案時的 24.8 KB 增加為 31.5 KB，與多出第二個測試專案的結果檔相符。

CI 執行的是 `dotnet test FlashLoop.sln`，任何一個測試專案失敗，這一步都會失敗。

CI 頁面上有一則 Node.js 20 deprecation 警告，來自 `actions/checkout@v4` 等官方 action。它不影響建置結果，本次沒有處理。

## 附錄 A：本機 `dotnet test` 輸出

```text
> dotnet build FlashLoop.sln -c Release --no-incremental
    0 個警告
    0 個錯誤

> dotnet test FlashLoop.sln -c Release --no-build
D:\workspace\FlashLoop\tests\FlashLoop.Tests\bin\Release\net9.0\FlashLoop.Tests.dll 的測試回合 (.NETCoreApp,Version=v9.0)
D:\workspace\FlashLoop\tests\FlashLoop.App.Tests\bin\Release\net9.0-windows\FlashLoop.App.Tests.dll 的測試回合 (.NETCoreApp,Version=v9.0)
VSTest 版本 17.14.1 (x64)
正在啟動測試執行，請稍候...
總共有 1 個測試檔案與指定的模式相符。
已通過! - 失敗:     0，通過:   130，略過:     0，總計:   130，持續時間: 2 s - FlashLoop.Tests.dll (net9.0)
已通過! - 失敗:     0，通過:    13，略過:     0，總計:    13，持續時間: 5 s - FlashLoop.App.Tests.dll (net9.0)
```

## 附錄 B：重現方式

```powershell
dotnet build FlashLoop.sln -c Release
dotnet test FlashLoop.sln -c Release --no-build
```
