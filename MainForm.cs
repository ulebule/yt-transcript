using System.Text;

namespace YtTranscript;

public sealed class MainForm : Form
{
    /// <summary>Combo box item: a fixed code plus a display name that follows the UI language.</summary>
    private sealed record Item(string Code, Func<string> Name)
    {
        public override string ToString() => Name();
    }

    private static readonly string[] VideoLanguages = { "auto", "sl", "en", "hr", "sr", "bs", "fr", "de", "it" };
    private static readonly string[] Models = { "tiny", "base", "small", "medium", "large-v3-turbo" };
    private const int DefaultModelIndex = 2; // small

    private readonly Label _uiLangLabel = MakeLabel();
    private readonly ComboBox _uiLang = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly Label _urlLabel = MakeLabel();
    private readonly TextBox _url = new() { Dock = DockStyle.Fill, PlaceholderText = "https://www.youtube.com/watch?v=…" };
    private readonly Label _langLabel = MakeLabel();
    private readonly ComboBox _lang = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly Label _modelLabel = MakeLabel();
    private readonly ComboBox _model = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    private readonly CheckBox _forceWhisper = new() { AutoSize = true, Margin = new Padding(12, 6, 3, 3) };
    private readonly Button _run = new() { AutoSize = true };
    private readonly Button _cancel = new() { AutoSize = true, Enabled = false };
    private readonly ProgressBar _progress = new() { Width = 260, Height = 23, Margin = new Padding(12, 5, 3, 3) };
    private readonly Label _status = new() { AutoSize = true, Margin = new Padding(3, 6, 3, 6) };
    private readonly TextBox _output = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical,
        Font = new Font("Segoe UI", 10.5f), WordWrap = true
    };
    private readonly Button _copy = new() { AutoSize = true, Enabled = false };
    private readonly Button _save = new() { AutoSize = true, Enabled = false };

    private readonly TranscriptService _service = new();
    private CancellationTokenSource? _cts;
    private string _title = "transcript";
    private bool _statusIsReady = true;

    public MainForm()
    {
        Text = "YT Transcript";
        Width = 900;
        Height = 650;
        MinimumSize = new Size(640, 420);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        foreach (var (code, name) in L.UiLanguages) _uiLang.Items.Add(new Item(code, () => name));
        _uiLang.SelectedIndex = Array.FindIndex(L.UiLanguages, x => x.Code == L.Current);

        foreach (var code in VideoLanguages) _lang.Items.Add(new Item(code, () => L.T("Lang." + code)));
        _lang.SelectedIndex = 0;

        foreach (var code in Models) _model.Items.Add(new Item(code, () => L.T("Model." + code)));
        _model.SelectedIndex = DefaultModelIndex;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
        for (var i = 0; i < 5; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // interface language, aligned to the right
        var uiRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false
        };
        uiRow.Controls.Add(_uiLang);
        uiRow.Controls.Add(_uiLangLabel);

        var urlRow = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        urlRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        urlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        urlRow.Controls.Add(_urlLabel, 0, 0);
        urlRow.Controls.Add(_url, 1, 0);

        layout.Controls.Add(uiRow, 0, 0);
        layout.Controls.Add(urlRow, 0, 1);
        layout.Controls.Add(Flow(_langLabel, _lang, _modelLabel, _model, _forceWhisper), 0, 2);
        layout.Controls.Add(Flow(_run, _cancel, _progress), 0, 3);
        layout.Controls.Add(_status, 0, 4);
        layout.Controls.Add(_output, 0, 5);
        layout.Controls.Add(Flow(_copy, _save), 0, 6);
        Controls.Add(layout);

        AcceptButton = _run;
        _run.Click += async (_, _) => await RunAsync();
        _cancel.Click += (_, _) => _cts?.Cancel();
        _copy.Click += (_, _) => { if (_output.TextLength > 0) Clipboard.SetText(_output.Text); };
        _save.Click += (_, _) => Save();
        _uiLang.SelectedIndexChanged += (_, _) => L.Set(((Item)_uiLang.SelectedItem!).Code);

        L.Changed += ApplyLanguage;
        FormClosed += (_, _) => L.Changed -= ApplyLanguage;
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        SuspendLayout();
        _uiLangLabel.Text = L.T("UiLanguage");
        _urlLabel.Text = L.T("Url");
        _langLabel.Text = L.T("VideoLanguage");
        _modelLabel.Text = L.T("Model");
        _forceWhisper.Text = L.T("ForceWhisper");
        _run.Text = L.T("Run");
        _cancel.Text = L.T("Cancel");
        _copy.Text = L.T("Copy");
        _save.Text = L.T("Save");
        if (_statusIsReady) _status.Text = L.T("Ready");

        RefreshItems(_lang);
        RefreshItems(_model);
        ResumeLayout(true);
    }

    /// <summary>Re-adds items so the combo box redraws them with the new display names.</summary>
    private static void RefreshItems(ComboBox combo)
    {
        var selected = combo.SelectedIndex;
        var items = combo.Items.Cast<object>().ToArray();
        combo.BeginUpdate();
        combo.Items.Clear();
        combo.Items.AddRange(items);
        combo.SelectedIndex = selected;
        combo.EndUpdate();
    }

    private async Task RunAsync()
    {
        if (string.IsNullOrWhiteSpace(_url.Text)) { _url.Focus(); return; }

        _cts = new CancellationTokenSource();
        SetBusy(true);
        _output.Clear();

        var progress = new Progress<ProgressInfo>(pi =>
        {
            SetStatus(pi.Status);
            if (pi.PartialText is not null)
            {
                _output.Text = pi.PartialText;
                _output.SelectionStart = _output.TextLength;
                _output.ScrollToCaret();
            }
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
            SetStatus(L.T("Done", result.Title, result.Source));
            _progress.Style = ProgressBarStyle.Blocks;
            _progress.Value = 100;
        }
        catch (OperationCanceledException) { SetStatus(L.T("Cancelled")); ResetProgress(); }
        catch (Exception ex) { SetStatus(L.T("Error", ex.Message)); ResetProgress(); }
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
            Filter = L.T("TxtFilter"),
            FileName = SafeFileName(_title) + ".txt",
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            File.WriteAllText(dlg.FileName, _output.Text, new UTF8Encoding(true));
            SetStatus(L.T("Saved", dlg.FileName));
        }
    }

    private void SetStatus(string text)
    {
        _statusIsReady = false;
        _status.Text = text;
    }

    private void SetBusy(bool busy)
    {
        _run.Enabled = _url.Enabled = _lang.Enabled = _model.Enabled = _forceWhisper.Enabled = _uiLang.Enabled = !busy;
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

    private static Label MakeLabel() => new() { AutoSize = true, Margin = new Padding(3, 7, 3, 3) };

    private static FlowLayoutPanel Flow(params Control[] controls)
    {
        var f = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        f.Controls.AddRange(controls);
        return f;
    }
}
