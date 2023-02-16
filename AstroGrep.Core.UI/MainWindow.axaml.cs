namespace AstroGrep.Core.UI;

using Avalonia.Controls;

public partial class MainWindow : Window
{
  public MainWindow()
  {
    InitializeComponent();

    DataContext = new MainWindowViewModel(this);
  }
}
