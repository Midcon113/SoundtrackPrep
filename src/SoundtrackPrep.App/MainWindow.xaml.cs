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

    // Remembers which column we last sorted by and the direction.
    // Allows us to reverse the sort when the same header is clicked again.
    private string _lastSortColumn = "";
    private bool _sortAscending = true;

    // Single shared instance of the service that reads and writes audio tags.
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
    /// Updates the status bar text.
    /// </summary>
    public void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    /// <summary>
    /// Updates the status bar with the current selection count
    /// and keeps the header checkbox in the correct state
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

        if (selectedCount == 0)
            HeaderCheckBox.IsChecked = false;
        else if (selectedCount == totalCount)
            HeaderCheckBox.IsChecked = true;
        else
            HeaderCheckBox.IsChecked = null; // indeterminate state
    }

    /// <summary>
    /// Returns the rows that should be affected by an Apply action.
    /// Priority:
    /// 1. All checked rows
    /// 2. If nothing is checked → the currently highlighted row
    /// </summary>
    private List<TrackRow> GetTargetRows()
    {
        if (FileList.ItemsSource is not IEnumerable<TrackRow> allRows)
            return new List<TrackRow>();

        // Prefer checked rows
        List<TrackRow> checkedRows = allRows.Where(r => r.IsSelected).ToList();
        if (checkedRows.Count > 0)
            return checkedRows;

        // Fall back to the highlighted row
        if (FileList.SelectedItem is TrackRow highlighted)
            return new List<TrackRow> { highlighted };

        return new List<TrackRow>();
    }

    // -------------------------------------------------------
    // Selection handlers
    // -------------------------------------------------------

    /// <summary>
    /// Header checkbox – selects or deselects every row.
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
    /// Individual row checkbox clicked.
    /// </summary>
    private void RowCheckBox_Click(object sender, RoutedEventArgs e)
    {
        UpdateSelectionStatus();
    }

    /// <summary>
    /// Select All button.
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
    /// Select None button.
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

    /// <summary>
    /// Called when the user clicks a row.
    /// Copies Disc, Track and Title into the text boxes for easy editing.
    /// </summary>
    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileList.SelectedItem is not TrackRow row)
            return;

        DiscNumberTextBox.Text = row.Disc.ToString();
        TrackNumberTextBox.Text = row.Track;
        TitleTextBox.Text = row.Title;
        AlbumArtistTextBox.Text = row.AlbumArtist;
    }

    // -------------------------------------------------------
    // Tag writing handlers
    // -------------------------------------------------------

    /// <summary>
    /// Flattens a multi-disc album into a single continuous disc.
    /// 
    /// What it does:
    /// 1. Asks the user for confirmation (because this permanently changes files)
    /// 2. Finds the highest track number on Disc 1
    /// 3. Takes every track from Disc 2, 3, etc.
    /// 4. Renumbers those tracks so they continue sequentially after Disc 1
    /// 5. Sets every track’s Disc Number to 1
    /// 6. Writes all changes to the actual audio files
    /// 7. Shows progress so the UI doesn’t appear frozen
    /// </summary>
    private async void FlattenToSingleDisc_Click(object sender, RoutedEventArgs e)
    {
        if (FileList.ItemsSource is not IEnumerable<TrackRow> allRows)
        {
            SetStatus("No tracks loaded");
            return;
        }

        List<TrackRow> rows = allRows.ToList();

        // Separate the tracks that are already on Disc 1 from the ones that need to be moved
        List<TrackRow> disc1Tracks = rows.Where(r => r.Disc == 1).OrderBy(r => r.Track).ToList();
        List<TrackRow> otherDiscTracks = rows.Where(r => r.Disc > 1)
                                            .OrderBy(r => r.Disc)
                                            .ThenBy(r => r.Track)
                                            .ToList();

        if (otherDiscTracks.Count == 0)
        {
            SetStatus("Nothing to flatten – everything is already on Disc 1");
            return;
        }

        // -------------------------------------------------------
        // Confirmation dialog – this is a destructive operation
        // -------------------------------------------------------
        MessageBoxResult confirm = MessageBox.Show(
            $"This will move {otherDiscTracks.Count} track(s) from higher discs onto Disc 1\n" +
            "and renumber them so they continue after the last track of Disc 1.\n\n" +
            "This permanently changes your audio files.\n\n" +
            "Do you want to continue?",
            "Flatten to Single Disc",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            SetStatus("Flatten cancelled");
            return;
        }

        // Disable the button so the user can’t click it twice
        // (We’ll re-enable it in the finally block)
        if (sender is Button btn)
            btn.IsEnabled = false;

        try
        {
            // Find the next track number to use (one higher than the current highest on Disc 1)
            int nextTrackNumber = 1;
            if (disc1Tracks.Count > 0)
            {
                nextTrackNumber = disc1Tracks.Max(r => int.Parse(r.Track)) + 1;
            }

            int successCount = 0;
            int failCount = 0;
            int totalToProcess = otherDiscTracks.Count;
            int current = 0;

            // Process the tracks that need to be moved
            foreach (TrackRow row in otherDiscTracks)
            {
                current++;
                SetStatus($"Flattening… {current} of {totalToProcess}");

                // Give the UI a chance to update the status text
                await Task.Delay(1);

                bool ok = true;

                // 1. Change Disc Number to 1
                if (_audioService.SetDiscNumber(row.FullPath, 1))
                {
                    row.Disc = 1;
                }
                else
                {
                    ok = false;
                }

                // 2. Assign the next sequential Track Number
                if (_audioService.SetTrackNumber(row.FullPath, nextTrackNumber))
                {
                    row.Track = nextTrackNumber.ToString("D2");
                }
                else
                {
                    ok = false;
                }

                if (ok)
                    successCount++;
                else
                    failCount++;

                nextTrackNumber++;
            }

            // Re-sort the list so everything appears in the new order
            FileList.ItemsSource = rows.OrderBy(r => r.Disc).ThenBy(r => r.Track).ToList();
            UpdateSelectionStatus();

            if (failCount == 0)
                SetStatus($"Flatten complete – {successCount} track(s) updated");
            else
                SetStatus($"Flatten finished with issues – {successCount} updated, {failCount} failed");
        }
        finally
        {
            // Always re-enable the button
            if (sender is Button btn2)
                btn2.IsEnabled = true;
        }
    }
    /// <summary>
    /// Applies the current values in the Disc, Track, and Title text boxes
    /// to the target rows (checked rows, or the highlighted row if none are checked)
    /// and writes the changes permanently into the audio files.
    /// </summary>
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        List<TrackRow> targets = GetTargetRows();
        if (targets.Count == 0)
        {
            SetStatus("No tracks selected or highlighted");
            return;
        }

        // Read the values from the text boxes
        bool hasDisc = int.TryParse(DiscNumberTextBox.Text, out int newDisc) && newDisc >= 1;
        bool hasTrack = int.TryParse(TrackNumberTextBox.Text, out int newTrack) && newTrack >= 1;
        string newTitle = TitleTextBox.Text.Trim();
        bool hasTitle = !string.IsNullOrWhiteSpace(newTitle);
        string newAlbumArtist = AlbumArtistTextBox.Text.Trim();
        bool hasAlbumArtist = !string.IsNullOrWhiteSpace(newAlbumArtist);

        if (!hasDisc && !hasTrack && !hasTitle && !hasAlbumArtist)
        {
            SetStatus("Nothing to apply – fill in at least one field");
            return;
        }

        int successCount = 0;
        int failCount = 0;

        foreach (TrackRow row in targets)
        {
            bool allOk = true;

            try
            {
                if (hasDisc)
                {
                    if (_audioService.SetDiscNumber(row.FullPath, newDisc))
                        row.Disc = newDisc;
                    else
                        allOk = false;
                }

                if (hasTrack)
                {
                    if (_audioService.SetTrackNumber(row.FullPath, newTrack))
                        row.Track = newTrack.ToString("D2");
                    else
                        allOk = false;
                }

                if (hasTitle)
                {
                    if (_audioService.SetTitle(row.FullPath, newTitle))
                        row.Title = newTitle;
                    else
                        allOk = false;
                }

                if (hasAlbumArtist)
                {
                    if (_audioService.SetAlbumArtist(row.FullPath, newAlbumArtist))
                        row.AlbumArtist = newAlbumArtist;
                    else
                        allOk = false;
                }
            }
            catch
            {
                allOk = false;
            }

            if (allOk)
                successCount++;
            else
                failCount++;
        }

        FileList.Items.Refresh();
        UpdateSelectionStatus();

        if (failCount == 0)
            SetStatus($"Successfully updated {successCount} track(s)");
        else
            SetStatus($"Updated {successCount} track(s), {failCount} failed");
    }

    // -------------------------------------------------------
    // Sorting
    // -------------------------------------------------------

    /// <summary>
    /// Handles clicks on column headers and sorts the list.
    /// </summary>
    private void Sort_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not GridViewColumnHeader header || header.Tag is not string columnName)
            return;

        if (FileList.ItemsSource is not IEnumerable<TrackRow> rows)
            return;

        if (columnName == _lastSortColumn)
            _sortAscending = !_sortAscending;
        else
        {
            _lastSortColumn = columnName;
            _sortAscending = true;
        }

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
    /// Scans for audio files on a background thread and displays them.
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

                    TrackRow row = new TrackRow { FullPath = file };

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
                        row.AlbumArtist = track.AlbumArtist ?? "";
                        row.Duration = track.Duration.HasValue
                            ? track.Duration.Value.ToString(@"m\:ss")
                            : "";
                    }

                    result.Add(row);
                }

                return result;
            });

            // Default sort by Track number when a folder is loaded
            FileList.ItemsSource = rows.OrderBy(r => r.Disc).ThenBy(r => r.Track).ToList();
            _lastSortColumn = "Track";
            _sortAscending = true;
            SetStatus($"Found {rows.Count} audio file(s)");
        }
        finally
        {
            SelectFolderButton.IsEnabled = true;
        }
    }
}