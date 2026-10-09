namespace Replica
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    class Program
    {
        private static string source = "";
        private static string replica = "";
        private static string LogPath = "";
        private static int gap;
        private static ConcurrentQueue<(string SourcePath, string? OldSourcePath, WatcherChangeTypes ChangeType)> changes = new();


        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                return;
            }
            changes.Enqueue((e.FullPath, null, e.ChangeType));
        }

        private static void OnCreated(object sender, FileSystemEventArgs e)
        {
            changes.Enqueue((e.FullPath, null, e.ChangeType));
        }

        private static void OnDeleted(object sender, FileSystemEventArgs e)
        {
            changes.Enqueue((e.FullPath, null, e.ChangeType));
        }

        private static void OnRenamed(object sender, RenamedEventArgs e)
        {
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
        private static void Log(Dictionary<string, (string? OldSourcePath, WatcherChangeTypes ChangeType)> batch)
        {
            try
            {
                foreach (var action in batch)
                {
                    if (action.Value.ChangeType == WatcherChangeTypes.Renamed)
                    {
                        Console.WriteLine($"{DateTime.Now}: {action.Key} -> {action.Value.ChangeType} (Old Path: {action.Value.OldSourcePath})");
                        File.AppendAllText(LogPath, $"{DateTime.Now}: {action.Key} -> {action.Value.ChangeType} (Old Path: {action.Value.OldSourcePath}) {Environment.NewLine}");
                    }
                    else
                    {
                        Console.WriteLine($"{DateTime.Now}: {action.Key} -> {action.Value.ChangeType}");
                        File.AppendAllText(LogPath, $"{DateTime.Now}: {action.Key} -> {action.Value.ChangeType} {Environment.NewLine}");
                    }
                }
            }
            catch (Exception ex)
            {
                PrintException(ex);
            }

        }

        private static void DoBackup(Dictionary<string, (string? OldSourcePath, WatcherChangeTypes ChangeType)> batch)
        {
            try
            {
                foreach (var action in batch)
                {
                    //Get unique path for the source folder
                    string RelativePath = Path.GetRelativePath(source, action.Key);
                    //Build the path for the replica folder
                    string ReplicaPath = Path.Combine(replica, RelativePath);

                    switch (action.Value.ChangeType)
                    {
                        case WatcherChangeTypes.Created:
                        case WatcherChangeTypes.Changed:
                            //If the action is a folder and it doesnt exist in the replica, create it.
                            if (Directory.Exists(action.Key))
                            {
                                Directory.CreateDirectory(ReplicaPath);
                            }

                            //If the action is a file, create the directory if it doesnt exist and copy the file to the replica.
                            else if (File.Exists(action.Key))
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(ReplicaPath)!);
                                File.Copy(action.Key, ReplicaPath, true);
                            }
                            break;

                        case WatcherChangeTypes.Renamed:

                            //Get the unique path of the old source folder
                            string OldRelativePath = Path.GetRelativePath(source, action.Value.OldSourcePath!);
                            //Build the path for the old replica folder
                            string OldReplicaPath = Path.Combine(replica, OldRelativePath);

                            //Delete the old file/folder in the replica if it exists
                            if (File.Exists(OldReplicaPath))
                            {
                                File.Delete(OldReplicaPath);
                            }
                            else if (Directory.Exists(OldReplicaPath))
                            {
                                Directory.Delete(OldReplicaPath, true);
                            }

                            //Create the new file/folder in the replica
                            if (Directory.Exists(action.Key))
                            {
                                Directory.CreateDirectory(ReplicaPath);
                            }
                            else if (File.Exists(action.Key))
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(ReplicaPath)!);
                                File.Copy(action.Key, ReplicaPath, true);
                            }
                            break;

                        case WatcherChangeTypes.Deleted:
                            //Delete the file/folder in the replica if it exists
                            if (File.Exists(ReplicaPath))
                            {
                                File.Delete(ReplicaPath);
                            }
                            else if (Directory.Exists(ReplicaPath))
                            {
                                Directory.Delete(ReplicaPath, true);
                            }
                            break;
                    }
                }

                Log(batch);

            }
            catch (Exception ex)
            {
                PrintException(ex);
            }

        }

        private static void FolderSyncronization(string source, string replica)
        {
            try
            {
                //Clear the log file at the start of the program
                File.WriteAllText(LogPath, "");

                //Clear the replica directory
                foreach (string file in Directory.GetFiles(replica))
                {
                    File.Delete(file);
                }
                foreach (string dir in Directory.GetDirectories(replica))
                {
                    Directory.Delete(dir, true);
                }

                //Create a batch of changes to log after the initial syncronization.
                Dictionary<string, (string? OldSourcePath, WatcherChangeTypes ChangeType)> batch = new();

                //Get all subdirectories and create them in the replica
                foreach (string DirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                {
                    string RelativePath = Path.GetRelativePath(source, DirPath);
                    string ReplicaPath = Path.Combine(replica, RelativePath);
                    Directory.CreateDirectory(ReplicaPath);

                    batch[DirPath] = (null, WatcherChangeTypes.Created);
                }

                //Copy all the files and replaces any files with the same name
                foreach (string NewPath in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
                {
                    string RelativePath = Path.GetRelativePath(source, NewPath);
                    string ReplicaPath = Path.Combine(replica, RelativePath);

                    File.Copy(NewPath, ReplicaPath, true);

                    batch[NewPath] = (null, WatcherChangeTypes.Created);
                }

                Log(batch);

            }
            catch (Exception ex)
            {
                PrintException(ex);
            }

        }

        static async Task Main(string[] args)
        {
            try
            {
                // Parse command line arguments
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
                        LogPath = args[i + 1];
                    }
                }

                //Create replica directory if needed
                Directory.CreateDirectory(replica);

                //Syncronizes source and replica at the start of the program
                FolderSyncronization(source, replica);

                //FileSystemWatcher to check for changes in the source directory
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

                //PeriodicTimer for async wait
                using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromSeconds(gap));

                while (await timer.WaitForNextTickAsync())
                {
                    //Create a batch of changes to process. The dictionary ensures only the last change of each file is processed.
                    Dictionary<string, (string? OldSourcePath, WatcherChangeTypes ChangeType)> batch = new();

                    int count = changes.Count;
                    for (int i = 0; i < count; i++)
                    {
                        if (changes.TryDequeue(out var change))
                        {
                            //Necessary to prevent situations where FileSystemWatcher registers a rename followed by a Changed(was happening in folders but also works for files) (only the rename is important, changed is for metadata)
                            if (batch.TryGetValue(change.SourcePath, out var PreviousChange) && PreviousChange.OldSourcePath != null)
                            {
                                continue;
                            }

                            batch[change.SourcePath] = (change.OldSourcePath, change.ChangeType);
                        }
                    }

                    DoBackup(batch);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return;
            }
        }
    }
}


