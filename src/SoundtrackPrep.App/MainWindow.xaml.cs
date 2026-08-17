using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace SoundtrackPrep.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        StatusText.Text = "Ready";
    }

    /// <summary>
    /// Updates the status bar text.
    /// </summary>
    public void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    /// <summary>
    /// Opens a folder browser, finds audio files, and displays them in the list.
    /// </summary>
    private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFolderDialog dialog = new OpenFolderDialog
        {
            Title = "Select a folder containing soundtrack files"
        };

        if (dialog.ShowDialog() != true)
        {
            SetStatus("Folder selection cancelled");
            return;
        }

        string folderPath = dialog.FolderName;
        SetStatus($"Scanning: {folderPath}");

        // Clear any previous results
        FileList.Items.Clear();

        // Extensions we care about for soundtracks
        string[] audioExtensions = { ".flac", ".wav", ".mp3" };

        // Find all matching files (including subfolders)
        List<string> files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                      .Where(f => audioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                                      .OrderBy(f => f)
                                      .ToList();

        // Add each file to the ListBox (show just the file name for now)
        foreach (string file in files)
        {
            FileList.Items.Add(Path.GetFileName(file));
        }

        SetStatus($"Found {files.Count} audio file(s)");
    }
}