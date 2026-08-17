using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SoundtrackPrep.Core.Models;
using SoundtrackPrep.Core.Services;

namespace SoundtrackPrep.App;

public partial class MainWindow : Window
{
    private readonly AudioFileService _audioService = new AudioFileService();

    public MainWindow()
    {
        InitializeComponent();
        StatusText.Text = "Ready";
    }

    public void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private async void SelectFolderButton_Click(object sender, RoutedEventArgs e)
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
        SetStatus($"Scanning: {folderPath} …");
        FileList.Items.Clear();
        SelectFolderButton.IsEnabled = false;

        try
        {
            // 1. Heavy work on background thread – only data, no UI objects
            var results = await Task.Run(() =>
            {
                string[] audioExtensions = { ".flac", ".wav", ".mp3" };

                List<string> files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                              .Where(f => audioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                                              .OrderBy(f => f)
                                              .ToList();

                // Simple data structure we can safely pass back to the UI thread
                List<(string DisplayText, string FullPath)> data = new();

                foreach (string file in files)
                {
                    Track? track = _audioService.ReadTrack(file);

                    string displayText = track == null
                        ? $"[UNREADABLE] {Path.GetFileName(file)}"
                        : $"Disc {track.DiscNumber} – Track {track.Number:D2}: {track.Title}";

                    data.Add((displayText, file));
                }

                return data;
            });

            // 2. Back on the UI thread – now it is safe to create ListBoxItems
            foreach (var (displayText, fullPath) in results)
            {
                ListBoxItem item = new ListBoxItem
                {
                    Content = displayText,
                    ToolTip = fullPath
                };
                FileList.Items.Add(item);
            }

            SetStatus($"Found {results.Count} audio file(s)");
        }
        finally
        {
            SelectFolderButton.IsEnabled = true;
        }
    }
}