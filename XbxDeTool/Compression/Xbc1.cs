using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ImpromptuNinjas.ZStd;

using Syroot.BinaryData;

namespace XbxDeTool.Compression;

public class Xbc1
{
    /// <summary>
    /// 'Xbc1'
    /// </summary>
    public const uint MAGIC = 0x31636278;

    public static void Decompress(Stream inputStream, long offset, Stream outputStream, uint? expectedSize = null)
    {
        const int Xbc1HeaderSize = 0x30;

        uint magic = inputStream.ReadUInt32();
        if (magic != MAGIC)
            throw new InvalidDataException("Not a Xbc1 stream.");

        XbcCompressionType compressionType = (XbcCompressionType)inputStream.ReadUInt32();
        uint decompressedSize = inputStream.ReadUInt32();
        uint compresserdSize = inputStream.ReadUInt32();
        uint crc = inputStream.ReadUInt32();
        // TODO: name

        if (expectedSize is not null)
            Debug.Assert(decompressedSize == expectedSize);

        inputStream.Position = offset + Xbc1HeaderSize;

        Stream compressionStream = compressionType switch
        {
            XbcCompressionType.Zlib => new ZLibStream(inputStream, CompressionMode.Decompress, leaveOpen: true),
            XbcCompressionType.ZStd => new ZStdDecompressStream(inputStream, leaveOpen: true),
            _ => throw new NotSupportedException($"Compression type {compressionType} is not supported."),
        };

        Utils.CopyStreamRange(compressionStream, outputStream, decompressedSize);

        compressionStream.Dispose();
    }
}

public enum XbcCompressionType
{
    Zlib = 1,
    ZStd = 3,
}
