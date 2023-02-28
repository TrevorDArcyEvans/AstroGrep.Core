using System.Text.Json.Nodes;
using Newtonsoft.Json.Linq;

namespace AstroGrep.Core.UI;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.TextMate;
using ReactiveUI;
using TextMateSharp.Grammars;

public sealed class MainWindowViewModel : ReactiveObject
{
  private readonly MainWindow _parent;

  private readonly TextMate.Installation _textMateInstallation;
  private readonly TextEditor _textEditor;
  private RegistryOptions _registryOptions = new(ThemeName.Monokai);

  public List<FilterItem> FilterItems { get; set; }

  public MainWindowViewModel() :
    this(null)
  {
  }

  public MainWindowViewModel(MainWindow parent)
  {
    _parent = parent;

    LoadFilterItems();
    LoadSettings();

    _textEditor = _parent.FindControl<TextEditor>("Editor");
    _textEditor.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
    _textEditor.Background = Brushes.Transparent;
    _textEditor.TextArea.Background = _parent.Background;
    _textEditor.Options.ColumnRulerPosition = 80;

    _textMateInstallation = _textEditor.InstallTextMate(_registryOptions);
    _textMateInstallation.SetTheme(_registryOptions.LoadTheme(ThemeName.DimmedMonokai));

    PropertyChanged += OnPropertyChanged;
  }

  private void LoadSettings()
  {
    var settingsFilePath = GetCreateSettingsFilePath();
    if (!File.Exists(settingsFilePath))
    {
      return;
    }

    var json = File.ReadAllText(settingsFilePath);
    var jobj = JObject.Parse(json);
    StartFolder = jobj.Value<string>(nameof(StartFolder));
    SearchInSubfolders = jobj.Value<bool>(nameof(SearchInSubfolders));
    UseRegularExpressions = jobj.Value<bool>(nameof(UseRegularExpressions));
    UseCaseSensitivity = jobj.Value<bool>(nameof(UseCaseSensitivity));
    UseWholeWordMatching = jobj.Value<bool>(nameof(UseWholeWordMatching));
    UseNegation = jobj.Value<bool>(nameof(UseNegation));
    ContextLines = jobj.Value<int>(nameof(ContextLines));
    ReturnOnlyFileNames = jobj.Value<bool>(nameof(ReturnOnlyFileNames));
    FileType = jobj.Value<string>(nameof(FileType));
  }

  private void LoadFilterItems()
  {
    var assy = Assembly.GetExecutingAssembly();
    var assyLoc = assy.Location;
    var assyDir = Path.GetDirectoryName(assyLoc);
    var excFilePath = Path.Combine(assyDir, "exclusions.json");
    var json = File.ReadAllText(excFilePath);
    var opts = new JsonSerializerOptions
    {
      Converters =
      {
        new JsonStringEnumConverter()
      }
    };
    FilterItems = JsonSerializer.Deserialize<List<FilterItem>>(json, opts);
  }

  #region Properties

  private string _startFolder = Environment.CurrentDirectory;

  public string StartFolder
  {
    get => _startFolder;

    set => this.RaiseAndSetIfChanged(ref _startFolder, value);
  }

  private bool _searchInSubfolders = true;

  public bool SearchInSubfolders
  {
    get => _searchInSubfolders;

    set => this.RaiseAndSetIfChanged(ref _searchInSubfolders, value);
  }

  private bool _useRegularExpressions;

  public bool UseRegularExpressions
  {
    get => _useRegularExpressions;

    set => this.RaiseAndSetIfChanged(ref _useRegularExpressions, value);
  }

  private bool _useCaseSensitivity;

  public bool UseCaseSensitivity
  {
    get => _useCaseSensitivity;

    set => this.RaiseAndSetIfChanged(ref _useCaseSensitivity, value);
  }

  private bool _useWholeWordMatching;

  public bool UseWholeWordMatching
  {
    get => _useWholeWordMatching;

    set => this.RaiseAndSetIfChanged(ref _useWholeWordMatching, value);
  }

  private bool _useNegation;

  public bool UseNegation
  {
    get => _useNegation;

    set => this.RaiseAndSetIfChanged(ref _useNegation, value);
  }

  private int _contextLines = 4;

  public int ContextLines
  {
    get => _contextLines;

    set => this.RaiseAndSetIfChanged(ref _contextLines, value);
  }

  private bool _returnOnlyFileNames;

  public bool ReturnOnlyFileNames
  {
    get => _returnOnlyFileNames;

    set => this.RaiseAndSetIfChanged(ref _returnOnlyFileNames, value);
  }

  private string _searchText;

  public string SearchText
  {
    get => _searchText;

    set => this.RaiseAndSetIfChanged(ref _searchText, value);
  }

  private string _fileType = "*.cs";

  public string FileType
  {
    get => _fileType;

    set => this.RaiseAndSetIfChanged(ref _fileType, value);
  }

  private IEnumerable<MatchResult> _matchResults = Enumerable.Empty<MatchResult>();

  public IEnumerable<MatchResult> MatchResults
  {
    get => _matchResults;

    set => this.RaiseAndSetIfChanged(ref _matchResults, value);
  }

  private bool _isOnSearchEnabled;

  public bool IsOnSearchEnabled
  {
    get => _isOnSearchEnabled;

    set => this.RaiseAndSetIfChanged(ref _isOnSearchEnabled, value);
  }

  private MatchResult _selectedItem;

  public MatchResult SelectedItem
  {
    get => _selectedItem;

    set
    {
      this.RaiseAndSetIfChanged(ref _selectedItem, value);
      OnMatchResultsSelectionChanged(this, SelectedItem);
    }
  }

  #endregion

  public async Task OnSelectStartFolder()
  {
    var dlg = new OpenFolderDialog
    {
      Directory = StartFolder
    };
    StartFolder = await dlg.ShowAsync(_parent) ?? StartFolder;
  }

  public void OnSearch()
  {
    _textEditor.Document = new();

    var searchSpec = new SearchSpec
    {
      StartDirectories = new List<string> {StartFolder},
      SearchText = SearchText,
      UseRegularExpressions = UseRegularExpressions,
      UseCaseSensitivity = UseCaseSensitivity,
      UseWholeWordMatching = UseWholeWordMatching,
      SearchInSubfolders = SearchInSubfolders,
      ReturnOnlyFileNames = ReturnOnlyFileNames,
      UseNegation = UseNegation,
      ContextLines = ContextLines
    };

    var filterSpec = new FileFilterSpec
    {
      FileFilter = FileType,
      FilterItems = FilterItems
    };

    var grep = new Grep(searchSpec, filterSpec);
    grep.Execute();
    MatchResults = grep.MatchResults;
  }

  public void OnMatchResultsSelectionChanged(object sender, MatchResult matchRes)
  {
    if (matchRes is null)
    {
      return;
    }

    var extn = Path.GetExtension(matchRes.File.Name);
    var lang = _registryOptions.GetLanguageByExtension(extn);
    var scopeName = _registryOptions.GetScopeByLanguageId(lang.Id);

    _textMateInstallation.SetGrammar(null);
    _textEditor.Document = Render(matchRes);
    _textMateInstallation.SetGrammar(scopeName);
  }

  public void OnClosed(object? sender, EventArgs e)
  {
    SaveSettings();
  }

  private void SaveSettings()
  {
    var settings = new
    {
      StartFolder,
      SearchInSubfolders,
      UseRegularExpressions,
      UseCaseSensitivity,
      UseWholeWordMatching,
      UseNegation,
      ContextLines,
      ReturnOnlyFileNames,
      FileType
    };
    var json = JsonSerializer.Serialize(settings);

    var settingsFilePath = GetCreateSettingsFilePath();
    File.WriteAllText(settingsFilePath, json);
  }

  private static string GetCreateSettingsFilePath()
  {
    var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    var settingsDir = Path.Combine(appData, "AstroGrep");
    Directory.CreateDirectory(settingsDir);
    var settingsFilePath = Path.Combine(settingsDir, "settings.json");
    return settingsFilePath;
  }

  private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName is nameof(SearchText) or nameof(FileType) or nameof(StartFolder))
    {
      IsOnSearchEnabled =
        !string.IsNullOrEmpty(SearchText) &&
        !string.IsNullOrEmpty(FileType) &&
        Directory.Exists(StartFolder);
    }
  }

  private TextDocument Render(MatchResult result)
  {
    var sb = new StringBuilder();
    foreach (var matchLine in result.Matches)
    {
      var lineNum = matchLine.LineNumber == -1 ? "    " : $"{matchLine.LineNumber,4:####}";
      sb.AppendLine($"{lineNum}  {matchLine.Line}");
    }

    sb.AppendLine();

    return new TextDocument(sb.ToString());
  }
}
