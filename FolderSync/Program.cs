using Serilog;

// create logger configuration - specify minimum level, and always write to console
var loggerConf = new LoggerConfiguration()
    .MinimumLevel.Debug()
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
        log.Information("Source and replica folders exist and are readable.");
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

log.Information("Starting synchronization...");

return 0;