using System.Net;
using System.Text;

namespace AstroGrep.Core.UI;

using System;
using System.Collections.Generic;
using System.Diagnostics;
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

  private string _dumpMatch;

  public string DumpMatch
  {
    get => _dumpMatch;

    set => this.RaiseAndSetIfChanged(ref _dumpMatch, value);
  }

  #endregion

  public async Task OnSelectStartFolder()
  {
    var dlg = new OpenFolderDialog
    {
      Directory = StartFolder
    };
    StartFolder = await dlg.ShowAsync(_parent) ?? StartFolder;

    Debug.WriteLine(StartFolder);
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
    var matchResults = grep.MatchResults;

    Dump(matchResults);
  }

  private void Dump(IEnumerable<MatchResult> results)
  {
    var sb = new StringBuilder();
    sb.AppendLine("--------------------");
    foreach (var result in results)
    {
      sb.AppendLine($"{result.File.Name}");
      foreach (var match in result.Matches)
      {
        sb.AppendLine($"  {match.Line}");
      }
    }

    DumpMatch = sb.ToString();
  }
}
