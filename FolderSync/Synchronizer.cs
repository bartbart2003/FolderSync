using System.Security.Cryptography;
using Serilog;

namespace FolderSync;

using FileDictionary = Dictionary<string, FileStruct>;

/// <summary>
/// Class <c>Synchronizer</c> is responsible for performing synchronization of files between source and replica folders.
/// </summary>
public sealed class Synchronizer: IDisposable
{
    private string _sourceFolder;
    private string _replicaFolder;
    private int _syncIntervalMs;
    
    private bool _disposed = false;
    
    private CancellationTokenSource _cts;
    private Thread _fileSyncThread;
    
    private static EnumerationOptions _enumOptions = new()
    {
        MatchCasing = MatchCasing.PlatformDefault,
        MatchType = MatchType.Simple,
        RecurseSubdirectories = true,
        IgnoreInaccessible = false,
        ReturnSpecialDirectories = false
    };
    
    /// <param name="sourceFolder">source folder for synchronizing files</param>
    /// <param name="replicaFolder">replica folder for synchronizing files</param>
    /// <param name="syncIntervalMs">synchronization interval, in milliseconds</param>
    public Synchronizer(string sourceFolder, string replicaFolder, int syncIntervalMs)
    {
        _sourceFolder = sourceFolder;
        _replicaFolder = replicaFolder;
        _syncIntervalMs = syncIntervalMs;
        
        _cts = new CancellationTokenSource();
        
        _fileSyncThread = new Thread(() => FileSyncLoop(_cts.Token));
        // mark the thread as background so that it terminates when app closes
        _fileSyncThread.IsBackground = true;
    }

    /// <summary>
    /// Start the synchronization loop.
    /// </summary>
    public void StartSync()
    {
        Log.Information("Starting synchronization from {SourceFolder} to {ReplicaFolder}, with interval of {SyncInterval} seconds...",
            _sourceFolder, _replicaFolder, (_syncIntervalMs/1000));
        _fileSyncThread.Start();
    }

    private void FileSyncLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                SynchronizeFiles();
            }
            catch (FilesSynchronizationException e)
            {
                Log.Fatal(e, "Files synchronization exception occurred. Exiting!");
                Log.CloseAndFlush();
                Environment.Exit(1);
                return;
            }
            if (!token.IsCancellationRequested) Thread.Sleep(_syncIntervalMs);
        }
        Log.Debug("FileSyncLoop: Cancellation requested.");
    }
    
    private void SynchronizeFiles() {
        var sourceDict =  EnumerateFiles(_sourceFolder);
        var replicaDict =  EnumerateFiles(_replicaFolder);
        
        var keysInSourceOnly = sourceDict.Keys.Except(replicaDict.Keys);
        var keysInReplicaOnly = replicaDict.Keys.Except(sourceDict.Keys);
        var keysInBoth = sourceDict.Keys.Intersect(replicaDict.Keys);

        foreach (var fileName in keysInReplicaOnly)
        {
            RemoveFromReplica(fileName);
        }
        
        foreach (var fileName in keysInSourceOnly)
        {
            CopyFromSource(fileName);
        }

        // Compare files that are in both source and replica
        foreach (var fileName in keysInBoth)
        {
            string sourcePath = Path.Combine(_sourceFolder, fileName);
            string replicaPath = Path.Combine(_replicaFolder, fileName);

            // if sizes are different, files are surely different - copy over
            if (sourceDict[fileName].FileSizeBytes != replicaDict[fileName].FileSizeBytes)
            {
                Log.Information("File {FileName} has different size in source and replica, overwriting", fileName);
                try
                {
                    File.Copy(sourcePath, replicaPath, true);
                }
                catch (Exception e)
                {
                    throw new FilesSynchronizationException($"Cannot overwrite {fileName}", e);
                }
            }
            // if sizes are identical, but modification times differ, files MAY be different - compare checksums
            else if (sourceDict[fileName].ModifiedTime != replicaDict[fileName].ModifiedTime)
            {
                Log.Verbose("File {FileName} has different modification time in source and replica, comparing checksums...", fileName);
                
                // if checksums match, file is identical - do nothing
                if (ChecksumEqual(sourcePath, replicaPath))
                {
                    Log.Verbose("File {FileName} has identical checksum in source and replica", fileName);
                }
                // if checksums do not match - copy over
                else
                {
                    Log.Information("File {FileName} has different checksum in source and replica, overwriting", fileName);
                    try
                    {
                        File.Copy(sourcePath, replicaPath, true);
                    }
                    catch (Exception e)
                    {
                        throw new FilesSynchronizationException($"Cannot overwrite {fileName}", e);
                    }
                }
            }
            // if size and modification time are identical, assume files are identical and do nothing
            else
            {
                Log.Verbose("File {FileName} has the same modification time and size in source and replica, assuming identical", fileName);
            }
        }
    }

    private bool ChecksumEqual(string sourcePath, string replicaPath)
    {
        try
        {
            using var md5 = MD5.Create();
            using FileStream sourceStream = File.OpenRead(sourcePath), replicaStream = File.OpenRead(replicaPath);
            return (md5.ComputeHash(sourceStream) == md5.ComputeHash(replicaStream));
        }
        catch (Exception e)
        {
            throw new FilesSynchronizationException($"Cannot compare checksums of {sourcePath} and {replicaPath}", e);
        }
    }
    
    private FileDictionary EnumerateFiles(string path)
    {
        FileDictionary dict = new();

        IEnumerable<string> files;

        try
        {
            files = Directory.EnumerateFiles(path, "*", _enumOptions);
        }
        catch (Exception e)
        {
            throw new FilesSynchronizationException($"Error enumerating files in {path}", e);
        }

        foreach (string file in files)
        {
            string fileName = file.Substring(path.Length + 1);
            Log.Verbose("Found {FileName} in {Path}",  fileName, path);

            FileStruct fs;
            try
            {
                fs = new FileStruct(new FileInfo(file).Length, File.GetLastWriteTime(file));
            }
            catch (Exception e)
            {
                throw new FilesSynchronizationException($"Error retrieving file info about {fileName} in {path}", e);
            }
            
            dict.Add(fileName, fs);
        }
        Log.Verbose("New dictionary: {@Dictionary}", dict);

        return dict;
    }

    private void CopyFromSource(string fileName)
    {
        string sourcePath = Path.Combine(_sourceFolder, fileName);
        string replicaPath = Path.Combine(_replicaFolder, fileName);
        // create directory structure (if it already exists, does nothing)
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(replicaPath)!);
            File.Copy(sourcePath, replicaPath);
        }
        catch (Exception e)
        {
            throw new FilesSynchronizationException($"Error copying file {fileName}", e);
        }
        
        Log.Verbose("Copied file from {SourcePath} to {ReplicaPath}", sourcePath, replicaPath);
    }

    private void RemoveFromReplica(string fileName)
    {
        string replicaPath = Path.Combine(_replicaFolder, fileName);
        try
        {
            File.Delete(replicaPath);
        }
        catch (Exception e)
        {
            throw new FilesSynchronizationException($"Error removing file {fileName}", e);
        }
        
        Log.Verbose("Deleted file from {ReplicaPath}", replicaPath);
    }

    public void Dispose()
    {
        if (_disposed) return;
        //Console.WriteLine("Disposing synchronizer...");
        _cts.Cancel();
        _fileSyncThread.Join();
        _cts.Dispose();
        //Console.WriteLine("Disposed synchronizer.");
        _disposed = true;
    }
}