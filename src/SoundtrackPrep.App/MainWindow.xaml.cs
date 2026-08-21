using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SoundtrackPrep.App.ViewModels;
using SoundtrackPrep.Core.Models;
using SoundtrackPrep.Core.Services;

namespace SoundtrackPrep.App;

/// <summary>
/// The main window of SoundtrackPrep.
/// This is where the user interacts with the application.
/// </summary>
public partial class MainWindow : Window
{
    // -------------------------------------------------------
    // Fields
    // -------------------------------------------------------

    // Remembers which column we last sorted by and whether it was ascending or descending.
    // This lets us reverse the sort when the user clicks the same header again.
    private string _lastSortColumn = "";
    private bool _sortAscending = true;

    // One shared instance of the service that knows how to read and write audio tags.
    // We create it once and reuse it for the lifetime of the window.
    private readonly AudioFileService _audioService = new AudioFileService();

    // -------------------------------------------------------
    // Constructor
    // -------------------------------------------------------

    public MainWindow()
    {
        InitializeComponent();
        StatusText.Text = "Ready";
    }

    // -------------------------------------------------------
    // Status helpers
    // -------------------------------------------------------

    /// <summary>
    /// Simple helper so any part of this window can update the status bar.
    /// </summary>
    public void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    /// <summary>
    /// Updates the status bar with how many tracks are selected
    /// and keeps the header checkbox in the correct visual state
    /// (checked / unchecked / indeterminate).
    /// </summary>
    private void UpdateSelectionStatus()
    {
        if (FileList.ItemsSource is not IEnumerable<TrackRow> rows)
        {
            SetStatus("Ready");
            HeaderCheckBox.IsChecked = false;
            return;
        }

        int selectedCount = rows.Count(r => r.IsSelected);
        int totalCount = rows.Count();

        SetStatus($"{selectedCount} of {totalCount} tracks selected");

        // Keep the header checkbox honest
        if (selectedCount == 0)
            HeaderCheckBox.IsChecked = false;
        else if (selectedCount == totalCount)
            HeaderCheckBox.IsChecked = true;
        else
            HeaderCheckBox.IsChecked = null; // indeterminate (square) state
    }

    // -------------------------------------------------------
    // Selection handlers
    // -------------------------------------------------------

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
            row.IsSelected = isChecked;

        FileList.Items.Refresh();
        UpdateSelectionStatus();
    }

    /// <summary>
    /// Called when any individual row checkbox is clicked.
    /// We only need to refresh the selection count.
    /// </summary>
    private void RowCheckBox_Click(object sender, RoutedEventArgs e)
    {
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
            row.IsSelected = true;

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
            row.IsSelected = false;

        FileList.Items.Refresh();
        UpdateSelectionStatus();
    }

    // -------------------------------------------------------
    // Tag writing handlers (the real power of the tool)
    // -------------------------------------------------------

    /// <summary>
    /// Applies the disc number from the text box to every currently selected track
    /// and writes the change permanently into the audio files on disk.
    /// </summary>
    private void ApplyDiscNumber_Click(object sender, RoutedEventArgs e)
    {
        // Validate the number the user typed
        if (!int.TryParse(DiscNumberTextBox.Text, out int newDiscNumber) || newDiscNumber < 1)
        {
            SetStatus("Please enter a valid disc number (1 or higher)");
            return;
        }

        if (FileList.ItemsSource is not IEnumerable<TrackRow> rows)
            return;

        int successCount = 0;
        int failCount = 0;

        foreach (TrackRow row in rows)
        {
            if (!row.IsSelected)
                continue;

            // Ask the service to write the new disc number into the real file
            bool written = _audioService.SetDiscNumber(row.FullPath, newDiscNumber);

            if (written)
            {
                // Update the value the user sees on screen immediately
                row.Disc = newDiscNumber;
                successCount++;
            }
            else
            {
                failCount++;
            }
        }

        FileList.Items.Refresh();
        UpdateSelectionStatus();

        if (failCount == 0)
            SetStatus($"Successfully updated disc number on {successCount} track(s)");
        else
            SetStatus($"Updated {successCount} track(s), {failCount} failed");
    }

    /// <summary>
    /// Applies the track number from the text box to every currently selected track
    /// and writes the change permanently into the audio files on disk.
    /// </summary>
    private void ApplyTrackNumber_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TrackNumberTextBox.Text, out int newTrackNumber) || newTrackNumber < 1)
        {
            SetStatus("Please enter a valid track number (1 or higher)");
            return;
        }

        if (FileList.ItemsSource is not IEnumerable<TrackRow> rows)
            return;

        int successCount = 0;
        int failCount = 0;

        foreach (TrackRow row in rows)
        {
            if (!row.IsSelected)
                continue;

            bool written = _audioService.SetTrackNumber(row.FullPath, newTrackNumber);

            if (written)
            {
                row.Track = newTrackNumber.ToString("D2");
                successCount++;
            }
            else
            {
                failCount++;
            }
        }

        FileList.Items.Refresh();
        UpdateSelectionStatus();

        if (failCount == 0)
            SetStatus($"Successfully updated track number on {successCount} track(s)");
        else
            SetStatus($"Updated {successCount} track(s), {failCount} failed");
    }

    /// <summary>
    /// Called when the user clicks a row in the list.
    /// Copies that row’s Disc, Track, and Title into the text boxes
    /// so they are ready to edit.
    /// </summary>
    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileList.SelectedItem is not TrackRow row)
            return;

        DiscNumberTextBox.Text = row.Disc.ToString();
        TrackNumberTextBox.Text = row.Track;
        TitleTextBox.Text = row.Title;
    }

    /// <summary>
    /// Applies the title from the text box to every currently selected track
    /// and writes the change permanently into the audio files.
    /// </summary>
    private void ApplyTitle_Click(object sender, RoutedEventArgs e)
    {
        string newTitle = TitleTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(newTitle))
        {
            SetStatus("Please enter a title");
            return;
        }

        if (FileList.ItemsSource is not IEnumerable<TrackRow> rows)
        {
            SetStatus("No tracks loaded");
            return;
        }

        int successCount = 0;
        int failCount = 0;

        foreach (TrackRow row in rows)
        {
            if (!row.IsSelected)
                continue;

            try
            {
                bool written = _audioService.SetTitle(row.FullPath, newTitle);

                if (written)
                {
                    row.Title = newTitle;
                    successCount++;
                }
                else
                {
                    failCount++;
                }
            }
            catch
            {
                failCount++;
            }
        }

        FileList.Items.Refresh();
        UpdateSelectionStatus();

        if (failCount == 0)
            SetStatus($"Successfully updated title on {successCount} track(s)");
        else
            SetStatus($"Updated {successCount} track(s), {failCount} failed");
    }
    // -------------------------------------------------------
    // Sorting
    // -------------------------------------------------------

    /// <summary>
    /// Called when the user clicks any column header.
    /// Sorts the list by that column. Clicking the same column again reverses the direction.
    /// </summary>
    private void Sort_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not GridViewColumnHeader header || header.Tag is not string columnName)
            return;

        if (FileList.ItemsSource is not IEnumerable<TrackRow> rows)
            return;

        // Toggle direction if the user clicked the same column again
        if (columnName == _lastSortColumn)
            _sortAscending = !_sortAscending;
        else
        {
            _lastSortColumn = columnName;
            _sortAscending = true;
        }

        // Perform the sort
        List<TrackRow> sorted = columnName switch
        {
            "Disc" => _sortAscending ? rows.OrderBy(r => r.Disc).ToList() : rows.OrderByDescending(r => r.Disc).ToList(),
            "Track" => _sortAscending ? rows.OrderBy(r => r.Track).ToList() : rows.OrderByDescending(r => r.Track).ToList(),
            "Title" => _sortAscending ? rows.OrderBy(r => r.Title).ToList() : rows.OrderByDescending(r => r.Title).ToList(),
            "Duration" => _sortAscending ? rows.OrderBy(r => r.Duration).ToList() : rows.OrderByDescending(r => r.Duration).ToList(),
            _ => rows.ToList()
        };

        FileList.ItemsSource = sorted;
        UpdateSelectionStatus();
    }

    // -------------------------------------------------------
    // Folder scanning
    // -------------------------------------------------------

    /// <summary>
    /// Handles the Select Folder button.
    /// Runs the heavy file scanning and tag reading on a background thread
    /// so the UI stays responsive, then shows the results in the ListView.
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
        FileList.ItemsSource = null;
        SelectFolderButton.IsEnabled = false;

        try
        {
            // Heavy work runs on a background thread
            List<TrackRow> rows = await Task.Run(() =>
            {
                string[] audioExtensions = { ".flac", ".wav", ".mp3" };

                List<string> files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                              .Where(f => audioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                                              .OrderBy(f => f)
                                              .ToList();

                List<TrackRow> result = new List<TrackRow>();

                foreach (string file in files)
                {
                    Track? track = _audioService.ReadTrack(file);

                    TrackRow row = new TrackRow
                    {
                        FullPath = file
                    };

                    if (track == null)
                    {
                        row.Disc = 0;
                        row.Track = "--";
                        row.Title = $"[UNREADABLE] {Path.GetFileName(file)}";
                        row.Duration = "";
                    }
                    else
                    {
                        row.Disc = track.DiscNumber;
                        row.Track = track.Number.ToString("D2");
                        row.Title = track.Title;
                        row.Duration = track.Duration.HasValue
                            ? track.Duration.Value.ToString(@"m\:ss")
                            : "";
                    }

                    result.Add(row);
                }

                return result;
            });

            // Back on the UI thread – safe to update the ListView
            FileList.ItemsSource = rows;
            SetStatus($"Found {rows.Count} audio file(s)");
        }
        finally
        {
            // Always re-enable the button
            SelectFolderButton.IsEnabled = true;
        }
    }
}