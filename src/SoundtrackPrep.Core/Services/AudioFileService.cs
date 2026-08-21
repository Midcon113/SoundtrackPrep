using ATL;
using SoundtrackPrep.Core.Models;

namespace SoundtrackPrep.Core.Services;

/// <summary>
/// Responsible for reading (and later writing) audio file tags.
/// This keeps all the "talk to the file" logic in one place so the rest of the app stays clean.
/// </summary>
public class AudioFileService
{
    /// <summary>
    /// Reads the basic tags from a single audio file and returns a Track object.
    /// Returns null if the file cannot be read.
    /// </summary>
    public Models.Track? ReadTrack(string filePath)
    {
        try
        {
            // ATL.Track is the library's own class that opens the file and reads its tags.
            // We immediately map the useful bits into *our* Track domain model.
            ATL.Track atlTrack = new ATL.Track(filePath);

            Models.Track track = new Models.Track
            {
                // Use the null-coalescing operator (??) to supply a default when ATL returns null
                Number = atlTrack.TrackNumber ?? 0,
                Title = atlTrack.Title ?? string.Empty,
                Artist = string.IsNullOrWhiteSpace(atlTrack.Artist) ? null : atlTrack.Artist,
                DiscNumber = (atlTrack.DiscNumber == null || atlTrack.DiscNumber == 0) ? 1 : atlTrack.DiscNumber.Value,
                Duration = atlTrack.DurationMs > 0
                    ? TimeSpan.FromMilliseconds(atlTrack.DurationMs)
                    : null
            };

            return track;
        }
        catch
        {
            // For the first version we simply skip files that cannot be read.
            // Later we can surface a proper error list.
            return null;
        }
    }

    /// <summary>
    /// Writes a new Disc Number into the actual audio file on disk.
    /// This permanently changes the file.
    /// Returns true if the write succeeded, false if anything went wrong.
    /// </summary>
    public bool SetDiscNumber(string filePath, int discNumber)
    {
        try
        {
            // Open the audio file with ATL so we can read and write its tags
            ATL.Track atlTrack = new ATL.Track(filePath);

            // Update the Disc Number tag in memory
            atlTrack.DiscNumber = discNumber;

            // Save() writes the change back to the physical file.
            // It returns true on success, false on failure.
            bool success = atlTrack.Save();

            return success;
        }
        catch
        {
            // Any exception (file locked, permission issue, unsupported format, etc.)
            // is treated as a failure for now. Later we can surface better error messages.
            return false;
        }
    }
    /// <summary>
    /// Writes a new Track Number into the actual audio file on disk.
    /// Returns true if the write succeeded, false if it failed.
    /// </summary>
    public bool SetTrackNumber(string filePath, int trackNumber)
    {
        try
        {
            ATL.Track atlTrack = new ATL.Track(filePath);

            // Update the Track Number tag
            atlTrack.TrackNumber = trackNumber;

            // Save the change permanently to the file
            return atlTrack.Save();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Writes a new Title into the actual audio file on disk.
    /// Returns true if the write succeeded, false if it failed.
    /// </summary>
    public bool SetTitle(string filePath, string title)
    {
        try
        {
            ATL.Track atlTrack = new ATL.Track(filePath);
            atlTrack.Title = title;
            return atlTrack.Save();
        }
        catch
        {
            return false;
        }
    }
}