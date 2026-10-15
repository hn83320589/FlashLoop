using FlashLoop.App.Dialogs;
using FlashLoop.Core.Model;
using FlashLoop.Core.Services;

namespace FlashLoop.App.Views;

/// <summary>
/// 牌組列表。鍵盤：Enter 開始複習、Ctrl+N 新增、F2 重新命名、Del 刪除、Ctrl+I 匯入 CSV。
/// </summary>
public sealed class DeckListView : UserControl
{
    private readonly AppServices _services;
    private readonly DataGridView _grid;
    private readonly Label _empty;

    public event EventHandler<Deck>? StartReviewRequested;

    public DeckListView(AppServices services)
    {
        _services = services;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Background;
        Padding = new Padding(16);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = UiTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Font = UiTheme.Base,
            RowTemplate = { Height = 34 },
            ColumnHeadersHeight = 36,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            StandardTab = true,
        };
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "牌組", FillWeight = 50 });
        AddNumberColumn("Total", "卡片總數");
        AddNumberColumn("Due", "今日待複習");
        AddNumberColumn("New", "今日新卡");
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                StartReview();
            }
        };

        _empty = new Label
        {
            Text = "還沒有牌組。按「新增牌組」(Ctrl+N) 建立第一個牌組，再匯入 CSV。",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = UiTheme.MutedText,
            Visible = false,
        };

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 8) };
        toolbar.Controls.AddRange(
        [
            MakeButton("開始複習 (Enter)", (_, _) => StartReview(), primary: true),
            MakeButton("新增牌組 (Ctrl+N)", (_, _) => CreateDeck()),
            MakeButton("重新命名 (F2)", (_, _) => RenameDeck()),
            MakeButton("刪除 (Del)", (_, _) => DeleteDeck()),
            MakeButton("匯入 CSV (Ctrl+I)", (_, _) => ImportCsv()),
        ]);

        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.Add(_grid);
        body.Controls.Add(_empty);

        Controls.Add(body);
        Controls.Add(toolbar);
    }

    private void AddNumberColumn(string name, string header) =>
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            FillWeight = 16,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight },
        });

    private static Button MakeButton(string text, EventHandler onClick, bool primary = false)
    {
        var b = new Button { Text = text, AutoSize = true, Padding = new Padding(8, 4, 8, 4), TabStop = false };
        if (primary)
        {
            b.BackColor = UiTheme.Accent;
            b.ForeColor = Color.White;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
        }
        b.Click += onClick;
        return b;
    }

    public void RefreshData(long? selectDeckId = null)
    {
        selectDeckId ??= SelectedDeck?.Id;
        var overviews = _services.Decks.GetOverviews();

        _grid.Rows.Clear();
        foreach (var o in overviews)
        {
            var index = _grid.Rows.Add(o.Deck.Name, o.TotalCards, o.DueToday, o.NewToday);
            _grid.Rows[index].Tag = o.Deck;
            if (o.Deck.Id == selectDeckId)
            {
                _grid.CurrentCell = _grid.Rows[index].Cells[0];
            }
        }

        _grid.Visible = overviews.Count > 0;
        _empty.Visible = overviews.Count == 0;
        _grid.Focus();
    }

    private Deck? SelectedDeck => _grid.CurrentRow?.Tag as Deck;

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Enter:
                StartReview();
                return true;
            case Keys.Control | Keys.N:
                CreateDeck();
                return true;
            case Keys.F2:
                RenameDeck();
                return true;
            case Keys.Delete:
                DeleteDeck();
                return true;
            case Keys.Control | Keys.I:
                ImportCsv();
                return true;
            default:
                return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    private void StartReview()
    {
        if (SelectedDeck is { } deck)
        {
            StartReviewRequested?.Invoke(this, deck);
        }
    }

    private void CreateDeck()
    {
        var name = TextPromptDialog.Ask(this, "新增牌組", "牌組名稱：");
        if (name is null)
        {
            return;
        }
        if (TryRun(() => _services.Decks.Create(name), out var deck))
        {
            RefreshData(deck.Id);
        }
    }

    private void RenameDeck()
    {
        if (SelectedDeck is not { } deck)
        {
            return;
        }
        var name = TextPromptDialog.Ask(this, "重新命名牌組", "新名稱：", deck.Name);
        if (name is not null && TryRun(() => { _services.Decks.Rename(deck.Id, name); return true; }, out _))
        {
            RefreshData(deck.Id);
        }
    }

    private void DeleteDeck()
    {
        if (SelectedDeck is not { } deck)
        {
            return;
        }
        var total = _grid.CurrentRow!.Cells["Total"].Value;
        var answer = MessageBox.Show(this,
            $"確定要刪除牌組「{deck.Name}」嗎？\n其中 {total} 張卡片與學習紀錄都會一併刪除，且無法復原。",
            "刪除牌組", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer == DialogResult.Yes)
        {
            _services.Decks.Delete(deck.Id);
            RefreshData();
        }
    }

    private void ImportCsv()
    {
        if (SelectedDeck is not { } deck)
        {
            MessageBox.Show(this, "請先選擇要匯入的牌組。", "匯入 CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = $"匯入 CSV 到「{deck.Name}」",
            Filter = "CSV 檔 (*.csv)|*.csv|所有檔案 (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var summary = _services.Import.Import(deck.Id, File.ReadAllBytes(dialog.FileName));
            RefreshData(deck.Id);
            ImportResultDialog.Show(this, Path.GetFileName(dialog.FileName), summary);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "無法匯入", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool TryRun<T>(Func<T> action, out T result)
    {
        try
        {
            result = action();
            return true;
        }
        catch (ValidationException ex)
        {
            MessageBox.Show(this, ex.Message, "輸入有誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            result = default!;
            return false;
        }
    }
}
