namespace SoundtrackPrep.App.ViewModels;

/// <summary>
/// Simple object that represents one row in the track list.
/// The ListView columns bind directly to these properties.
/// </summary>
public class TrackRow
{
    public bool IsSelected { get; set; }   // <-- new: used by the checkbox column

    public int Disc { get; set; }
    public string Track { get; set; } = string.Empty;   // e.g. "01"
    public string Title { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty; // e.g. "3:42"
    public string FullPath { get; set; } = string.Empty; // for tooltips / future use
    public string AlbumArtist { get; set; } = string.Empty; // for Album Artist column
}