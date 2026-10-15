using FlashLoop.Core.Csv;

namespace FlashLoop.App.Dialogs;

/// <summary>顯示匯入摘要：新增、略過、錯誤數量，以及每一筆略過／錯誤的行號與原因。</summary>
public sealed class ImportResultDialog : Form
{
    private ImportResultDialog(string fileName, ImportSummary summary)
    {
        Text = "匯入結果";
        Font = UiTheme.Base;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = ShowInTaskbar = false;
        Size = new Size(720, 460);
        Padding = new Padding(12);

        var header = new Label
        {
            Text = $"{fileName}\n新增 {summary.Added} 張　略過 {summary.Skipped} 張（重複）　錯誤 {summary.Errors.Count} 列",
            Font = UiTheme.Heading,
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 0, 0, 8),
        };

        var list = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Dock = DockStyle.Fill,
        };
        list.Columns.Add("類型", 70);
        list.Columns.Add("行號", 60, HorizontalAlignment.Right);
        list.Columns.Add("原因", 540);
        var rows = summary.Errors.Select(e => (Kind: "錯誤", Issue: e))
            .Concat(summary.SkippedRows.Select(s => (Kind: "略過", Issue: s)))
            .OrderBy(r => r.Issue.LineNumber);
        foreach (var (kind, issue) in rows)
        {
            var item = new ListViewItem([kind, issue.LineNumber.ToString(), issue.Reason])
            {
                ForeColor = kind == "錯誤" ? UiTheme.Again : UiTheme.MutedText,
            };
            list.Items.Add(item);
        }
        if (list.Items.Count == 0)
        {
            list.Items.Add(new ListViewItem(["", "", "全部成功匯入，沒有略過或錯誤的列。"]));
        }

        var close = new Button { Text = "關閉", DialogResult = DialogResult.OK, AutoSize = true, Dock = DockStyle.Right };
        var footer = new Panel { Dock = DockStyle.Bottom, Height = close.PreferredSize.Height + 12, Padding = new Padding(0, 8, 0, 0) };
        footer.Controls.Add(close);

        Controls.Add(list);
        Controls.Add(footer);
        Controls.Add(header);
        AcceptButton = close;
        CancelButton = close;
    }

    public static void Show(IWin32Window owner, string fileName, ImportSummary summary)
    {
        using var dialog = new ImportResultDialog(fileName, summary);
        dialog.ShowDialog(owner);
    }
}
