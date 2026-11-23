namespace DeezNET.Exceptions;

/// <summary>
/// Thrown when a track's download ends before the size the Deezer CDN reported.
/// </summary>
public class IncompleteTrackException : Exception
{
    public IncompleteTrackException() { }
    public IncompleteTrackException(string message) : base(message) { }
    public IncompleteTrackException(string message, Exception inner) : base(message, inner) { }
}
