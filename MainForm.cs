using System.Text;

namespace YtPrepis;

public sealed class MainForm : Form
{
    private sealed record Item(string Code, string Name)
    {
        public override string ToString() => Name;
    }

    private readonly TextBox _url = new() { Dock = DockStyle.Fill, PlaceholderText = "https://www.youtube.com/watch?v=…" };
    private readonly ComboBox _lang = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly ComboBox _model = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
    private readonly CheckBox _forceWhisper = new() { Text = "Vedno uporabi Whisper", AutoSize = true, Margin = new Padding(12, 6, 3, 3) };
    private readonly Button _run = new() { Text = "Prepiši", AutoSize = true };
    private readonly Button _cancel = new() { Text = "Prekliči", AutoSize = true, Enabled = false };
    private readonly ProgressBar _progress = new() { Width = 260, Height = 23, Margin = new Padding(12, 5, 3, 3) };
    private readonly Label _status = new() { AutoSize = true, Text = "Prilepi link in klikni Prepiši.", Margin = new Padding(3, 6, 3, 6) };
    private readonly TextBox _output = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical,
        Font = new Font("Segoe UI", 10.5f), WordWrap = true
    };
    private readonly Button _copy = new() { Text = "Kopiraj", AutoSize = true, Enabled = false };
    private readonly Button _save = new() { Text = "Shrani .txt", AutoSize = true, Enabled = false };

    private readonly TranscriptService _service = new();
    private CancellationTokenSource? _cts;
    private string _title = "prepis";

    public MainForm()
    {
        Text = "YT Prepis";
        Width = 900;
        Height = 650;
        MinimumSize = new Size(640, 420);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        _lang.Items.AddRange(new object[]
        {
            new Item("auto", "Samodejno"), new Item("sl", "Slovenščina"), new Item("en", "Angleščina"),
            new Item("hr", "Hrvaščina"), new Item("sr", "Srbščina"), new Item("bs", "Bosanščina"),
            new Item("fr", "Francoščina"), new Item("de", "Nemščina"), new Item("it", "Italijanščina"),
        });
        _lang.SelectedIndex = 0;

        _model.Items.AddRange(new object[]
        {
            new Item("tiny", "tiny (75 MB, hitro)"), new Item("base", "base (142 MB)"),
            new Item("small", "small (466 MB)"), new Item("medium", "medium (1,5 GB)"),
            new Item("large-v3-turbo", "large-v3-turbo (1,6 GB, najboljše)"),
        });
        _model.SelectedIndex = 2;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var urlRow = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        urlRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        urlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        urlRow.Controls.Add(MakeLabel("YouTube link:"), 0, 0);
        urlRow.Controls.Add(_url, 1, 0);

        var options = Flow(MakeLabel("Jezik:"), _lang, MakeLabel("Whisper model:"), _model, _forceWhisper);
        var actions = Flow(_run, _cancel, _progress);
        var bottom = Flow(_copy, _save);

        layout.Controls.Add(urlRow, 0, 0);
        layout.Controls.Add(options, 0, 1);
        layout.Controls.Add(actions, 0, 2);
        layout.Controls.Add(_status, 0, 3);
        layout.Controls.Add(_output, 0, 4);
        layout.Controls.Add(bottom, 0, 5);
        Controls.Add(layout);

        AcceptButton = _run;
        _run.Click += async (_, _) => await RunAsync();
        _cancel.Click += (_, _) => _cts?.Cancel();
        _copy.Click += (_, _) => { if (_output.TextLength > 0) Clipboard.SetText(_output.Text); };
        _save.Click += (_, _) => Save();
    }

    private async Task RunAsync()
    {
        if (string.IsNullOrWhiteSpace(_url.Text)) { _url.Focus(); return; }

        _cts = new CancellationTokenSource();
        SetBusy(true);
        _output.Clear();

        var progress = new Progress<ProgressInfo>(pi =>
        {
            _status.Text = pi.Status;
            if (pi.Percent is double pct)
            {
                _progress.Style = ProgressBarStyle.Blocks;
                _progress.Value = (int)Math.Clamp(pct, 0, 100);
            }
            else _progress.Style = ProgressBarStyle.Marquee;
        });

        var options = new TranscriptOptions(
            _url.Text,
            ((Item)_lang.SelectedItem!).Code,
            ((Item)_model.SelectedItem!).Code,
            _forceWhisper.Checked);

        try
        {
            var result = await Task.Run(() => _service.RunAsync(options, progress, _cts.Token));
            _title = result.Title;
            _output.Text = result.Text;
            _status.Text = $"Končano: {result.Title} · vir: {result.Source}";
            _progress.Style = ProgressBarStyle.Blocks;
            _progress.Value = 100;
        }
        catch (OperationCanceledException) { _status.Text = "Preklicano."; ResetProgress(); }
        catch (Exception ex) { _status.Text = "Napaka: " + ex.Message; ResetProgress(); }
        finally
        {
            SetBusy(false);
            _cts.Dispose();
            _cts = null;
        }
    }

    private void Save()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "Besedilo (*.txt)|*.txt",
            FileName = SafeFileName(_title) + ".txt",
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            File.WriteAllText(dlg.FileName, _output.Text, new UTF8Encoding(true));
            _status.Text = "Shranjeno: " + dlg.FileName;
        }
    }

    private void SetBusy(bool busy)
    {
        _run.Enabled = _url.Enabled = _lang.Enabled = _model.Enabled = _forceWhisper.Enabled = !busy;
        _cancel.Enabled = busy;
        _copy.Enabled = _save.Enabled = !busy && _output.TextLength > 0;
        UseWaitCursor = busy;
    }

    private void ResetProgress()
    {
        _progress.Style = ProgressBarStyle.Blocks;
        _progress.Value = 0;
    }

    private static string SafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Length > 120 ? name[..120] : name;
    }

    private static Label MakeLabel(string text) =>
        new() { Text = text, AutoSize = true, Margin = new Padding(3, 7, 3, 3) };

    private static FlowLayoutPanel Flow(params Control[] controls)
    {
        var f = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        f.Controls.AddRange(controls);
        return f;
    }
}
