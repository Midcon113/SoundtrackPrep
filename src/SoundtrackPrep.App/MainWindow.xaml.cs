using System.IO;
using System.Windows;
using Microsoft.Win32;   // for OpenFolderDialog in newer .NET

namespace SoundtrackPrep.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        StatusText.Text = "Ready";
    }

    public void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select a folder containing soundtrack files"
        };

        if (dialog.ShowDialog() == true)
        {
            string folderPath = dialog.FolderName;
            SetStatus($"Selected: {folderPath}");

            // Count audio files (flac, wav, mp3) including subfolders
            var audioExtensions = new[] { ".flac", ".wav", ".mp3" };
            int count = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                 .Count(f => audioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

            SetStatus($"Found {count} audio file(s) in {folderPath}");
        }
        else
        {
            SetStatus("Folder selection cancelled");
        }
    }
}