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
}