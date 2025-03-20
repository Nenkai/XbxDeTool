using System.Diagnostics;
using System.CommandLine;
using System.IO.Compression;

using Microsoft.Extensions.Logging;

using CommunityToolkit.HighPerformance.Buffers;

using Syroot.BinaryData;

using ImpromptuNinjas.ZStd;

using NLog;
using NLog.Extensions.Logging;

namespace XbxDeTool;

public class Program
{
    private static ILoggerFactory _loggerFactory;
    private static Microsoft.Extensions.Logging.ILogger _logger;

    public const string Version = "1.0.1";

    static async Task<int> Main(string[] args)
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddNLog());
        _logger = _loggerFactory.CreateLogger<Program>();

        Console.WriteLine("-----------------------------------------");
        Console.WriteLine($"- XBX:DE Extractor {Version} by Nenkai");
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine("- https://github.com/Nenkai");
        Console.WriteLine("- https://twitter.com/Nenkaai");
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine("");

        var rootCommand = new RootCommand("XBX:DE ARD/ARH2 Extractor");

        var inputOption = new Option<FileInfo>(
            aliases: ["--input", "-i"],
            description: "Input .arh file")
        {
            IsRequired = true,
        };
        var outputOption = new Option<FileInfo?>(
            aliases: ["--output", "-o"],
            description: "Output directory.");

        var extractAllCommand = new Command("extract-all", "Extracts all files from a .arh/ard archive.")
        {
            inputOption,
            outputOption,
        };
        extractAllCommand.SetHandler(UnpackAll, inputOption, outputOption);

        var gameFileOption = new Option<string>(
            aliases: ["--file", "-f"],
            description: "Game file to extract.")
        {
            IsRequired = true,
        };

        var extractFileCommand = new Command("extract-file", "Extracts a single file from a .arh/ard archive.")
        {
            inputOption,
            gameFileOption,
            outputOption,
        };
        extractFileCommand.SetHandler(UnpackFile, inputOption, gameFileOption, outputOption);

        var hashOption = new Option<string>(
            aliases: ["--hash", "-h"],
            description: "Hash to extract.")
        {
            IsRequired = true,
        };
        var extractHashCommand = new Command("extract-hash", "Extracts a single file by hash from a .arh/ard archive.")
        {
            inputOption,
            hashOption,
            outputOption,
        };
        extractHashCommand.SetHandler(UnpackHash, inputOption, hashOption, outputOption);

        var hashListCommand = new Command("hash-list", "Produces a hash list with known paths from the ard/arh archive.")
        {
            inputOption,
            outputOption,
        };
        hashListCommand.SetHandler(HashList, inputOption);

        rootCommand.AddCommand(extractAllCommand);
        rootCommand.AddCommand(extractFileCommand);
        rootCommand.AddCommand(extractHashCommand);
        rootCommand.AddCommand(hashListCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static void UnpackAll(FileInfo inputFile, FileInfo? outputDirInfo)
    {
        string outputDir;
        if (outputDirInfo is not null)
            outputDir = outputDirInfo.FullName;
        else
            outputDir = Path.Combine(inputFile.DirectoryName, "extracted")!;

        using var archiveExtractor = ArchiveExtractor.Init(inputFile.FullName, _loggerFactory);
        if (archiveExtractor is null)
        {
            _logger.LogError("Failed to open arh/ard files.");
            return;
        }

        archiveExtractor.ExtractAll(outputDir);

        _logger.LogInformation("Done.");
    }

    private static void UnpackFile(FileInfo inputFile, string gamePath, FileInfo? outputDirInfo)
    {
        string outputDir;
        if (outputDirInfo is not null)
            outputDir = outputDirInfo.FullName;
        else
            outputDir = Path.Combine(inputFile.DirectoryName, "extracted")!;

        using var archiveExtractor = ArchiveExtractor.Init(inputFile.FullName, _loggerFactory);
        if (archiveExtractor is null)
        {
            _logger.LogError("Failed to open arh/ard files.");
            return;
        }

        string outputFile = Path.Combine(outputDir, gamePath.StartsWith('/') ? gamePath.Substring(1) : gamePath);
        if (!archiveExtractor.Extract(gamePath, outputFile))
        {
            _logger.LogError("Failed to extract, file likely does not exist in archive.");
            return;
        }

        _logger.LogInformation("File extracted.");
    }

    private static void UnpackHash(FileInfo inputFile, string hashStr, FileInfo? outputDirInfo)
    {
        string outputDir;
        if (outputDirInfo is not null)
            outputDir = outputDirInfo.FullName;
        else
            outputDir = Path.Combine(inputFile.DirectoryName, "extracted")!;

        using var archiveExtractor = ArchiveExtractor.Init(inputFile.FullName, _loggerFactory);
        if (archiveExtractor is null)
        {
            _logger.LogError("Failed to open arh/ard files.");
            return;
        }

        if (hashStr.StartsWith("0x"))
            hashStr = hashStr.Substring(2);

        if (hashStr.Length != 16)
        {
            _logger.LogError("Hash string must be 16 characters in length. Example hash: DA7EB7B09B34DD80");
            return;
        }

        if (!ulong.TryParse(hashStr, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ulong hash))
        {
            _logger.LogError("Unable to parse hash string '{str}'.", hashStr);
            return;
        }

        string outputFile = Path.Combine(outputDir, $"{hashStr:X16}.bin");
        if (!archiveExtractor.Extract(hash, outputFile))
        {
            _logger.LogError("Failed to extract, file likely does not exist in archive.");
            return;
        }

        _logger.LogInformation("File extracted.");
    }

    private static void HashList(FileInfo inputFile)
    {
        using var archiveExtractor = ArchiveExtractor.Init(inputFile.FullName, _loggerFactory);
        if (archiveExtractor is null)
        {
            _logger.LogError("Failed to open arh/ard files.");
            return;
        }

        string output = Path.Combine(Path.GetDirectoryName(inputFile.FullName), "hash_list.txt");
        archiveExtractor.CreateHashList(output);

        _logger.LogInformation("Hash list exported at {path}.", output);
    }
}
