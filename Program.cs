
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
        private static ConcurrentQueue<(string SourcePath, string? OldSourcePath, WatcherChangeTypes ChangeType)> changes = new();



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
        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                return;
            }
            //Console.WriteLine($"Changed: {e.FullPath}");
            changes.Enqueue((e.FullPath, null, e.ChangeType));
        }

        private static void OnCreated(object sender, FileSystemEventArgs e)
        {
            //string value = $"Created: {e.FullPath}";
            //Console.WriteLine(value);
            changes.Enqueue((e.FullPath, null, e.ChangeType));
        }

        private static void OnDeleted(object sender, FileSystemEventArgs e)
        {
            //Console.WriteLine($"Deleted: {e.FullPath}");
            changes.Enqueue((e.FullPath, null, e.ChangeType));
        }

        private static void OnRenamed(object sender, RenamedEventArgs e)
        {
            //Console.WriteLine($"Renamed:");
            //Console.WriteLine($"    Old: {e.OldFullPath}");
            //Console.WriteLine($"    New: {e.FullPath}");
            changes.Enqueue((e.FullPath, e.OldFullPath, e.ChangeType));
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

        private static async Task DoBackup(Dictionary<string, (string? OldSourcePath, WatcherChangeTypes ChangeType)> batch)
        {
            foreach (var action in batch)
            {
                string relativePath = Path.GetRelativePath(source, action.Key);
                string replicaPath = Path.Combine(replica, relativePath);

                if (action.Value.ChangeType == WatcherChangeTypes.Created || action.Value.ChangeType == WatcherChangeTypes.Changed)
                {
                    // Copy source file to replica
                    Console.WriteLine($"Copying: {action.Key}");

                    Directory.CreateDirectory(Path.GetDirectoryName(replicaPath)!);
                    File.Copy(action.Key, replicaPath, true);

                }
                else if (action.Value.ChangeType == WatcherChangeTypes.Renamed)
                {                 
                    string oldRelativePath = Path.GetRelativePath(source, action.Value.OldSourcePath!);
                    string oldReplicaPath = Path.Combine(replica, oldRelativePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(replicaPath)!);
                    File.Move(oldReplicaPath, replicaPath, true);
                }
                else if (action.Value.ChangeType == WatcherChangeTypes.Deleted)
                {
                    // Delete file from replica
                    Console.WriteLine($"Deleting: {action.Key}");

                    File.Delete(replicaPath);
                }
            }

            await Task.CompletedTask;
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

            // FolderSyncronization();

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

            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;

            // backup
            using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromSeconds(gap));

            while (await timer.WaitForNextTickAsync())
            {
                // Log queue
                foreach (var change in changes)
                {
                    Console.WriteLine($"QUEUE:{change.SourcePath} -> {change.ChangeType}");
                }


                Dictionary<string, (string? OldSourcePath, WatcherChangeTypes ChangeType)> batch = new();
                int count = changes.Count;
                for (int i = 0; i < count; i++)
                {
                    if (changes.TryDequeue(out var change))
                    {
                        batch[change.SourcePath] = (change.OldSourcePath, change.ChangeType);
                    }
                }

               



                //Log Dictionary
                foreach (var b in batch)
                {
                    Console.WriteLine($"BATCH:{b.Key} -> {b.Value.ChangeType}");
                }

                await DoBackup(batch);
            }


        }






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


