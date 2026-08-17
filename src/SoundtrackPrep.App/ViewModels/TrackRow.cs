namespace SoundtrackPrep.App.ViewModels;

/// <summary>
/// Simple object that represents one row in the track list.
/// The ListView columns bind directly to these properties.
/// Later this can grow or be replaced when we move to a full DataGrid.
/// </summary>
public class TrackRow
{
    public int Disc { get; set; }
    public string Track { get; set; } = string.Empty;   // e.g. "01"
    public string Title { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty; // e.g. "3:42"
    public string FullPath { get; set; } = string.Empty; // for the tooltip later
}