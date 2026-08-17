using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using SoundtrackPrep.Core.Models;
using SoundtrackPrep.Core.Services;

namespace SoundtrackPrep.App;

public partial class MainWindow : Window
{
    // Create one instance of the service for the whole window to use.
    private readonly AudioFileService _audioService = new AudioFileService();

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
    /// Opens a folder browser, reads tags from each audio file, and shows a summary in the list.
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

        FileList.Items.Clear();

        string[] audioExtensions = { ".flac", ".wav", ".mp3" };

        List<string> files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                      .Where(f => audioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                                      .OrderBy(f => f)
                                      .ToList();

        int successCount = 0;

        foreach (string file in files)
        {
            // Ask the service to read the tags
            Track? track = _audioService.ReadTrack(file);

            if (track == null)
            {
                // Could not read this file – show the filename so we still see it
                FileList.Items.Add($"[UNREADABLE] {Path.GetFileName(file)}");
                continue;
            }

            // Build a clear line that shows Disc + Track + Title
            // This will immediately reveal the duplicate track numbers you noticed earlier
            string display = $"Disc {track.DiscNumber} – Track {track.Number:D2}: {track.Title}";
            FileList.Items.Add(display);
            successCount++;
        }

        SetStatus($"Found {files.Count} files, successfully read {successCount}");
    }
}