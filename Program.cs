
//Synchronization must be one-way: after the synchronization content of the replica folder should be modified to exactly match content of the source folder;

//Synchronization should be performed periodically;

//File creation/copying/removal operations should be logged to a file and to the console output;


//Folder paths, synchronization interval and log file path should be provided using the command line arguments;


namespace Replica
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Timers;
    class Program
    {
        //private static Timer aTimer;
        static string source = "";
        static string replica = "";
        static string logF = "";
        static int gap;

        private static ConcurrentDictionary<string, WatcherChangeTypes> changes = new();



        /*  private static async Task CopyFilesRecursively(Object source_, System.Timers.ElapsedEventArgs e)
          {
              //Now Create all of the directories
              foreach (string dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
              {
                  await Directory.CreateDirectory(dirPath.Replace(source, replica));
              }

              //Copy all the files & Replaces any files with the same name
              foreach (string newPath in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
              {
                  File.Copy(newPath, newPath.Replace(source, replica), true);
              }

              Console.WriteLine("Log1");
          }*/


        private static async Task DoBackup()
        {
            /*foreach (var change in changes)
            {
                string sourcePath = change.Key;
                WatcherChangeTypes changeType = change.Value;

                if (changeType == WatcherChangeTypes.Created ||
                    changeType == WatcherChangeTypes.Changed)
                {
                    // Copy source file to replica
                    Console.WriteLine($"Copying: {sourcePath}");

                    File.Copy(sourcePath, replica, true);

                    changes.TryRemove(sourcePath, out _);
                }
                else if (changeType == WatcherChangeTypes.Deleted)
                {
                    // Delete file from replica
                    Console.WriteLine($"Deleting: {sourcePath}");

                    File.Delete(replica);

                    changes.TryRemove(sourcePath, out _);
                }

                changes.TryRemove(sourcePath, out _);
            }

            await Task.CompletedTask;*/
        }

        static async Task Main(string[] args)
        {


            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-s")
                {
                    source = args[i + 1];
                }
                else if (args[i] == "-r")
                {
                    replica = args[i + 1];
                }
                else if (args[i] == "-i")
                {
                    gap = Int32.Parse(args[i + 1]);
                }
                else if (args[i] == "-l")
                {
                    logF = args[i + 1];
                }
            }

            using var watcher = new FileSystemWatcher(source);

            watcher.NotifyFilter = NotifyFilters.Attributes
                                | NotifyFilters.CreationTime
                                | NotifyFilters.DirectoryName
                                | NotifyFilters.FileName
                                | NotifyFilters.LastAccess
                                | NotifyFilters.LastWrite
                                | NotifyFilters.Security
                                | NotifyFilters.Size;

            watcher.Changed += OnChanged;
            watcher.Created += OnCreated;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnError;

            // watcher.Filter = "*.txt";
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;

            // using disposes the timer when the application closes
            // backup
            using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromSeconds(gap));

            while (await timer.WaitForNextTickAsync())
            {

                
                ConcurrentDictionary<string, WatcherChangeTypes> changesBatch = new(changes);
                changes.Clear();

                
                await DoBackup();

                /*foreach (var change in changes)
                {
                    Console.WriteLine($"{change.Key} -> {change.Value}");
                }*/
            }


        }

        

        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                return;
            }
            Console.WriteLine($"Changed: {e.FullPath}");
            changes[e.FullPath] = e.ChangeType;
        }

        private static void OnCreated(object sender, FileSystemEventArgs e)
        {
            string value = $"Created: {e.FullPath}";
            Console.WriteLine(value);
            changes[e.FullPath] = e.ChangeType;
        }

        private static void OnDeleted(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"Deleted: {e.FullPath}");
            changes[e.FullPath] = e.ChangeType;
        }

        private static void OnRenamed(object sender, RenamedEventArgs e)
        {
            Console.WriteLine($"Renamed:");
            Console.WriteLine($"    Old: {e.OldFullPath}");
            Console.WriteLine($"    New: {e.FullPath}");
            changes[e.FullPath] = e.ChangeType;
        }

        private static void OnError(object sender, ErrorEventArgs e) =>
            PrintException(e.GetException());

        private static void PrintException(Exception? ex)
        {
            if (ex != null)
            {
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine("Stacktrace:");
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine();
                PrintException(ex.InnerException);
            }
        }

        /*
        // Create a timer and set a two second interval.
        aTimer = new System.Timers.Timer();
        aTimer.Interval = gap*1000;

        // Hook up the Elapsed event for the timer. 
        aTimer.Elapsed += CopyFilesRecursively;

        // Have the timer fire repeated events (true is the default)
        aTimer.AutoReset = true;

        // Start the timer
        aTimer.Enabled = true;

        Console.WriteLine("Press the Enter key to exit the program at any time... ");
        Console.ReadLine();

        */
        /*  string[] fileList = Directory.GetFiles(source);

          foreach(string f in fileList)
          {
              // Remove path from the file name.
              string file_ = f.Substring(source.Length + 1);

              // Use the Path.Combine method to safely append the file name to the path.
              // Will overwrite if the destination file already exists.
              File.Copy(Path.Combine(source, file_), Path.Combine(replica, file_), true);


          }*/


    }
}


