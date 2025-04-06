using System.Diagnostics;
using System.CommandLine;
using System.IO.Compression;

using Microsoft.Extensions.Logging;

using CommunityToolkit.HighPerformance.Buffers;

using Syroot.BinaryData;

using ImpromptuNinjas.ZStd;

using NLog;
using NLog.Extensions.Logging;
using XbxDeTool.Compression;

namespace XbxDeTool;

public class Program
{
    private static ILoggerFactory _loggerFactory;
    private static Microsoft.Extensions.Logging.ILogger _logger;

    public const string Version = "1.0.9";

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
        var noExtractXbc = new Option<bool>(
            aliases: ["--no-extract-xbc"],
            description: "Whether not to auto-extract files wrapped in a Xbc1 container.");

        var extractAllCommand = new Command("extract-all", "Extracts all files from a .arh/ard archive.")
        {
            inputOption,
            outputOption,
            noExtractXbc,
        };
        extractAllCommand.SetHandler(UnpackAll, inputOption, outputOption, noExtractXbc);

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
            noExtractXbc,
        };
        extractFileCommand.SetHandler(UnpackFile, inputOption, gameFileOption, outputOption, noExtractXbc);

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
            noExtractXbc,
        };
        extractHashCommand.SetHandler(UnpackHash, inputOption, hashOption, outputOption, noExtractXbc); 

        var hashListCommand = new Command("hash-list", "Produces a hash list with known paths from the ard/arh archive.")
        {
            inputOption,
        };
        hashListCommand.SetHandler(HashList, inputOption);

        var extractXbc = new Command("extract-xbc", "Extract files wrapped in a Xbc1 header/layer.")
        {
            inputOption,
        };
        extractXbc.SetHandler(ExtractXbc1, inputOption);

        rootCommand.AddCommand(extractAllCommand);
        rootCommand.AddCommand(extractFileCommand);
        rootCommand.AddCommand(extractHashCommand);
        rootCommand.AddCommand(hashListCommand);
        rootCommand.AddCommand(extractXbc);

        return await rootCommand.InvokeAsync(args);
    }

    private static void UnpackAll(FileInfo inputFile, FileInfo? outputDirInfo, bool noExtractXbc)
    {
        string outputDir;
        if (outputDirInfo is not null)
            outputDir = outputDirInfo.FullName;
        else if (inputFile.DirectoryName is not null)
            outputDir = Path.Combine(inputFile.DirectoryName, "extracted")!;
        else
            outputDir = "extracted";

        if (inputFile.FullName.EndsWith(".ard"))
        {
            _logger.LogError("Point to the .arh file, not the .ard file.");
            return;
        }

        var options = new ArchiveExtractorOptions()
        {
            ExtractAllExternalXbcs = !noExtractXbc,
        };
        using var archiveExtractor = ArchiveExtractor.Init(inputFile.FullName, options, _loggerFactory);
        if (archiveExtractor is null)
        {
            _logger.LogError("Failed to open arh/ard files.");
            return;
        }

        archiveExtractor.ExtractAll(outputDir);

        _logger.LogInformation("Done, extracted at '{outputDir}'", outputDir);
    }

    private static void UnpackFile(FileInfo inputFile, string gamePath, FileInfo? outputDirInfo, bool noExtractXbc)
    {
        string outputDir;
        if (outputDirInfo is not null)
            outputDir = outputDirInfo.FullName;
        else if (inputFile.DirectoryName is not null)
            outputDir = Path.Combine(inputFile.DirectoryName, "extracted")!;
        else
            outputDir = "extracted";

        if (inputFile.FullName.EndsWith(".ard"))
        {
            _logger.LogError("Point to the .arh file, not the .ard file.");
            return;
        }

        var options = new ArchiveExtractorOptions()
        {
            ExtractAllExternalXbcs = !noExtractXbc,
        };
        using var archiveExtractor = ArchiveExtractor.Init(inputFile.FullName, options, _loggerFactory);
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

        _logger.LogInformation("File extracted at {path}", Path.GetFullPath(outputFile));
    }

    private static void UnpackHash(FileInfo inputFile, string hashStr, FileInfo? outputDirInfo, bool noExtractXbc)
    {
        string outputDir;
        if (outputDirInfo is not null)
            outputDir = outputDirInfo.FullName;
        else if (inputFile.DirectoryName is not null)
            outputDir = Path.Combine(inputFile.DirectoryName, "extracted")!;
        else
            outputDir = "extracted";

        if (inputFile.FullName.EndsWith(".ard"))
        {
            _logger.LogError("Point to the .arh file, not the .ard file.");
            return;
        }

        var options = new ArchiveExtractorOptions()
        {
            ExtractAllExternalXbcs = !noExtractXbc,
        };
        using var archiveExtractor = ArchiveExtractor.Init(inputFile.FullName, options, _loggerFactory);
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

        _logger.LogInformation("File extracted at '{outputFile}'.", Path.GetFullPath(outputFile));
    }

    private static void ExtractXbc1(FileInfo inputFile)
    {
        string outputPath = inputFile.FullName + ".dec";

        using var inputStream = File.OpenRead(inputFile.FullName);
        using var outputStream = File.Create(outputPath);

        Xbc1.Decompress(inputStream, 0, outputStream);

        _logger.LogInformation("File extracted at {path}", Path.GetFullPath(outputPath));
    }

    private static void HashList(FileInfo inputFile)
    {
        using var archiveExtractor = ArchiveExtractor.Init(inputFile.FullName, loggerFactory: _loggerFactory);
        if (archiveExtractor is null)
        {
            _logger.LogError("Failed to open arh/ard files.");
            return;
        }

        string output;
        if (inputFile.DirectoryName is not null)
            output = Path.Combine(inputFile.DirectoryName, "hash_list.txt");
        else
            output = Path.Combine("hash_list.txt");

        archiveExtractor.CreateHashList(output);

        _logger.LogInformation("Hash list exported at {path}.", Path.GetFullPath(output));
    }
}
