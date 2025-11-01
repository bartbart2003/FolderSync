# FolderSync
A simple C# program for performing periodic, one-way synchronization of files from one folder to another.

At a configurable time interval, it scans the *source* and *replica* folders, and compares them looking for differences. 
File comparison is based on modification time, size, and checksum. 
For efficiency, checksum is only calculated in specific cases, when metadata alone doesn't give a clear answer.

## Usage
```
./FolderSync <source folder> <replica folder> <log file> <sync interval (s)>
```
e.g.
```
./FolderSync /home/user/Source /home/user/Replica ../sync-log.txt 15
```

## Known issues/caveats
- empty subfolders are ignored during synchronization process, meaning they are neither added to, nor removed from the replica folder
- syncing between filesystems with different case-sensitivities (e.g. ext4 and FAT32) is untested and may lead to improper behavior

## External libraries used
- [Serilog](https://serilog.net/) licensed under Apache 2.0, together with File and Console sinks

