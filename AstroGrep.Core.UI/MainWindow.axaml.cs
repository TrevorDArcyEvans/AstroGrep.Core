namespace AstroGrep.Core.UI;

using System;
using Avalonia.Controls;

public partial class MainWindow : Window
{
  public MainWindow()
  {
    InitializeComponent();

    DataContext = new MainWindowViewModel(this);
  }

  private void OnClosed(object? sender, EventArgs e)
  {
    var vm = (MainWindowViewModel) DataContext;
    vm.OnClosed(sender, e);
  }
}
