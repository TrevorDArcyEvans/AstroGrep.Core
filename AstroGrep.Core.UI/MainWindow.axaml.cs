namespace AstroGrep.Core.UI;

using System;
using Avalonia.Controls;

public partial class MainWindow : Window
{
  public event EventHandler<SelectionChangedEventArgs> MatchResultsSelectionChanged;
  
  public MainWindow()
  {
    InitializeComponent();

    DataContext = new MainWindowViewModel(this);
  }

  public void OnMatchResultsSelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    // echo to parent
    MatchResultsSelectionChanged?.Invoke(sender, e);
  }
}
