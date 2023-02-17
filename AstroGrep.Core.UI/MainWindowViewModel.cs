﻿namespace AstroGrep.Core.UI;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using ReactiveUI;

public sealed class MainWindowViewModel : ReactiveObject
{
  private readonly MainWindow _parent;

  public MainWindowViewModel() :
    this(null)
  {
  }

  public MainWindowViewModel(MainWindow parent)
  {
    _parent = parent;
    _parent.MatchResultsSelectionChanged += OnMatchResultsSelectionChanged;
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

  private string _matchResult;

  public string MatchResult
  {
    get => _matchResult;

    set => this.RaiseAndSetIfChanged(ref _matchResult, value);
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
      FileFilter = FileType
    };

    var grep = new Grep(searchSpec, filterSpec);
    grep.Execute();
    MatchResults = grep.MatchResults;
  }

  public void OnMatchResultsSelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    var matchRes = e.AddedItems.OfType<MatchResult>().SingleOrDefault();
    MatchResult = Render(matchRes);
  }

  private string Render(MatchResult result)
  {
    var sb = new StringBuilder();
    foreach (var match in result.Matches)
    {
      var lineNum = match.LineNumber == -1 ? string.Empty : match.LineNumber.ToString(CultureInfo.InvariantCulture);
      sb.AppendLine($"{lineNum}  {match.Line}");
    }

    sb.AppendLine();

    return sb.ToString();
  }
}
