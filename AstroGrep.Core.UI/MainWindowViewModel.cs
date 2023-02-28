namespace AstroGrep.Core.UI;

using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.TextMate;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextMateSharp.Grammars;

public sealed class MainWindowViewModel : ReactiveObject
{
  private readonly MainWindow _parent;

  private readonly TextMate.Installation _textMateInstallation;
  private readonly TextEditor _textEditor;
  private RegistryOptions _registryOptions = new RegistryOptions(ThemeName.Monokai);

  public List<FilterItem> FilterItems { get; } = new List<FilterItem>
  {
    new FilterItem(FilterType.FromString("File^Extension"), ".exe", FilterType.ValueOptions.None, true),
    new FilterItem(FilterType.FromString("File^Extension"), ".dll", FilterType.ValueOptions.None, true),
    new FilterItem(FilterType.FromString("File^Extension"), ".obj", FilterType.ValueOptions.None, true),
    new FilterItem(FilterType.FromString("File^Extension"), ".pdb", FilterType.ValueOptions.None, true),
    new FilterItem(FilterType.FromString("File^Extension"), ".msi", FilterType.ValueOptions.None, true),
    new FilterItem(FilterType.FromString("File^Extension"), ".sys", FilterType.ValueOptions.None, true),
    new FilterItem(FilterType.FromString("File^Extension"), ".ppt", FilterType.ValueOptions.None, true),
    new FilterItem(FilterType.FromString("File^Extension"), ".gif", FilterType.ValueOptions.None, true),
    new FilterItem(FilterType.FromString("File^Extension"), ".jpg", FilterType.ValueOptions.None, true),
    new FilterItem(FilterType.FromString("File^Extension"), ".jpeg", FilterType.ValueOptions.None, true),
    new FilterItem(FilterType.FromString("File^Extension"), ".png", FilterType.ValueOptions.None, true),
    new FilterItem(FilterType.FromString("File^Extension"), ".bmp", FilterType.ValueOptions.None, true),
    new FilterItem(FilterType.FromString("File^Extension"), ".class", FilterType.ValueOptions.None, true),
    new FilterItem(FilterType.FromString("File^Extension"), ".chm", FilterType.ValueOptions.None, true),
    new FilterItem(FilterType.FromString("File^Extension"), ".mdf", FilterType.ValueOptions.None, true),
    new FilterItem(FilterType.FromString("File^Extension"), ".ldf", FilterType.ValueOptions.None, true),
    new FilterItem(FilterType.FromString("File^Extension"), ".ndf", FilterType.ValueOptions.None, true),
    new FilterItem(FilterType.FromString("Directory^Name"), ".git", FilterType.ValueOptions.Equals, true),
    new FilterItem(FilterType.FromString("Directory^Name"), ".hg", FilterType.ValueOptions.Equals, true),
    new FilterItem(FilterType.FromString("Directory^Name"), ".svn", FilterType.ValueOptions.Equals, true),
    new FilterItem(FilterType.FromString("Directory^Name"), ".cvs", FilterType.ValueOptions.Equals, true),
    new FilterItem(FilterType.FromString("Directory^Name"), ".metadata", FilterType.ValueOptions.Equals, true),
    new FilterItem(FilterType.FromString("Directory^Name"), ".settings", FilterType.ValueOptions.Equals, true),
    new FilterItem(FilterType.FromString("Directory^Name"), ".vscode", FilterType.ValueOptions.Equals, true),
    new FilterItem(FilterType.FromString("Directory^Name"), ".idea", FilterType.ValueOptions.Equals, true),
    new FilterItem(FilterType.FromString("Directory^Name"), ".cache", FilterType.ValueOptions.Equals, true),
    new FilterItem(FilterType.FromString("Directory^Name"), "node_modules", FilterType.ValueOptions.Equals, true)
  };

  public MainWindowViewModel() :
    this(null)
  {
  }

  public MainWindowViewModel(MainWindow parent)
  {
    _parent = parent;

    _textEditor = _parent.FindControl<TextEditor>("Editor");
    _textEditor.HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Visible;
    _textEditor.Background = Brushes.Transparent;
    _textEditor.TextArea.Background = _parent.Background;
    _textEditor.Options.ColumnRulerPosition = 80;

    _textMateInstallation = _textEditor.InstallTextMate(_registryOptions);
    _textMateInstallation.SetTheme(_registryOptions.LoadTheme(ThemeName.DimmedMonokai));

    PropertyChanged += OnPropertyChanged;
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
    _textEditor.Document = new ();

    var searchSpec = new SearchSpec
    {
      StartDirectories = new List<string> { StartFolder },
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
      var lineNum = matchLine.LineNumber == -1 ? "    " :$"{ matchLine.LineNumber, 4:####}";
      sb.AppendLine($"{lineNum}  {matchLine.Line}");
    }

    sb.AppendLine();

    return new TextDocument(sb.ToString());
  }
}
