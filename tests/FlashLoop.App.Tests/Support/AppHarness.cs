using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using FlashLoop.App.Views;
using FlashLoop.Core.Backup;
using FlashLoop.Core.Review;
using FlashLoop.Core.Services;
using FlashLoop.Data;
using Microsoft.Data.Sqlite;

namespace FlashLoop.App.Tests.Support;

/// <summary>
/// 端到端測試用的真實應用程式：暫存資料目錄中的 SQLite 與設定檔、完整的服務組裝，以及真正的 MainForm。
/// 視窗開在螢幕外且不取得焦點；按鍵透過 ProcessCmdKey 送入，與實體鍵盤在 WinForms 中走同一段處理。
/// UI 的內部控制項以 reflection 取得，畫面結構改變時這裡需要跟著調整。
/// </summary>
internal sealed class AppHarness : IDisposable
{
    private const int SwShowNoActivate = 4;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private MainForm? _form;

    public AppHarness(ManualTimeProvider? time = null)
    {
        Time = time ?? new ManualTimeProvider(new DateTime(2026, 10, 15, 9, 0, 0));
        DataDirectory = Path.Combine(Path.GetTempPath(), "FlashLoopAppTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DataDirectory);
        Open();
    }

    public ManualTimeProvider Time { get; }
    public string DataDirectory { get; }
    public string DatabaseFile => Path.Combine(DataDirectory, "flashloop.db");
    public string BackupsDirectory => Path.Combine(DataDirectory, "backups");
    public SqliteStore Store { get; private set; } = null!;
    public JsonSettingsStore Settings { get; private set; } = null!;
    public AppServices Services { get; private set; } = null!;

    public MainForm Form => _form ?? throw new InvalidOperationException("主視窗尚未開啟");
    public DeckListView DeckList => Field<DeckListView>(Form, "_deckList");
    public DataGridView DeckGrid => Field<DataGridView>(DeckList, "_grid");
    public StatsView StatsView => Field<StatsView>(Form, "_stats");
    public ReviewView Review => Field<ReviewView>(Form, "_review");
    public ReviewSession Session => Field<ReviewSession>(Review, "_session");
    public string Today => DateOnly.FromDateTime(Time.GetLocalNow().DateTime).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>模擬關閉程式後重新啟動：關掉主視窗、釋放連線，再以同一個資料目錄重新組裝。</summary>
    public void Restart()
    {
        CloseForm();
        Open();
    }

    private void Open()
    {
        Settings = new JsonSettingsStore(Path.Combine(DataDirectory, "settings.json"));
        Store = new SqliteStore(DatabaseFile);
        Services = new AppServices(
            new DeckService(Store, Store, Store, Settings, Time),
            new ImportService(Store, Time),
            new ReviewService(Store, Store, Store, Settings, Time),
            new StatsService(Store, Store, Time),
            new BackupService(Store, Settings, new BackupArchive(BackupsDirectory), Time),
            Settings,
            Time);

        _form = new MainForm(Services)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            ShowInTaskbar = false,
        };
        ShowWindow(_form.Handle, SwShowNoActivate);
        _form.Visible = true;
        Pump();
        Invoke(_form, "ShowDecks");
        Pump();
    }

    private void CloseForm()
    {
        // 直接 Dispose 而不呼叫 Close，避免複習進行中的確認對話框卡住測試
        _form?.Dispose();
        _form = null;
        Pump();
        SqliteConnection.ClearAllPools();
    }

    public void Dispose()
    {
        CloseForm();
        try
        {
            Directory.Delete(DataDirectory, recursive: true);
        }
        catch (IOException)
        {
            // 暫存目錄刪除失敗不影響測試結果；留給系統清理
        }
    }

    public void Press(Control target, Keys keys)
    {
        var method = typeof(Control).GetMethod("ProcessCmdKey", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var message = new Message();
        method.Invoke(target, [message, keys]);
        Pump();
    }

    /// <summary>牌組列表的每一列：「名稱 總數/到期/新卡」。</summary>
    public List<string> DeckRows()
    {
        Invoke(DeckList, "RefreshData", [null]);
        Pump();
        return DeckGrid.Rows.Cast<DataGridViewRow>()
            .Select(r => $"{r.Cells[0].Value} {r.Cells[1].Value}/{r.Cells[2].Value}/{r.Cells[3].Value}")
            .ToList();
    }

    /// <summary>在牌組列表選取指定牌組並按 Enter 開始複習。</summary>
    public void StartReview(string deckName)
    {
        DeckRows();
        DeckGrid.CurrentCell = DeckGrid.Rows.Cast<DataGridViewRow>().Single(r => Equals(r.Cells[0].Value, deckName)).Cells[0];
        Press(DeckList, Keys.Enter);
    }

    /// <summary>以鍵盤複習指定牌組直到結束（每張都翻面後按同一個評分鍵），再按 Enter 回到列表。</summary>
    public SessionSummary ReviewAll(string deckName, Keys ratingKey)
    {
        StartReview(deckName);
        var review = Review;
        for (var guard = 0; review.InProgress && guard < 500; guard++)
        {
            Press(review, Keys.Space);
            Press(review, ratingKey);
        }
        Assert.False(review.InProgress, "複習沒有在預期的次數內結束");
        var summary = Session.Summary;
        Press(review, Keys.Enter);
        return summary;
    }

    public long Scalar(string sql)
    {
        using var connection = OpenRaw();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }

    /// <summary>直接從資料庫讀出卡片的排程欄位。</summary>
    public string CardRow(long cardId)
    {
        using var connection = OpenRaw();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT ease, repetitions, interval_days, due_date, last_reviewed_at FROM cards WHERE id = $id";
        command.Parameters.AddWithValue("$id", cardId);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        return string.Create(CultureInfo.InvariantCulture,
            $"ease={reader.GetDouble(0)} reps={reader.GetInt64(1)} interval={reader.GetInt64(2)} " +
            $"due={(reader.IsDBNull(3) ? "null" : reader.GetString(3))} last={(reader.IsDBNull(4) ? "null" : reader.GetString(4))}");
    }

    /// <summary>以 guid 排序輸出牌組、卡片與評分紀錄的所有欄位，用來比對兩個資料庫是否完全一致。</summary>
    public string Dump()
    {
        using var connection = OpenRaw();
        var sb = new StringBuilder();
        foreach (var sql in new[]
        {
            "SELECT guid, name FROM decks ORDER BY guid",
            "SELECT c.guid, d.guid, front, back, tags, ease, repetitions, interval_days, due_date, last_reviewed_at FROM cards c JOIN decks d ON d.id = c.deck_id ORDER BY c.guid",
            "SELECT l.guid, c.guid, reviewed_at, review_date, rating, was_new FROM review_logs l JOIN cards c ON c.id = l.card_id ORDER BY l.guid",
        })
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    sb.Append(reader.IsDBNull(i) ? "null" : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture)).Append('|');
                }
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    private SqliteConnection OpenRaw()
    {
        var connection = new SqliteConnection($"Data Source={DatabaseFile};Pooling=False");
        connection.Open();
        return connection;
    }

    public static T Field<T>(object target, string name) where T : class =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target) as T
        ?? throw new InvalidOperationException($"{target.GetType().Name}.{name} 不存在或為 null");

    public static void Invoke(object target, string name, object?[]? args = null)
    {
        var method = target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                     ?? throw new InvalidOperationException($"{target.GetType().Name}.{name} 不存在");
        method.Invoke(target, args ?? []);
    }

    /// <summary>處理排隊中的視窗訊息（版面配置、繪製），讓畫面狀態與程式狀態一致。</summary>
    public static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Application.DoEvents();
        }
    }

    /// <summary>收集控制項樹中所有可見文字（含 ListView、DataGridView 的儲存格）。</summary>
    public static string AllText(Control root)
    {
        var sb = new StringBuilder(root.Text).Append('\n');
        if (root is ListView list)
        {
            foreach (ListViewItem item in list.Items)
            {
                sb.AppendLine(string.Join(" ", item.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => s.Text)));
            }
        }
        if (root is DataGridView grid)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                sb.AppendLine(string.Join(" ", row.Cells.Cast<DataGridViewCell>().Select(c => c.Value)));
            }
        }
        foreach (Control child in root.Controls)
        {
            sb.Append(AllText(child));
        }
        return sb.ToString();
    }
}
