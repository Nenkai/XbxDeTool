using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

using Microsoft.Extensions.Logging;

using CommunityToolkit.HighPerformance.Buffers;

using Syroot.BinaryData;

using XbxDeTool.Hashing;
using XbxDeTool.Compression;

namespace XbxDeTool;

public class ArchiveExtractor : IDisposable
{
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger? _logger;

    private ArchiveHeaderFile _headerFile;
    private BinaryStream _dataStream;

    private ArchiveExtractorOptions _options;

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

    private ArchiveExtractor(ArchiveExtractorOptions options = null, ILoggerFactory? loggerFactory = null)
    {
        _options = options ?? new ArchiveExtractorOptions();

        _loggerFactory = loggerFactory;
        if (loggerFactory is not null)
            _logger = loggerFactory.CreateLogger(GetType().ToString());
    }

    public static ArchiveExtractor? Init(string arhFilePath, ArchiveExtractorOptions options = null, ILoggerFactory? loggerFactory = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(arhFilePath, nameof(arhFilePath));

        var extractor = new ArchiveExtractor(options, loggerFactory);
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

        if (!_options.ExtractAllExternalXbcs)
            _logger?.LogWarning("Not decompressing all files compressed externally (Xbc1 layer). Some files may remain compressed.");

        return true;
    }

    private void InitFileLists()
    {
        string exePath = Utils.GetCurrentExecutingPath();
        string currentDir = Path.GetDirectoryName(exePath)!;
        string fileListDir = Path.Combine(currentDir, "Filelists");

        if (!Directory.Exists(fileListDir))
        {
            _logger?.LogWarning("Filelists directory is missing next to the executable.");
            return;
        }

        foreach (var file in Directory.GetFiles(fileListDir))
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

    private static string[] _locales = ["us", "jp", "cn", "fr", "sp", "ge", "tw", "kr"];

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
                _knownPaths.TryAdd(hash, wiPath);
        }

        if (normalizedStr.StartsWith("/00") && normalizedStr.Length > 5)
        {
            string normStr2 = normalizedStr.Substring(5);
            hash = XxHash64.HashPath(normStr2);

            if (_headerFile.Files.ContainsKey(hash))
                _knownPaths.TryAdd(hash, normStr2);
        }

        foreach (var currentLocale in _locales)
        {
            if (normalizedStr.Contains(currentLocale))
            {
                foreach (var targetLocale in _locales)
                {
                    string localePath = normalizedStr.Replace(currentLocale, targetLocale);
                    hash = XxHash64.HashPath(localePath);

                    if (_headerFile.Files.ContainsKey(hash))
                        _knownPaths.TryAdd(hash, localePath);
                }
            }
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
        _dataStream.Position -= 4;

        // We excluded smda (Section (?) Map DAta) from auto xbc extraction as they actually contain multiple chunks.
        // These xbc chunks are pointed to from smhd (Section (?) Map HeaDer), which contains offsets to chunks.
        if (archiveFile.IsCompressed || (magic == Xbc1.MAGIC && _options.ExtractAllExternalXbcs && !outputPath.EndsWith(".wismda")))
        {
            Xbc1.Decompress(_dataStream, archiveFile.Offset, outputStream, 
                archiveFile.IsCompressed ? archiveFile.ExpandedSize : null);
        }
        else
        {
            Utils.CopyStreamRange(_dataStream, outputStream, archiveFile.DiskSize);
        }
    }

    public void Dispose()
    {
        ((IDisposable)_dataStream).Dispose();
    }
}
