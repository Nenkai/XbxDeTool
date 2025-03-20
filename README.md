# XbxDeTool

Xenoblade Chronicles X: Definitive Edition ARD/ARH2 Extractor.

## Usage

Download the latest version from [Releases](https://github.com/Nenkai/XbxDeTool/releases).

* Extract all files in the archive: `extract-all -i <path to .arh> [-o output dir]`
* Extract a known file in the archive: `extract-file -i <path to .arh> -f <game path> [-o output dir]`
* Extract a file by hash in the archive: `extract-hash -i <path to .arh> -h <16 character hash> [-o output dir]`
* List all known hashes in the archive: `hash-list -i <path to .arh>`

## Research Notes

Unlike previous Xenoblade games, Monolith has transitioned to a hashed file system - no paths are present. Paths have to be found manually.

Currently, `40631` out of `104824` hashes are known (38.76%). Please contribute!

* [x] Used XBX file lists as a base, remapped `.ca` extensions to `.wi`
* [x] [strings2](https://github.com/glmcdona/strings2)'d over the entire game contents for extra paths
* [ ] Remap `/menu` paths to `/ui/..` as that folder has moved
* [ ] Log files at runtime, from start to full game completion

Additionally, the file system has been dumbed down compared to XB3. The header is essentially a list of:

```c
struct
{
    uint64 Hash; // XXHash64
    uint32 DiskSize; // Size in the archive
    uint32 ExpandedSize; // Size when decompressed, if compressed. Note that even if this isn't set, the file may still be wrapped in a 'Xbc1' header.
} FileInfo;
```

File offsets are calculated during file system initialization by accumulating disk sizes and aligning them using an alignment value in the main header.

## Building

.NET 9.0 SDK, Visual Studio 2022.

## License

MIT License.
