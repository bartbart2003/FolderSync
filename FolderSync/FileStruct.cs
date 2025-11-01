namespace FolderSync;

/// <summary>
/// Struct storing metadata about a file.
/// </summary>
public readonly struct FileStruct
{
    public long FileSizeBytes { get; }
    public DateTime ModifiedTime { get; }

    public FileStruct(long fileSizeBytes, DateTime modifiedTime)
    {
        FileSizeBytes = fileSizeBytes;
        ModifiedTime = modifiedTime;
    }
}