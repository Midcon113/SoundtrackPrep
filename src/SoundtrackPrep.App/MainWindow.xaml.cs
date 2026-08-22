using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SoundtrackPrep.App.ViewModels;
using SoundtrackPrep.Core.Models;
using SoundtrackPrep.Core.Services;
using ATL;

namespace SoundtrackPrep.App;

/// <summary>
/// The main window of the SoundtrackPrep application.
/// This is the primary UI the user interacts with.
/// It handles:
/// - Scanning folders for audio files
/// - Displaying and editing tags (Disc, Track, Title, Album Artist)
/// - Embedding and previewing cover art
/// - Flattening multi-disc albums into a single disc for YouTube Music
/// </summary>
public partial class MainWindow : Window
{
    // -------------------------------------------------------
    // Private fields
    // -------------------------------------------------------

    // Used by the column-header sorting logic so we can reverse direction
    // when the user clicks the same column twice.
    private string _lastSortColumn = "";
    private bool _sortAscending = true;

    // Single shared instance of the service that knows how to read and write
    // tags and cover art. Created once and reused for the life of the window.
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
    // Status bar helpers
    // -------------------------------------------------------

    /// <summary>
    /// Updates the text shown in the status bar at the bottom of the window.
    /// </summary>
    public void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    /// <summary>
    /// Recalculates how many tracks are currently checked and updates
    /// both the status bar and the state of the header checkbox
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

        // Keep the header checkbox visually in sync with reality
        if (selectedCount == 0)
            HeaderCheckBox.IsChecked = false;
        else if (selectedCount == totalCount)
            HeaderCheckBox.IsChecked = true;
        else
            HeaderCheckBox.IsChecked = null; // indeterminate (square) state
    }

    /// <summary>
    /// Determines which rows should be affected by an "Apply" or cover-art action.
    /// Rules:
    /// 1. If any rows are checked → use the checked rows
    /// 2. If nothing is checked → use the currently highlighted row
    /// 3. If nothing is highlighted either → return an empty list
    /// </summary>
    private List<TrackRow> GetTargetRows()
    {
        if (FileList.ItemsSource is not IEnumerable<TrackRow> allRows)
            return new List<TrackRow>();

        // Prefer explicitly checked rows
        List<TrackRow> checkedRows = allRows.Where(r => r.IsSelected).ToList();
        if (checkedRows.Count > 0)
            return checkedRows;

        // Fall back to the single highlighted row
        if (FileList.SelectedItem is TrackRow highlighted)
            return new List<TrackRow> { highlighted };

        return new List<TrackRow>();
    }

    // -------------------------------------------------------
    // Selection / checkbox handlers
    // -------------------------------------------------------

    /// <summary>
    /// Handles the checkbox that lives in the column header.
    /// Checks or unchecks every visible row at once.
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
    /// Called whenever an individual row checkbox is clicked.
    /// We only need to refresh the selection count.
    /// </summary>
    private void RowCheckBox_Click(object sender, RoutedEventArgs e)
    {
        UpdateSelectionStatus();
    }

    /// <summary>
    /// Select All button – checks every row.
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
    /// Select None button – unchecks every row.
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
    /// Fired when the user clicks a different row in the list.
    /// Responsibilities:
    /// 1. Fill the Disc / Track / Title / Album Artist text boxes
    /// 2. Attempt to load and display any embedded cover art
    /// </summary>
    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Always clear the previous image first
        CoverArtPreview.Source = null;

        if (FileList.SelectedItem is not TrackRow row)
            return;

        // Populate the editing fields from the selected row
        DiscNumberTextBox.Text = row.Disc.ToString();
        TrackNumberTextBox.Text = row.Track;
        TitleTextBox.Text = row.Title;
        AlbumArtistTextBox.Text = row.AlbumArtist;

        // Try to load the first embedded picture (if any)
        try
        {
            ATL.Track atlTrack = new ATL.Track(row.FullPath);

            if (atlTrack.EmbeddedPictures != null && atlTrack.EmbeddedPictures.Count > 0)
            {
                PictureInfo pic = atlTrack.EmbeddedPictures[0];

                using (MemoryStream ms = new MemoryStream(pic.PictureData))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();          // makes it cross-thread safe
                    CoverArtPreview.Source = bitmap;
                }
            }
        }
        catch
        {
            // No art present or failed to load – leave the preview empty
        }
    }

    // -------------------------------------------------------
    // Single "Apply" button – writes Disc / Track / Title / Album Artist
    // -------------------------------------------------------

    /// <summary>
    /// Reads the current values from the four text boxes and writes
    /// whatever fields are filled in to the target rows (checked or highlighted).
    /// </summary>
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        List<TrackRow> targets = GetTargetRows();
        if (targets.Count == 0)
        {
            SetStatus("No tracks selected or highlighted");
            return;
        }

        // Parse each field – only fields the user actually filled will be written
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
    // Cover Art
    // -------------------------------------------------------

    /// <summary>
    /// Lets the user pick a local image and embeds it as front cover
    /// on the currently targeted tracks (checked or highlighted).
    /// </summary>
    private void ChooseCoverArt_Click(object sender, RoutedEventArgs e)
    {
        List<TrackRow> targets = GetTargetRows();
        if (targets.Count == 0)
        {
            SetStatus("No tracks selected or highlighted");
            return;
        }

        // 1. Let the user pick the image first
        OpenFileDialog dialog = new OpenFileDialog
        {
            Title = "Select cover art image",
            Filter = "Image files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
            return;

        // 2. Now ask for confirmation
        MessageBoxResult confirm = MessageBox.Show(
            $"Apply this image as cover art to {targets.Count} track(s)?\n\nThis permanently changes the files.",
            "Apply Cover Art",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            SetStatus("Cover art cancelled");
            return;
        }

        // 3. Read the image and write it
        byte[] imageData;
        try
        {
            imageData = File.ReadAllBytes(dialog.FileName);
        }
        catch (Exception ex)
        {
            SetStatus($"Could not read image: {ex.Message}");
            return;
        }

        int successCount = 0;
        int failCount = 0;

        foreach (TrackRow row in targets)
        {
            try
            {
                ATL.Track atlTrack = new ATL.Track(row.FullPath);
                atlTrack.EmbeddedPictures.Clear();
                PictureInfo picInfo = PictureInfo.fromBinaryData(imageData, PictureInfo.PIC_TYPE.Front);
                atlTrack.EmbeddedPictures.Add(picInfo);

                if (atlTrack.Save())
                    successCount++;
                else
                    failCount++;
            }
            catch
            {
                failCount++;
            }
        }

        // Refresh preview
        if (FileList.SelectedItem is TrackRow)
            FileList_SelectionChanged(FileList, null!);

        if (failCount == 0)
            SetStatus($"Cover art successfully applied to {successCount} track(s)");
        else
            SetStatus($"Cover art applied to {successCount} track(s), {failCount} failed");

        // Force the preview to update for the currently selected track
        if (FileList.SelectedItem is TrackRow current)
        {
            FileList.SelectedItem = null;
            FileList.SelectedItem = current;
        }
    }

    /// <summary>
    /// Opens a Google Image search for high-quality soundtrack cover art
    /// based on the current Album Artist + Title.
    /// The user can then download a good image and use “Choose Image…”.
    /// </summary>
    private void SearchCoverArt_Click(object sender, RoutedEventArgs e)
    {
        string artist = AlbumArtistTextBox.Text.Trim();
        string title = TitleTextBox.Text.Trim();

        // Fall back to the highlighted row if the text boxes are empty
        if (string.IsNullOrWhiteSpace(artist) && string.IsNullOrWhiteSpace(title) &&
            FileList.SelectedItem is TrackRow row)
        {
            artist = row.AlbumArtist;
            title = row.Title;
        }

        if (string.IsNullOrWhiteSpace(artist) && string.IsNullOrWhiteSpace(title))
        {
            SetStatus("Need an Album Artist or Title to search");
            return;
        }

        string query = $"{artist} {title} soundtrack cover art".Trim();
        string url = "https://www.google.com/search?tbm=isch&q=" + Uri.EscapeDataString(query);

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            SetStatus("Opened image search in your browser");
        }
        catch
        {
            SetStatus("Could not open the browser");
        }
    }

    // -------------------------------------------------------
    // Flatten multi-disc album into a single disc
    // -------------------------------------------------------

    /// <summary>
    /// Takes every track that is currently on Disc 2, 3, etc.,
    /// renumbers them so they continue after the last track of Disc 1,
    /// and sets every track’s Disc Number to 1.
    /// This makes YouTube Music treat the set as one continuous album.
    /// </summary>
    private async void FlattenToSingleDisc_Click(object sender, RoutedEventArgs e)
    {
        if (FileList.ItemsSource is not IEnumerable<TrackRow> allRows)
        {
            SetStatus("No tracks loaded");
            return;
        }

        List<TrackRow> rows = allRows.ToList();
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

        // Strong confirmation because this permanently rewrites files
        MessageBoxResult confirm = MessageBox.Show(
            $"This will move {otherDiscTracks.Count} track(s) from higher discs onto Disc 1\n" +
            "and renumber them so they continue after the last track of Disc 1.\n\n" +
            "This permanently changes your audio files.\n\nContinue?",
            "Flatten to Single Disc",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            SetStatus("Flatten cancelled");
            return;
        }

        if (sender is Button btn)
            btn.IsEnabled = false;

        try
        {
            int nextTrackNumber = disc1Tracks.Count > 0
                ? disc1Tracks.Max(r => int.Parse(r.Track)) + 1
                : 1;

            int successCount = 0;
            int failCount = 0;
            int total = otherDiscTracks.Count;
            int current = 0;

            foreach (TrackRow row in otherDiscTracks)
            {
                current++;
                SetStatus($"Flattening… {current} of {total}");
                await Task.Delay(1); // lets the UI update the status text

                bool ok = true;

                if (_audioService.SetDiscNumber(row.FullPath, 1))
                    row.Disc = 1;
                else
                    ok = false;

                if (_audioService.SetTrackNumber(row.FullPath, nextTrackNumber))
                    row.Track = nextTrackNumber.ToString("D2");
                else
                    ok = false;

                if (ok) successCount++;
                else failCount++;

                nextTrackNumber++;
            }

            // Re-sort so the new order is visible immediately
            FileList.ItemsSource = rows.OrderBy(r => r.Disc).ThenBy(r => r.Track).ToList();
            UpdateSelectionStatus();

            if (failCount == 0)
                SetStatus($"Flatten complete – {successCount} track(s) updated");
            else
                SetStatus($"Flatten finished with issues – {successCount} updated, {failCount} failed");
        }
        finally
        {
            if (sender is Button btn2)
                btn2.IsEnabled = true;
        }
    }

    // -------------------------------------------------------
    // Column sorting
    // -------------------------------------------------------

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
    /// Opens a folder browser, finds all supported audio files,
    /// reads their tags on a background thread, and displays them
    /// sorted by Disc then Track.
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
                    SoundtrackPrep.Core.Models.Track? track = _audioService.ReadTrack(file);

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

            // Default sort order: Disc number, then Track number
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