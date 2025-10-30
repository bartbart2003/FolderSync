using System.Security.Cryptography;
using Serilog;

// create logger configuration - specify minimum level, and always write to console
var loggerConf = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.Console();

// if log file is specified, add file sink to logger config
if (args.Length >= 3) loggerConf = loggerConf.WriteTo.File(args[2]);

using var log = loggerConf.CreateLogger();

String sourceFolder;
String replicaFolder;
int syncInterval;

if (args.Length == 4)
{
    sourceFolder = args[0];
    replicaFolder = args[1];
    try
    {
        syncInterval = int.Parse(args[3]);
    }
    catch (Exception)
    {
        log.Fatal("Failed to parse synchronization interval!");
        return 1;
    }

    if (Directory.Exists(sourceFolder) && Directory.Exists(replicaFolder))
    {
        log.Debug("Source and replica folders exist and are readable.");
    }
    else
    {
        log.Fatal("Source and/or replica folders do not exist or are not readable!");
        return 1;
    }
}
else
{
    log.Fatal("Expected 4 arguments! Syntax: <source folder> <replica folder> <log file> <sync interval (s)>");
    return 1;
}

log.Information("Starting synchronization from {SourceFolder} to {ReplicaFolder}, with interval of {SyncInterval} seconds...",
    sourceFolder, replicaFolder, syncInterval);

var enumOptions = new EnumerationOptions
{
    MatchCasing = MatchCasing.PlatformDefault,
    MatchType = MatchType.Simple,
    RecurseSubdirectories = true,
    IgnoreInaccessible = false,
    ReturnSpecialDirectories = false
};

Dictionary<string, FileStruct> sourceHashes =
    new Dictionary<string, FileStruct>();

try
{
    var sourceFiles = Directory.EnumerateFiles(sourceFolder, "*", enumOptions);

    foreach (string file in sourceFiles)
    {
        string fileName = file.Substring(sourceFolder.Length + 1);
        log.Verbose("Found {FileName} in source folder",  fileName);

        FileStruct fs = new FileStruct
        {
            ModifiedTime = File.GetLastWriteTime(file),
            FileSize = new FileInfo(file).Length
        };

        sourceHashes.Add(fileName, fs);
    }
}
catch (Exception ex)
{
    log.Error(ex.Message);
}

log.Verbose("Source dictionary: {@SourceHashes}", sourceHashes);

Dictionary<string, FileStruct> replicaHashes =
    new Dictionary<string, FileStruct>();

try
{
    var replicaFiles = Directory.EnumerateFiles(replicaFolder, "*", enumOptions);

    foreach (string file in replicaFiles)
    {
        string fileName = file.Substring(replicaFolder.Length + 1);
        log.Verbose("Found {FileName} in replica folder",  fileName);
        
        FileStruct fs = new FileStruct
        {
            ModifiedTime = File.GetLastWriteTime(file),
            FileSize = new FileInfo(file).Length
        };

        replicaHashes.Add(fileName, fs);
        
        // using (var md5 = MD5.Create())
        // {
        //     using (var stream = File.OpenRead(file))
        //     {
        //         replicaHashes.Add(fileName, md5.ComputeHash(stream));
        //     }
        // }
    }
}
catch (Exception ex)
{
    log.Error(ex.Message);
}

log.Verbose("Replica dictionary: {@ReplicaHashes}", replicaHashes);

var keysInSourceOnly = sourceHashes.Keys.Except(replicaHashes.Keys);
var keysInReplicaOnly = replicaHashes.Keys.Except(sourceHashes.Keys);
var keysInBoth = sourceHashes.Keys.Intersect(replicaHashes.Keys);

// copy files that are in source only
foreach (var fileName in keysInSourceOnly)
{
    string sourcePath = Path.Combine(sourceFolder, fileName);
    string replicaPath = Path.Combine(replicaFolder, fileName);
    // create directory structure (if it already exists, does nothing)
    //Directory.CreateDirectory(Path.GetDirectoryName(replicaPath));
    //File.Copy(sourcePath, replicaPath);
    log.Verbose("Copied file from {SourcePath} to {ReplicaPath}", sourcePath, replicaPath);
}

// Delete files that are in replica only
foreach (var fileName in keysInReplicaOnly)
{
    string replicaPath = Path.Combine(replicaFolder, fileName);
    //File.Delete(replicaPath);
    log.Verbose("Deleted file from {ReplicaPath}", replicaPath);
}

// Compare files that are in both
foreach (var fileName in keysInBoth)
{
    string sourcePath = Path.Combine(sourceFolder, fileName);
    string replicaPath = Path.Combine(replicaFolder, fileName);

    if (sourceHashes[fileName].FileSize == replicaHashes[fileName].FileSize && sourceHashes[fileName].ModifiedTime == replicaHashes[fileName].ModifiedTime)
    {
        log.Verbose("File {FileName} has identical size and modtime in source and replica", fileName);
    }
    else
    {
        log.Verbose("File {FileName} has different size and/or modtime in source and replica, overwriting", fileName);
        //File.Copy(sourcePath, replicaPath, true);
    }
}

return 0;

struct FileStruct
{
    public long FileSize; // file size (in bytes)
    public DateTime ModifiedTime;
    public byte[] hash;
}