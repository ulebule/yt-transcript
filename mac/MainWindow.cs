using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace YtTranscript;

public sealed class MainWindow : Window
{
    /// <summary>Combo box item: a fixed code plus a display name that follows the UI language.</summary>
    private sealed record Item(string Code, Func<string> Name)
    {
        public override string ToString() => Name();
    }

    private static readonly string[] VideoLanguages = { "auto", "sl", "en", "hr", "sr", "bs", "fr", "de", "it" };
    private static readonly string[] Models = { "tiny", "base", "small", "medium", "large-v3-turbo" };
    private const int DefaultModelIndex = 2; // small

    private readonly TextBlock _uiLangLabel = MakeLabel();
    private readonly ComboBox _uiLang = new() { Width = 140 };
    private readonly TextBlock _urlLabel = MakeLabel();
    private readonly TextBox _url = new();
    private readonly Button _browse = new() { Margin = new Thickness(8, 0, 0, 0) };
    private readonly TextBlock _langLabel = MakeLabel();
    private readonly ComboBox _lang = new() { Width = 160 };
    private readonly TextBlock _modelLabel = MakeLabel();
    private readonly ComboBox _model = new() { Width = 250 };
    private readonly CheckBox _forceWhisper = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _run = new() { Classes = { "accent" } };
    private readonly Button _cancel = new() { IsEnabled = false };
    private readonly ProgressBar _progress = new() { Width = 260, Minimum = 0, Maximum = 100, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10) };
    private readonly TextBox _output = new()
    {
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        FontSize = 15,
    };
    private readonly Button _copy = new() { IsEnabled = false };
    private readonly Button _save = new() { IsEnabled = false };
    private readonly Button _saveSrt = new() { IsEnabled = false };

    private readonly List<Item> _langItems = VideoLanguages.Select(c => new Item(c, () => L.T("Lang." + c))).ToList();
    // downloaded models are marked with a check
    private readonly List<Item> _modelItems = Models
        .Select(c => new Item(c, () => (TranscriptService.IsModelDownloaded(c) ? "✓ " : "") + L.T("Model." + c)))
        .ToList();

    private readonly TranscriptService _service = new();
    private CancellationTokenSource? _cts;
    private string _title = "transcript";
    private IReadOnlyList<Segment> _segments = Array.Empty<Segment>();
    private bool _statusIsReady = true;

    public MainWindow()
    {
        Title = "YT Transcript";
        Width = 900;
        Height = 650;
        MinWidth = 640;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _uiLang.ItemsSource = L.UiLanguages.Select(x => new Item(x.Code, () => x.Name)).ToList();
        _uiLang.SelectedIndex = Array.FindIndex(L.UiLanguages, x => x.Code == L.Current);
        _lang.ItemsSource = _langItems.ToList();
        _lang.SelectedIndex = 0;
        _model.ItemsSource = _modelItems.ToList();
        _model.SelectedIndex = DefaultModelIndex;

        var uiRow = Row(_uiLangLabel, _uiLang);
        uiRow.HorizontalAlignment = HorizontalAlignment.Right;

        var urlRow = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(_urlLabel, Dock.Left);
        DockPanel.SetDock(_browse, Dock.Right);
        urlRow.Children.Add(_urlLabel);
        urlRow.Children.Add(_browse);
        urlRow.Children.Add(_url);

        var options = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        foreach (var c in new Control[] { _langLabel, _lang, _modelLabel, _model, _forceWhisper })
        {
            c.Margin = new Thickness(0, 0, 10, 4);
            options.Children.Add(c);
        }

        var actions = Row(_run, _cancel, _progress);
        actions.Margin = new Thickness(0, 8, 0, 0);

        var bottom = Row(_copy, _save, _saveSrt);
        bottom.Margin = new Thickness(0, 10, 0, 0);

        var root = new Grid
        {
            Margin = new Thickness(16),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,*,Auto"),
        };
        AddRow(root, uiRow, 0);
        AddRow(root, urlRow, 1);
        AddRow(root, options, 2);
        AddRow(root, actions, 3);
        AddRow(root, _status, 4);
        AddRow(root, _output, 5);
        AddRow(root, bottom, 6);
        Content = root;

        _run.Click += async (_, _) => await RunAsync();
        _cancel.Click += (_, _) => _cts?.Cancel();
        _copy.Click += async (_, _) =>
        {
            if (Clipboard is not null && !string.IsNullOrEmpty(_output.Text))
                await Clipboard.SetTextAsync(_output.Text);
        };
        _save.Click += async (_, _) => await SaveAsync(srt: false);
        _saveSrt.Click += async (_, _) => await SaveAsync(srt: true);
        _browse.Click += async (_, _) => await BrowseAsync();

        // drop an audio/video file onto the window
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = e.Data.Contains(DataFormats.Files) && _run.IsEnabled ? DragDropEffects.Copy : DragDropEffects.None;
        });
        AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var file = e.Data.GetFiles()?.FirstOrDefault();
            if (file is not null && file.Path.IsFile) _url.Text = file.Path.LocalPath;
        });
        _url.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter && _run.IsEnabled) await RunAsync();
        };
        _uiLang.SelectionChanged += (_, _) =>
        {
            if (_uiLang.SelectedItem is Item item) L.Set(item.Code);
        };

        L.Changed += ApplyLanguage;
        Closed += (_, _) => L.Changed -= ApplyLanguage;
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        _uiLangLabel.Text = L.T("UiLanguage");
        _urlLabel.Text = L.T("Url");
        _url.Watermark = L.T("UrlPlaceholder");
        _browse.Content = L.T("Browse");
        _saveSrt.Content = L.T("SaveSrt");
        _langLabel.Text = L.T("VideoLanguage");
        _modelLabel.Text = L.T("Model");
        _forceWhisper.Content = L.T("ForceWhisper");
        _run.Content = L.T("Run");
        _cancel.Content = L.T("Cancel");
        _copy.Content = L.T("Copy");
        _save.Content = L.T("Save");
        if (_statusIsReady) _status.Text = L.T("Ready");

        // new list instances make the combo boxes redraw the translated names
        RefreshItems(_lang, _langItems);
        RefreshItems(_model, _modelItems);
    }

    private static void RefreshItems(ComboBox combo, List<Item> items)
    {
        var selected = combo.SelectedIndex;
        combo.ItemsSource = items.ToList();
        combo.SelectedIndex = selected;
    }

    private async Task RunAsync()
    {
        if (string.IsNullOrWhiteSpace(_url.Text)) { _url.Focus(); return; }

        _cts = new CancellationTokenSource();
        _segments = Array.Empty<Segment>();
        _output.Text = "";
        SetBusy(true);

        var progress = new Progress<ProgressInfo>(pi =>
        {
            SetStatus(pi.Status);
            _progress.IsIndeterminate = pi.Percent is null;
            if (pi.Percent is double pct) _progress.Value = Math.Clamp(pct, 0, 100);
            if (pi.PartialText is not null)
            {
                _output.Text = pi.PartialText;
                _output.CaretIndex = pi.PartialText.Length;
            }
        });

        var options = new TranscriptOptions(
            _url.Text!,
            ((Item)_lang.SelectedItem!).Code,
            ((Item)_model.SelectedItem!).Code,
            _forceWhisper.IsChecked == true);

        try
        {
            var result = await Task.Run(() => _service.RunAsync(options, progress, _cts.Token));
            _title = result.Title;
            _segments = result.Segments;
            _output.Text = result.Text;
            SetStatus(L.T("Done", result.Title, result.Source));
            _progress.IsIndeterminate = false;
            _progress.Value = 100;
        }
        catch (OperationCanceledException) { SetStatus(L.T("Cancelled")); ResetProgress(); }
        catch (Exception ex) { SetStatus(L.T("Error", ex.Message)); ResetProgress(); }
        finally
        {
            SetBusy(false);
            _cts.Dispose();
            _cts = null;
            RefreshItems(_model, _modelItems); // the model may have just been downloaded
        }
    }

    private static readonly string[] MediaPatterns =
    {
        "*.mp3", "*.m4a", "*.aac", "*.wav", "*.flac", "*.ogg", "*.opus", "*.aiff", "*.aif", "*.caf",
        "*.mp4", "*.m4v", "*.mov", "*.mkv", "*.avi", "*.webm",
    };

    private async Task BrowseAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(L.T("MediaFilterName")) { Patterns = MediaPatterns },
                FilePickerFileTypes.All,
            },
        });
        var file = files.FirstOrDefault();
        if (file is not null && file.Path.IsFile) _url.Text = file.Path.LocalPath;
    }

    private async Task SaveAsync(bool srt)
    {
        var ext = srt ? "srt" : "txt";
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = SafeFileName(_title) + "." + ext,
            DefaultExtension = ext,
            FileTypeChoices = new[]
            {
                new FilePickerFileType(L.T(srt ? "SrtFilterName" : "TxtFilterName")) { Patterns = new[] { "*." + ext } },
            },
        });
        if (file is null) return;

        // SRT is built from the timed segments (UTF-8 with BOM for player compatibility);
        // the .txt keeps any edits made in the text box
        var content = srt ? Subtitles.ToSrt(_segments) : _output.Text ?? "";
        await using (var stream = await file.OpenWriteAsync())
        await using (var writer = new StreamWriter(stream, new UTF8Encoding(srt)))
            await writer.WriteAsync(content);

        SetStatus(L.T("Saved", file.Path.IsFile ? file.Path.LocalPath : file.Name));
    }

    private void SetStatus(string text)
    {
        _statusIsReady = false;
        _status.Text = text;
    }

    private void SetBusy(bool busy)
    {
        foreach (var c in new Control[] { _run, _url, _browse, _lang, _model, _forceWhisper, _uiLang })
            c.IsEnabled = !busy;
        _cancel.IsEnabled = busy;
        _copy.IsEnabled = _save.IsEnabled = !busy && !string.IsNullOrEmpty(_output.Text);
        _saveSrt.IsEnabled = !busy && _segments.Count > 0;
    }

    private void ResetProgress()
    {
        _progress.IsIndeterminate = false;
        _progress.Value = 0;
    }

    private static string SafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars().Append(':')) name = name.Replace(c, '_');
        return name.Length > 120 ? name[..120] : name;
    }

    private static TextBlock MakeLabel() =>
        new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };

    private static StackPanel Row(params Control[] controls)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.AddRange(controls);
        return panel;
    }

    private static void AddRow(Grid grid, Control control, int row)
    {
        Grid.SetRow(control, row);
        grid.Children.Add(control);
    }
}
