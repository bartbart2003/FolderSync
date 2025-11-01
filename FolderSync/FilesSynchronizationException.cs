namespace FolderSync;

/// <summary>
/// Files synchronization exception (e.g. due to insufficient permissions).
/// </summary>
public class FilesSynchronizationException : Exception
{
    public FilesSynchronizationException()
    {
    }

    public FilesSynchronizationException(string message)
        : base(message)
    {
    }

    public FilesSynchronizationException(string message, Exception inner)
        : base(message, inner)
    {
    }
}