using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using CommunityToolkit.HighPerformance.Buffers;

using ImpromptuNinjas.ZStd;

using Syroot.BinaryData;

using XbxDeTool.Hashing;
using System.IO;

namespace XbxDeTool;

public class ArchiveExtractor : IDisposable
{
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger? _logger;

    private ArchiveHeaderFile _headerFile;
    private BinaryStream _dataStream;

    private Dictionary<ulong, string> _knownPaths = [];
    static Dictionary<string, string> _startPatterns = new()
    {
        ["/chr_dl/"] = "/chr/dl/",
        ["/chr_en/"] = "/chr/en/",
        ["/chr_fc/"] = "/chr/fc/",
        ["/chr_fctex/"] = "/chr/fctex/",
        ["/chr_fceye/"] = "/chr/fceye/",
        ["/chr_mb/"] = "/chr/mp/",
        ["/chr_np/"] = "/chr/np/",
        ["/chr_oj/"] = "/chr/oj/",
        ["/chr_pac/"] = "/chr/pac/",
        ["/chr_pc/"] = "/chr/pc/",
        ["/chr_pt/"] = "/chr/pt/",
        ["/chr_un/"] = "/chr/un/",
        ["/chr_we/"] = "/chr/we/",
        ["/chr_wd/"] = "/chr/wd/",
        ["/chr_wdb/"] = "/chr/wdb/",
        ["/chr_ws/"] = "/chr/ws/",
    };

    private ArchiveExtractor(ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory;
        if (loggerFactory is not null)
            _logger = loggerFactory.CreateLogger(GetType().ToString());
    }

    public static ArchiveExtractor? Init(string arhFilePath, ILoggerFactory? loggerFactory = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(arhFilePath, nameof(arhFilePath));

        var extractor = new ArchiveExtractor(loggerFactory);
        if (!extractor.Open(arhFilePath))
            return null;

        return extractor;
    }

    private bool Open(string arhFilePath)
    {
        var headerFile = ArchiveHeaderFile.Open(arhFilePath);

        _headerFile = headerFile;

        string ardFilePath = Path.ChangeExtension(arhFilePath, ".ard");
        if (!File.Exists(ardFilePath))
        {
            _logger?.LogError(".ard file does not exist next to .arh file.");
            return false;
        }

        var fs = File.OpenRead(ardFilePath);
        _dataStream = new BinaryStream(fs);

        _logger?.LogInformation("Num Files: {fileCount}", _headerFile.Files.Count);
        _logger?.LogInformation("Parsing file lists...");
        InitFileLists();

        _logger?.LogInformation("Known Hashes: {knownHashes}/{fileCount} ({percent:0.##}%)", _knownPaths.Count, _headerFile.Files.Count, 
            (float)_knownPaths.Count / _headerFile.Files.Count * 100.0f);
        _logger?.LogInformation("ARD2 ready.");

        return true;
    }

    private void InitFileLists()
    {
        foreach (var file in Directory.GetFiles("Filelists"))
        {
            if (Path.GetFileName(file) == "hash_list.txt")
            {
                foreach (var line in File.ReadAllLines(file))
                {
                    string[] spl = line.Split('|');
                    if (spl.Length >= 2)
                        TryRegisterPath(spl[1]);
                }
            }

            foreach (var line in File.ReadAllLines(file))
            {
                TryRegisterPath(line);
            }
        }
    }

    private void TryRegisterPath(string line)
    {
        string normalizedStr = line.Replace('\\', '/').ToLower();
        if (normalizedStr.StartsWith("/patch0/") || normalizedStr.StartsWith("/patch1/"))
            normalizedStr = normalizedStr.Substring(7);

        if (!normalizedStr.StartsWith('/'))
            normalizedStr = '/' + normalizedStr;

        foreach (var pat in _startPatterns)
        {
            if (normalizedStr.StartsWith(pat.Key))
            {
                normalizedStr = normalizedStr.Replace(pat.Key, pat.Value);
                break;
            }
        }

        ulong hash = XxHash64.HashPath(normalizedStr);
        if (_headerFile.Files.ContainsKey(hash))
            _knownPaths.TryAdd(hash, normalizedStr);

        if (normalizedStr.Contains(".ca"))
        {
            string wiPath = normalizedStr.Replace(".ca", ".wi");
            hash = XxHash64.HashPath(wiPath);

            if (_headerFile.Files.ContainsKey(hash))
                _knownPaths.TryAdd(hash, normalizedStr);
        }

        if (normalizedStr.StartsWith("/00") && normalizedStr.Length > 5)
        {
            string normStr2 = normalizedStr.Substring(5);
            hash = XxHash64.HashPath(normStr2);

            if (_headerFile.Files.ContainsKey(hash))
                _knownPaths.TryAdd(hash, normalizedStr);
        }
    }

    public bool Extract(string gamePath, string outputPath)
    {
        ulong hash = XxHash64.HashPath(gamePath);
        if (!_headerFile.Files.TryGetValue(hash, out ArchiveFileInfo? fileInfo))
            return false;

        ExtractFile(fileInfo, outputPath);
        return true;
    }

    public bool Extract(ulong hash, string outputPath)
    {
        if (!_headerFile.Files.TryGetValue(hash, out ArchiveFileInfo? fileInfo))
            return false;

        ExtractFile(fileInfo, outputPath);
        return true;
    }

    private int _counter = 0;
    public void ExtractAll(string outputDir)
    {
        _counter = 0;

        foreach (ArchiveFileInfo archiveFileInfo in _headerFile.Files.Values)
        {
            string outputPath;
            if (!_knownPaths.TryGetValue(archiveFileInfo.Hash, out string? path))
            {
                path = $"{archiveFileInfo.Hash:X16}";
                _logger?.LogInformation("[{counter}/{total}] Extracting unmapped: {file}", _counter+1, _headerFile.Files.Count, path);

                outputPath = Path.Combine(outputDir, ".unmapped", path+".bin");
            }
            else
            {
                _logger?.LogInformation("[{counter}/{total}] Extracting: {file}", _counter+1, _headerFile.Files.Count, path);
                string relativePath = path.StartsWith('/') ? path.Substring(1) : path;

                outputPath = Path.Combine(outputDir, relativePath);
            }

            ExtractFile(archiveFileInfo, outputPath);

            _counter++;
        }
    }

    public void CreateHashList(string outputPath)
    {
        using var sw = File.CreateText(outputPath);
        foreach (ArchiveFileInfo archiveFileInfo in _headerFile.Files.Values)
        {
            if (_knownPaths.TryGetValue(archiveFileInfo.Hash, out string? path))
                sw.WriteLine($"{archiveFileInfo.Hash:X16}|{path}");
            else
                sw.WriteLine($"{archiveFileInfo.Hash:X16}|");
        }
    }

    private void ExtractFile(ArchiveFileInfo archiveFile, string outputPath)
    {
        _dataStream.Position = archiveFile.Offset;

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var outputStream = File.Create(outputPath);

        uint magic = _dataStream.ReadUInt32();
        if (magic == 0x31636278)
        {
            HandleXbcHeader(_dataStream, outputStream, archiveFile);
        }
        else
        {
            _dataStream.Position -= 4;
            CopyStream(_dataStream, outputStream, archiveFile.DiskSize);
        }
    }

    private static void HandleXbcHeader(BinaryStream inputStream, Stream outputStream, ArchiveFileInfo file)
    {
        const int Xbc1HeaderSize = 0x30;

        XbcCompressionType compressionType = (XbcCompressionType)inputStream.ReadUInt32();
        uint decompressedSize = inputStream.ReadUInt32();
        uint compresserdSize = inputStream.ReadUInt32();
        uint crc = inputStream.ReadUInt32();
        // TODO: name

        if (file.IsCompressed)
            Debug.Assert(decompressedSize == file.ExpandedSize);

        inputStream.Position = file.Offset + Xbc1HeaderSize;


        Stream compressionStream = compressionType switch
        {
            XbcCompressionType.Zlib => new ZLibStream(inputStream, CompressionMode.Decompress),
            XbcCompressionType.ZStd => new ZStdDecompressStream(inputStream),
            _ => throw new NotSupportedException($"Compression type {compressionType} is not supported."),
        };

        CopyStream(compressionStream, outputStream, decompressedSize);
    }

    private static void CopyStream(Stream inputStream, Stream outputStream, uint length)
    {
        const int BufferSize = 0x40000;

        long remSize = length;
        using MemoryOwner<byte> outBuffer = MemoryOwner<byte>.Allocate(BufferSize);

        while (remSize > 0)
        {
            int chunkSize = (int)Math.Min(remSize, BufferSize);
            Span<byte> chunk = outBuffer.Span.Slice(0, chunkSize);

            inputStream.ReadExactly(chunk);
            outputStream.Write(chunk);

            remSize -= chunkSize;
        }
    }

    public void Dispose()
    {
        ((IDisposable)_dataStream).Dispose();
    }

    public enum XbcCompressionType
    {
        Zlib = 1,
        ZStd = 3,
    }
}
