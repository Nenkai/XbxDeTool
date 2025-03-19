using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Hashing;

using Syroot.BinaryData;
using System.IO;

namespace XbxDeTool;

public class ArchiveHeaderFile
{
    public Dictionary<ulong, ArchiveFileInfo> Files { get; set; } = [];

    public static ArchiveHeaderFile Open(string file)
    {
        using var fs = File.Open(file, FileMode.Open);
        return Open(fs);
    }

    public static ArchiveHeaderFile Open(Stream stream)
    {
        var arh = new ArchiveHeaderFile();

        BinaryStream bs = new BinaryStream(stream);
        uint magic = bs.ReadUInt32();
        uint numFiles = bs.ReadUInt32();
        uint fileAlignment = bs.ReadUInt32();
        bs.Position += 4;

        long offset = 0;
        for (int i = 0; i < numFiles; i++)
        {
            var info = new ArchiveFileInfo();
            info.Read(bs);

            info.Offset = offset;
            offset += ((fileAlignment - 1) + info.DiskSize) & -fileAlignment;

            arh.Files.Add(info.Hash, info);
        }

        return arh;
    }
}

public class ArchiveFileInfo
{
    public ulong Hash { get; set; }
    public uint DiskSize { get; set; }
    public uint ExpandedSize { get; set; }
    public long Offset { get; set; }

    public bool IsCompressed => ExpandedSize != 0;

    public void Read(BinaryStream bs)
    {
        Hash = bs.ReadUInt64();
        DiskSize = bs.ReadUInt32();
        ExpandedSize = bs.ReadUInt32();
    }

    public override string ToString()
    {
        return $"{Hash:X16}";
    }
}
