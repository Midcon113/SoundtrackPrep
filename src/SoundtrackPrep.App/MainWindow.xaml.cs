using System.Windows;

namespace SoundtrackPrep.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        StatusText.Text = "Ready";
    }

    // Simple helper we can call later from anywhere in this window
    public void SetStatus(string message)
    {
        StatusText.Text = message;
    }
}