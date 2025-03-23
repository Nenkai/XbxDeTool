# XbxDeTool

Xenoblade Chronicles X: Definitive Edition ARD/ARH2 Extractor.

## Usage

Download the latest version from [Releases](https://github.com/Nenkai/XbxDeTool/releases).

* Extract all files in the archive: `XbxDeTool.exe extract-all -i <path to .arh> [-o output dir]`
* Extract a known file in the archive: `XbxDeTool.exe extract-file -i <path to .arh> -f <game path> [-o output dir]`
* Extract a file by hash in the archive: `XbxDeTool.exe extract-hash -i <path to .arh> -h <16 character hash> [-o output dir]`
* List all known hashes in the archive: `XbxDeTool.exe hash-list -i <path to .arh>`

> [!NOTE]  
> Arguments wrapped in `<>` are required and `[]` are optional.

## Research Notes

Unlike previous Xenoblade games, Monolith has transitioned to a hashed file system - no paths are present. Paths have to be found manually.

Currently, `92732` out of `104824` hashes are known (88.46%) for 1.0.1. Please contribute!

Additionally, the file system has been dumbed down compared to XB3. The header is essentially a list of:

```c
struct
{
    uint64 PathHash; // XXHash64 - example: XXHash64("/bdat/common.bdat".ToLower())
    uint32 DiskSize; // Size in the archive
    uint32 ExpandedSize; // Size when decompressed, if compressed. Note that even if this isn't set, the file may still be wrapped in a 'Xbc1' header.
} FileInfo;
```

File offsets are calculated during file system initialization by accumulating disk sizes and aligning them using an alignment value in the main header.

## Building

.NET 9.0 SDK, Visual Studio 2022.

## Credits

* [roccodev](https://github.com/roccodev) - Research, hashes
* [ScanMountGoat](https://github.com/ScanMountGoat) - hashes

## License

MIT License.
