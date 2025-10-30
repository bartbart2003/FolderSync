using System.Security.Cryptography;
using System.Timers;
using Serilog;
using Timer = System.Timers.Timer;

namespace FolderSync;

using FileDictionary = Dictionary<string, FileStruct>;

public class Synchronizer: IDisposable
{
    private string _sourceFolder;
    private string _replicaFolder;
    private Timer _syncTimer;
    
    private static EnumerationOptions _enumOptions = new()
    {
        MatchCasing = MatchCasing.PlatformDefault,
        MatchType = MatchType.Simple,
        RecurseSubdirectories = true,
        IgnoreInaccessible = false,
        ReturnSpecialDirectories = false
    };

    public Synchronizer(string sourceFolder, string replicaFolder, int syncIntervalMs)
    {
        _sourceFolder = sourceFolder;
        _replicaFolder = replicaFolder;
        
        _syncTimer = new Timer(syncIntervalMs);
        _syncTimer.Elapsed += SyncTimerEvent;
        _syncTimer.AutoReset = true;
    }

    public void StartSync()
    {
        Log.Information("Starting synchronization from {SourceFolder} to {ReplicaFolder}, with interval of {SyncInterval} seconds...",
            _sourceFolder, _replicaFolder, (int)(_syncTimer.Interval/1000));
        _syncTimer.Start();
    }

    public void StopSync()
    {
        Log.Information("Stopping synchronization.");
        _syncTimer.Stop();
    }

    private void SyncTimerEvent(object? source, ElapsedEventArgs e)
    {
        var sourceDict =  EnumerateFiles(_sourceFolder);
        var replicaDict =  EnumerateFiles(_replicaFolder);
        
        var keysInSourceOnly = sourceDict.Keys.Except(replicaDict.Keys);
        var keysInReplicaOnly = replicaDict.Keys.Except(sourceDict.Keys);
        var keysInBoth = sourceDict.Keys.Intersect(replicaDict.Keys);

        foreach (var fileName in keysInReplicaOnly)
        {
            removeFromReplica(fileName);
        }
        
        foreach (var fileName in keysInSourceOnly)
        {
            copyFromSource(fileName);
        }

        // Compare files that are in both
        foreach (var fileName in keysInBoth)
        {
            string sourcePath = Path.Combine(_sourceFolder, fileName);
            string replicaPath = Path.Combine(_replicaFolder, fileName);

            if (sourceDict[fileName].FileSizeBytes != replicaDict[fileName].FileSizeBytes)
            {
                
                Log.Verbose("File {FileName} has different size in source and replica, overwriting", fileName);
                //File.Copy(sourcePath, replicaPath, true);
            }
            else if (sourceDict[fileName].ModifiedTime != replicaDict[fileName].ModifiedTime)
            {
                Log.Verbose("File {FileName} has different modification time in source and replica, comparing checksums...", fileName);
                using var md5 = MD5.Create();
                using FileStream sourceStream = File.OpenRead(sourcePath), replicaStream = File.OpenRead(replicaPath);
                if (md5.ComputeHash(sourceStream) == md5.ComputeHash(replicaStream))
                {
                    Log.Verbose("File {FileName} has identical checksum in source and replica", fileName);
                }
                else
                {
                    Log.Verbose("File {FileName} has different checksum in source and replica, overwriting", fileName);
                    //File.Copy(sourcePath, replicaPath, true);
                }
            }
            else
            {
                Log.Verbose("File {FileName} has the same modification time and size in source and replica, assuming identical", fileName);
            }
        }
    }

    private FileDictionary EnumerateFiles(string path)
    {
        FileDictionary dict = new();
        
        var files = Directory.EnumerateFiles(path, "*", _enumOptions);

        foreach (string file in files)
        {
            string fileName = file.Substring(path.Length + 1);
            Log.Verbose("Found {FileName} in {Path}",  fileName, path);

            FileStruct fs = new FileStruct(new FileInfo(file).Length, File.GetLastWriteTime(file));

            dict.Add(fileName, fs);
        }
        Log.Verbose("New dictionary: {@Dictionary}", dict);

        return dict;
    }

    private void copyFromSource(string fileName)
    {
        string sourcePath = Path.Combine(_sourceFolder, fileName);
        string replicaPath = Path.Combine(_replicaFolder, fileName);
        // create directory structure (if it already exists, does nothing)
        //Directory.CreateDirectory(Path.GetDirectoryName(replicaPath));
        //File.Copy(sourcePath, replicaPath);
        Log.Verbose("Copied file from {SourcePath} to {ReplicaPath}", sourcePath, replicaPath);
    }

    private void removeFromReplica(string fileName)
    {
        string replicaPath = Path.Combine(_replicaFolder, fileName);
        //File.Delete(replicaPath);
        Log.Verbose("Deleted file from {ReplicaPath}", replicaPath);
    }

    public void Dispose()
    {
        Console.WriteLine("Disposing synchronizer...");
    }
}