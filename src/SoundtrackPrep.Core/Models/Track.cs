namespace SoundtrackPrep.Core.Models;

/// <summary>
/// Represents a single audio track on a soundtrack.
/// </summary>
public class Track
{
    /// <summary>
    /// Track number within its disc (1-based).
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Title of the track.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional per-track artist. 
    /// Many soundtrack tracks leave this null and inherit the album artist instead.
    /// </summary>
    public string? Artist { get; set; }

    /// <summary>
    /// Length of the track, if known.
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Which disc this track belongs to (defaults to 1 for single-disc albums).
    /// Critical for multi-disc soundtracks so YouTube Music keeps them together.
    /// </summary>
    public int DiscNumber { get; set; } = 1;
}