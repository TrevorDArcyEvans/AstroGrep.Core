namespace AstroGrep.Core.UI;

using System;
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

  private string _selectedFolder = Environment.CurrentDirectory;

  public string SelectedFolder
  {
    get => _selectedFolder;

    set => this.RaiseAndSetIfChanged(ref _selectedFolder, value);
  }

  public async Task OnSelectFolder()
  {
    var dlg = new OpenFolderDialog();
    SelectedFolder = await dlg.ShowAsync(_parent) ?? SelectedFolder;

    Debug.WriteLine(SelectedFolder);
  }

  public void OnSearch()
  {
    // do something
    Debug.WriteLine(SelectedFolder);
  }
}
