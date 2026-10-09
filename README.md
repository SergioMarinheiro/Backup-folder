# Synchronizes two folders
Synchronizes a source directory with a replica directory, keeping the replica up to date with changes made to the source.

## Running locally
```bash
git clone https://github.com/SergioMarinheiro/Backup-folder.git

cd Backup-folder

dotnet run -- -s "C:\Dev\Backup-folder\original" -r "C:\Dev\Backup-folder\backup" -i 5 -l "C:\Dev\Backup-folder\backup.log"
```

## Command line arguments:

- `s`: Source dir

- `r`: Replica dir

- `i`: Synchronization interval

- `l`: log file
