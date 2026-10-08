namespace Replica
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    class Program
    {
        //private static Timer aTimer;
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
                    string relativePath = Path.GetRelativePath(source, action.Key);
                    string replicaPath = Path.Combine(replica, relativePath);

                    if (action.Value.ChangeType == WatcherChangeTypes.Created || action.Value.ChangeType == WatcherChangeTypes.Changed || action.Value.ChangeType == WatcherChangeTypes.Renamed)
                    {
                        if (action.Value.ChangeType == WatcherChangeTypes.Renamed)
                        {
                            string oldRelativePath = Path.GetRelativePath(source, action.Value.OldSourcePath!);

                            string oldReplicaPath = Path.Combine(replica, oldRelativePath);

                            if (File.Exists(oldReplicaPath))
                            {
                                File.Delete(oldReplicaPath);
                            }

                            else if (Directory.Exists(oldReplicaPath))
                            {
                                Directory.Delete(oldReplicaPath, true);
                            }
                        }

                        if (Directory.Exists(action.Key))
                        {
                            Directory.CreateDirectory(replicaPath);
                        }
                        else if (File.Exists(action.Key))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(replicaPath)!);
                            File.Copy(action.Key, replicaPath, true);
                        }

                    }

                    else if (action.Value.ChangeType == WatcherChangeTypes.Deleted)
                    {
                        if (File.Exists(replicaPath))
                        {
                            File.Delete(replicaPath);
                        }
                        else if (Directory.Exists(replicaPath))
                        {
                            Directory.Delete(replicaPath, true);
                        }
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
                File.WriteAllText(LogPath, "");
                Dictionary<string, (string? OldSourcePath, WatcherChangeTypes ChangeType)> batch = new();

                foreach (string dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                {
                    Directory.CreateDirectory(dirPath.Replace(source, replica));

                    batch[dirPath] = (null, WatcherChangeTypes.Created);
                }

                //Copy all the files & Replaces any files with the same name
                foreach (string newPath in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
                {
                    File.Copy(newPath, newPath.Replace(source, replica), true);

                    batch[newPath] = (null, WatcherChangeTypes.Created);
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

                Directory.CreateDirectory(replica);



                FolderSyncronization(source, replica);

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


                using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromSeconds(gap));

                while (await timer.WaitForNextTickAsync())
                {
                    Dictionary<string, (string? OldSourcePath, WatcherChangeTypes ChangeType)> batch = new();
                    int count = changes.Count;
                    for (int i = 0; i < count; i++)
                    {
                        if (changes.TryDequeue(out var change))
                        {
                            if (batch.TryGetValue(change.SourcePath, out var existing) && existing.ChangeType == WatcherChangeTypes.Renamed)
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


