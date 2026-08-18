using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using SoundtrackPrep.App.ViewModels;
using SoundtrackPrep.Core.Models;
using SoundtrackPrep.Core.Services;
using System.Collections;

namespace SoundtrackPrep.App;

public partial class MainWindow : Window
{
    // One shared instance of the service that knows how to read audio tags
    private readonly AudioFileService _audioService = new AudioFileService();

    public MainWindow()
    {
        InitializeComponent();
        StatusText.Text = "Ready";
    }

    /// <summary>
    /// Simple helper so any part of this window can update the status bar.
    /// </summary>
    public void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    /// <summary>
    /// Handles the checkbox in the column header.
    /// Checks or unchecks every row at once.
    /// </summary>
    private void HeaderCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (FileList.ItemsSource is not IEnumerable<TrackRow> rows)
            return;

        bool isChecked = HeaderCheckBox.IsChecked == true;

        foreach (TrackRow row in rows)
        {
            row.IsSelected = isChecked;
        }

        // Force the ListView to refresh the checkboxes
        FileList.Items.Refresh();

        // Update
        UpdateSelectionStatus();
    }

    /// <summary>
    /// Selects every track in the list.
    /// </summary>
    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (FileList.ItemsSource is not IEnumerable<TrackRow> rows)
            return;

        foreach (TrackRow row in rows)
        {
            row.IsSelected = true;
        }

        // Tell the ListView to redraw the checkboxes
        FileList.Items.Refresh();
        UpdateSelectionStatus();
    }

    /// <summary>
    /// Clears the selection on every track.
    /// </summary>
    private void SelectNoneButton_Click(object sender, RoutedEventArgs e)
    {
        if (FileList.ItemsSource is not IEnumerable<TrackRow> rows)
            return;

        foreach (TrackRow row in rows)
        {
            row.IsSelected = false;
        }

        FileList.Items.Refresh();
        UpdateSelectionStatus();
    }

    /// <summary>
    /// Called when any individual row checkbox is clicked.
    /// Simply refreshes the selection count in the status bar.
    /// </summary>
    private void RowCheckBox_Click(object sender, RoutedEventArgs e)
    {
        UpdateSelectionStatus();
    }

    /// <summary>
    /// Updates the status bar to show how many tracks are selected.
    /// </summary>
    private void UpdateSelectionStatus()
    {
        if (FileList.ItemsSource is not IEnumerable<TrackRow> rows)
        {
            SetStatus("Ready");
            return;
        }

        int selectedCount = rows.Count(r => r.IsSelected);
        int totalCount = rows.Count();

        SetStatus($"{selectedCount} of {totalCount} tracks selected");
    }

    /// <summary>
    /// Handles the Select Folder button.
    /// Runs the file scanning on a background thread so the UI stays responsive,
    /// then binds the results to the ListView.
    /// </summary>
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

        // Clear any previous results
        FileList.ItemsSource = null;
        SelectFolderButton.IsEnabled = false;

        try
        {
            // -------------------------------------------------------
            // Heavy work happens here on a background thread
            // -------------------------------------------------------
            List<TrackRow> rows = await Task.Run(() =>
            {
                string[] audioExtensions = { ".flac", ".wav", ".mp3" };

                // Find every audio file under the chosen folder
                List<string> files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                              .Where(f => audioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                                              .OrderBy(f => f)
                                              .ToList();

                List<TrackRow> result = new List<TrackRow>();

                foreach (string file in files)
                {
                    // Ask our service to read the embedded tags
                    Track? track = _audioService.ReadTrack(file);

                    TrackRow row = new TrackRow
                    {
                        FullPath = file          // kept for tooltips / future use
                    };

                    if (track == null)
                    {
                        // File could not be read – still show something useful
                        row.Disc = 0;
                        row.Track = "--";
                        row.Title = $"[UNREADABLE] {Path.GetFileName(file)}";
                        row.Duration = "";
                    }
                    else
                    {
                        // Map the domain Track into a simple row the ListView understands
                        row.Disc = track.DiscNumber;
                        row.Track = track.Number.ToString("D2");   // 01, 02, 03…
                        row.Title = track.Title;
                        row.Duration = track.Duration.HasValue
                            ? track.Duration.Value.ToString(@"m\:ss")
                            : "";
                    }

                    result.Add(row);
                }

                return result;
            });

            // -------------------------------------------------------
            // Back on the UI thread – safe to update the ListView
            // -------------------------------------------------------
            FileList.ItemsSource = rows;
            SetStatus($"Found {rows.Count} audio file(s)");
        }
        finally
        {
            // Always re-enable the button, success or failure
            SelectFolderButton.IsEnabled = true;
        }
    }
}