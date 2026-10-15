using FlashLoop.App.Views;
using FlashLoop.Core.Backup;
using FlashLoop.Core.Model;

namespace FlashLoop.App;

/// <summary>
/// 單一主視窗，內容區切換 UserControl。全域快捷鍵：Ctrl+1 牌組、Ctrl+2 統計、Ctrl+3 設定。
/// </summary>
public sealed class MainForm : Form
{
    private readonly AppServices _services;
    private readonly Panel _content = new() { Dock = DockStyle.Fill };
    private readonly DeckListView _deckList;
    private readonly StatsView _stats;
    private readonly SettingsView _settings;
    private ReviewView? _review;

    public MainForm(AppServices services)
    {
        _services = services;
        Text = "FlashLoop 單字卡間隔複習";
        Font = UiTheme.Base;
        BackColor = UiTheme.Background;
        MinimumSize = new Size(800, 560);
        Size = new Size(1000, 680);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = false;

        _deckList = new DeckListView(services);
        _deckList.StartReviewRequested += (_, deck) => StartReview(deck);
        _stats = new StatsView(services);
        _settings = new SettingsView(services);

        var toolbar = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(8, 4, 8, 4),
            BackColor = UiTheme.Surface,
            Font = UiTheme.Base,
            RenderMode = ToolStripRenderMode.System,
        };
        toolbar.Items.Add(new ToolStripButton("牌組 (Ctrl+1)", null, (_, _) => ShowDecks()));
        toolbar.Items.Add(new ToolStripButton("統計 (Ctrl+2)", null, (_, _) => ShowStats()));
        toolbar.Items.Add(new ToolStripButton("設定 (Ctrl+3)", null, (_, _) => ShowSettings()));
        toolbar.Items.Add(new ToolStripSeparator());
        var backup = new ToolStripDropDownButton("備份");
        backup.DropDownItems.Add("匯出備份…", null, (_, _) => ExportBackup());
        backup.DropDownItems.Add("還原備份…", null, (_, _) => RestoreBackup());
        toolbar.Items.Add(backup);

        Controls.Add(_content);
        Controls.Add(toolbar);

        Shown += (_, _) => ShowDecks();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.D1:
                ShowDecks();
                return true;
            case Keys.Control | Keys.D2:
                ShowStats();
                return true;
            case Keys.Control | Keys.D3:
                ShowSettings();
                return true;
            default:
                return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    private void ShowView(Control view)
    {
        if (_content.Controls.Count == 1 && _content.Controls[0] == view)
        {
            return;
        }
        _content.SuspendLayout();
        _content.Controls.Clear();
        _content.Controls.Add(view);
        _content.ResumeLayout();
        view.Focus();
    }

    /// <summary>離開複習畫面前確認。評分都已即時存檔，所以離開不會遺失進度。</summary>
    private bool LeaveReview()
    {
        if (_review is null)
        {
            return true;
        }
        if (_review.InProgress && _content.Controls.Contains(_review))
        {
            var answer = MessageBox.Show(this,
                "要結束本次複習嗎？\n已評分的卡片都已儲存；尚未評分的卡片之後仍會出現。",
                "結束複習", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                return false;
            }
        }
        _review.Dispose();
        _review = null;
        return true;
    }

    private void ShowDecks()
    {
        if (!LeaveReview())
        {
            return;
        }
        ShowView(_deckList);
        _deckList.RefreshData();
    }

    private void ShowStats()
    {
        if (!LeaveReview())
        {
            return;
        }
        ShowView(_stats);
        _stats.RefreshData();
    }

    private void ShowSettings()
    {
        if (!LeaveReview())
        {
            return;
        }
        ShowView(_settings);
        _settings.RefreshData();
    }

    private void StartReview(Deck deck)
    {
        _review = new ReviewView(_services, deck);
        _review.ExitRequested += (_, _) => ShowDecks();
        _review.StatsRequested += (_, _) => ShowStats();
        ShowView(_review);
    }

    private void ExportBackup()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "匯出備份",
            Filter = "JSON 檔 (*.json)|*.json",
            FileName = $"flashloop-backup-{_services.Time.GetLocalNow():yyyyMMdd-HHmm}.json",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        try
        {
            File.WriteAllText(dialog.FileName, _services.Backup.Export());
            MessageBox.Show(this, $"已匯出備份：\n{dialog.FileName}", "匯出備份", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "無法匯出備份", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RestoreBackup()
    {
        if (!LeaveReview())
        {
            return;
        }

        using var dialog = new OpenFileDialog { Title = "還原備份", Filter = "JSON 檔 (*.json)|*.json|所有檔案 (*.*)|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var overwrite = new TaskDialogButton("覆蓋") { Tag = RestoreMode.Overwrite };
        var merge = new TaskDialogButton("合併") { Tag = RestoreMode.Merge };
        var page = new TaskDialogPage
        {
            Caption = "還原備份",
            Heading = "選擇還原方式",
            Text = "覆蓋：清空目前所有牌組與進度，完全以備份內容取代（包含每日上限設定）。\n\n" +
                   "合併：保留目前資料並加入備份內容；同一張卡片或牌組以較晚更新者為準。",
            Icon = TaskDialogIcon.Information,
            Buttons = { overwrite, merge, TaskDialogButton.Cancel },
            DefaultButton = merge,
        };
        if (TaskDialog.ShowDialog(this, page).Tag is not RestoreMode mode)
        {
            return;
        }

        if (mode == RestoreMode.Overwrite &&
            MessageBox.Show(this, "覆蓋會刪除目前所有資料，確定要繼續嗎？", "確認覆蓋",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            _services.Backup.Restore(File.ReadAllText(dialog.FileName), mode);
            MessageBox.Show(this, "還原完成。", "還原備份", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message + "\n\n目前資料未做任何變更。", "無法還原", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        ShowDecks();
    }
}
