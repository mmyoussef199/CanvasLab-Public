using System.Windows;

namespace CanvasLab.Views;

public partial class HowToUseDialog : Window
{
    public HowToUseDialog() => InitializeComponent();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
