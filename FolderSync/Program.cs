using Serilog;

namespace FolderSync
{
    public class Program
    {
        public static int Main(string[] args)
        {
            // create logger configuration - specify minimum level, and always write to console
            var loggerConf = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console();

            // if log file is specified, add file sink to logger config
            if (args.Length >= 3) loggerConf = loggerConf.WriteTo.File(args[2]);

            // create logger, and set it globally
            Log.Logger = loggerConf.CreateLogger();

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
                    if (syncInterval <= 0) throw new ArgumentOutOfRangeException();
                }
                catch (Exception)
                {
                    Log.Fatal("Failed to parse synchronization interval!");
                    Log.CloseAndFlush();
                    return 1;
                }

                if (Directory.Exists(sourceFolder) && Directory.Exists(replicaFolder))
                {
                    Log.Debug("Source and replica folders exist and are readable.");
                }
                else
                {
                    Log.Fatal("Source and/or replica folders do not exist or are not readable!");
                    Log.CloseAndFlush();
                    return 1;
                }
            }
            else
            {
                Log.Fatal("Expected 4 arguments! Syntax: <source folder> <replica folder> <log file> <sync interval (s)>");
                Log.CloseAndFlush();
                return 1;
            }

            using var synchronizer = new Synchronizer(sourceFolder, replicaFolder, syncInterval*1000);
            
            synchronizer.StartSync();
            
            Console.WriteLine("Press Enter to exit...");
            
            while (Console.ReadKey().Key != ConsoleKey.Enter) {}
            
            Log.Information("Stopping synchronization...");
            
            Log.CloseAndFlush();
            
            return 0;
        }
    }
}