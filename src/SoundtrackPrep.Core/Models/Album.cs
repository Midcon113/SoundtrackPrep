namespace SoundtrackPrep.Core.Models;

/// <summary>
/// Represents a complete soundtrack / album, which may contain one or more discs.
/// This is the main domain object the rest of the application will work with.
/// </summary>
public class Album
{
    /// <summary>
    /// Primary title of the album (e.g. "The Filmation Music of Ray Ellis").
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Main artist or composer (e.g. "Ray Ellis").
    /// Used as AlbumArtist for YouTube Music grouping.
    /// </summary>
    public string Artist { get; set; } = string.Empty;

    /// <summary>
    /// Release year, if known.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Catalog number, UPC, or other publisher identifier.
    /// Very useful for precise Discogs / MusicBrainz lookups.
    /// </summary>
    public string? CatalogNumber { get; set; }

    /// <summary>
    /// Optional subtitle or edition information 
    /// (e.g. "Expanded Edition", "Limited Edition", "Original Soundtrack").
    /// </summary>
    public string? Edition { get; set; }

    /// <summary>
    /// All tracks that belong to this album, across any number of discs.
    /// </summary>
    public List<Track> Tracks { get; set; } = new();

    /// <summary>
    /// Convenience property – how many distinct discs are present.
    /// </summary>
    public int DiscCount => Tracks.Select(t => t.DiscNumber).Distinct().Count();
}
