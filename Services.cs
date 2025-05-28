namespace AIFlow.Cli.Services;

using System;
using System.IO;
using System.IO.Hashing;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIFlow.Cli;
using AIFlow.Cli.Models;
using AIFlow.Cli.Helpers;
using Microsoft.Extensions.FileSystemGlobbing;

public static class AIFlowConfigService
{
    public static readonly string ConfigFileName = "aiflow.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System
            .Text
            .Json
            .Serialization
            .JsonIgnoreCondition
            .WhenWritingNull,
    };

    public static AIFlowFile? LoadConfig(string path = ".")
    {
        var configPath = Path.Combine(path, ConfigFileName);
        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine(
                Program.GetLocalizedString(
                    "ErrorConfigNotFound",
                    ConfigFileName,
                    Path.GetFullPath(path)
                )
            );
            return null;
        }
        try
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<AIFlowFile>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                Program.GetLocalizedString("ErrorParsingConfig", ConfigFileName, ex.Message)
            );
            return null;
        }
    }

    public static bool SaveConfig(AIFlowFile config, string path = ".")
    {
        var configPath = Path.Combine(path, ConfigFileName);
        try
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(configPath, json);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                Program.GetLocalizedString("ErrorSavingConfig", ConfigFileName, ex.Message)
            );
            return false;
        }
    }
}

public static class BackupService
{
    public const string BackupsDirectoryName = ".aiflow_backups";
    private const string BackupInfoFileName = "backup_info.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string CreateBackup(
        AIFlowFile currentConfig,
        List<string> filesToBackupRelativePaths,
        string? relatedTaskId,
        string? aiChangesetId
    )
    {
        var timestamp = DateTime.UtcNow;
        var backupId = timestamp.ToString("yyyyMMddHHmmssfff");
        var backupDir = Path.Combine(
            Directory.GetCurrentDirectory(),
            BackupsDirectoryName,
            backupId
        );

        try
        {
            Directory.CreateDirectory(backupDir);

            var currentConfigPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                AIFlowConfigService.ConfigFileName
            );
            if (File.Exists(currentConfigPath))
            {
                File.Copy(
                    currentConfigPath,
                    Path.Combine(backupDir, AIFlowConfigService.ConfigFileName),
                    true
                );
            }

            var backedUpFilePathsInBackupDir = new List<string>();
            foreach (var relativePath in filesToBackupRelativePaths)
            {
                var sourceFullPath = FileService.GetFullPath(relativePath);
                if (File.Exists(sourceFullPath))
                {
                    var cleanRelativePath = relativePath.TrimStart(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar
                    );
                    var destPathInBackup = Path.Combine(backupDir, "files", cleanRelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPathInBackup)!);
                    File.Copy(sourceFullPath, destPathInBackup, true);
                    backedUpFilePathsInBackupDir.Add(cleanRelativePath);
                }
            }

            var backupInfo = new BackupInfo
            {
                BackupId = backupId,
                TimestampUtc = timestamp,
                RelatedTaskId = relatedTaskId,
                AiChangesetId = aiChangesetId,
                BackedUpFileRelativePaths = backedUpFilePathsInBackupDir,
                Notes =
                    $"Backup before integrating AI changes for task '{relatedTaskId ?? "N/A"}'.",
            };
            File.WriteAllText(
                Path.Combine(backupDir, BackupInfoFileName),
                JsonSerializer.Serialize(backupInfo, JsonOptions)
            );

            Console.WriteLine(
                Program.GetLocalizedString("BackupCreatedSuccessfully", backupId, backupDir)
            );
            return backupId;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                Program.GetLocalizedString("ErrorCreatingBackup", backupId, ex.Message)
            );
            if (Directory.Exists(backupDir))
                try
                {
                    Directory.Delete(backupDir, true);
                }
                catch { }
            return string.Empty;
        }
    }

    public static List<BackupInfo> ListBackups()
    {
        var backups = new List<BackupInfo>();
        var rootBackupDir = Path.Combine(Directory.GetCurrentDirectory(), BackupsDirectoryName);
        if (!Directory.Exists(rootBackupDir))
            return backups;

        foreach (var dir in Directory.GetDirectories(rootBackupDir))
        {
            var infoFilePath = Path.Combine(dir, BackupInfoFileName);
            if (File.Exists(infoFilePath))
            {
                try
                {
                    var info = JsonSerializer.Deserialize<BackupInfo>(
                        File.ReadAllText(infoFilePath),
                        JsonOptions
                    );
                    if (info != null)
                        backups.Add(info);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        Program.GetLocalizedString(
                            "ErrorReadingBackupInfo",
                            Path.GetFileName(dir),
                            ex.Message
                        )
                    );
                }
            }
        }
        return backups.OrderByDescending(b => b.TimestampUtc).ToList();
    }

    public static bool RestoreBackup(string backupId)
    {
        var backupDir = Path.Combine(
            Directory.GetCurrentDirectory(),
            BackupsDirectoryName,
            backupId
        );
        var backupInfoFilePath = Path.Combine(backupDir, BackupInfoFileName);

        if (!Directory.Exists(backupDir) || !File.Exists(backupInfoFilePath))
        {
            Console.Error.WriteLine(
                Program.GetLocalizedString("ErrorBackupNotFound", backupId)
            );
            return false;
        }

        try
        {
            var backupInfo = JsonSerializer.Deserialize<BackupInfo>(
                File.ReadAllText(backupInfoFilePath),
                JsonOptions
            );
            if (backupInfo == null)
            {
                Console.Error.WriteLine(
                    Program.GetLocalizedString("ErrorInvalidBackupInfo", backupId)
                );
                return false;
            }

            var backupConfigPath = Path.Combine(backupDir, AIFlowConfigService.ConfigFileName);
            var targetConfigPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                AIFlowConfigService.ConfigFileName
            );
            if (File.Exists(backupConfigPath))
            {
                File.Copy(backupConfigPath, targetConfigPath, true);
                Console.WriteLine(
                    Program.GetLocalizedString(
                        "RestoredFile",
                        AIFlowConfigService.ConfigFileName,
                        backupId
                    )
                );
            }
            else
            {
                Console.Error.WriteLine(
                    Program.GetLocalizedString(
                        "WarningFileNotFoundInBackup",
                        AIFlowConfigService.ConfigFileName,
                        backupId
                    )
                );
            }

            foreach (var relativePath in backupInfo.BackedUpFileRelativePaths)
            {
                var sourcePathInBackup = Path.Combine(backupDir, "files", relativePath);
                var targetPathInProject = FileService.GetFullPath(relativePath);

                if (File.Exists(sourcePathInBackup))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPathInProject)!);
                    File.Copy(sourcePathInBackup, targetPathInProject, true);
                    Console.WriteLine(
                        Program.GetLocalizedString("RestoredFile", relativePath, backupId)
                    );
                }
                else
                {
                    Console.Error.WriteLine(
                        Program.GetLocalizedString(
                            "WarningFileNotFoundInBackup",
                            relativePath,
                            backupId
                        )
                    );
                }
            }

            Console.WriteLine(
                Program.GetLocalizedString("RestoreCompletedSuccessfully", backupId)
            );
            Console.WriteLine(Program.GetLocalizedString("RestoreNextSteps"));
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                Program.GetLocalizedString("ErrorDuringRestore", backupId, ex.Message)
            );
            return false;
        }
    }
}


/// <summary>
/// Provides checksum calculation services.
/// </summary>
public static class ChecksumService
{
    /// <summary>
    /// Calculates the CRC32 checksum for the given byte array.
    /// </summary>
    /// <param name="data">The byte array to calculate the checksum for.</param>
    /// <returns>A hexadecimal string representation of the CRC32 checksum.</returns>
    public static string CalculateCRC32(byte[] data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }
        var crc32 = new Crc32();
        crc32.Append(data);
        var hashBytes = crc32.GetCurrentHash();
        // Convert byte array to hex string
        var sb = new StringBuilder();
        for (var i = 0; i < hashBytes.Length; i++)
        {
            sb.Append(hashBytes[i].ToString("x2"));
        }
        return sb.ToString();
    }
}

/// <summary>
/// Processes files to determine the appropriate payload representation for AIFlow.
/// </summary>
public class FileProcessorService
{
    // Configuration constants (can be moved to a config file or settings class)
    private const int MaxRawStringFileSizeKB = 32; // Max file size in KB for raw string embedding
    private const int MaxBase64ChunkSizeBytes = 1 * 1024 * 1024; // 1MB per chunk (for the Base64 string itself)
    private static readonly HashSet<string> BinaryFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".dll", ".exe", ".so", ".dylib", ".bin", ".data", // Executables and libraries
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp", // Images
            ".mp3", ".wav", ".ogg", ".aac", // Audio
            ".mp4", ".mov", ".avi", ".mkv", // Video
            ".zip", ".gz", ".tar", ".rar", ".7z", // Archives
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", // Documents (often binary or complex structure)
            ".o", ".obj", ".lib", ".a", // Object files
            ".pdb", // Program database
            ".dat", ".idx" // Generic data files often binary
        };

    /// <summary>
    /// Processes a file and returns its JSON payload representation.
    /// </summary>
    /// <param name="filePath">The full path to the file.</param>
    /// <returns>A JSON string representing the file payload.</returns>
    public async Task<string> ProcessFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            // Consider how to handle this error in the CLI (e.g., log and skip, or throw)
            Console.Error.WriteLine($"Error: File not found at '{filePath}'. Skipping.");
            // Returning null or an error JSON structure might be options.
            // For now, let's assume the CLI handles this by not generating a payload.
            return null; // Or throw new FileNotFoundException("File not found.", filePath);
        }

        var fileInfo = new FileInfo(filePath);
        var fileName = fileInfo.Name;
        var fileBytes = await File.ReadAllBytesAsync(filePath);

        FilePayloadBase payload;

        // 1. Determine if it's a binary file by extension
        var isBinaryByExtension = BinaryFileExtensions.Contains(fileInfo.Extension);

        // 2. Check for raw string candidacy
        if (!isBinaryByExtension && fileInfo.Length / 1024 <= MaxRawStringFileSizeKB)
        {
            var rawContent = Encoding.UTF8.GetString(fileBytes); // Assuming UTF-8 for text files
            if (IsValidForRawJsonString(rawContent))
            {
                payload = new RawContentPayload(fileName, filePath, rawContent);
                return JsonPayloadSerializer.Serialize(payload);
            }
        }

        // 3. If not raw, it's Base64 (either single or chunked)
        var overallChecksum = ChecksumService.CalculateCRC32(fileBytes); // Checksum of the original full file

        // Estimate Base64 length: (bytes / 3) * 4, rounded up to multiple of 4
        var estimatedBase64Length = (long)Math.Ceiling(fileBytes.Length / 3.0) * 4;

        if (estimatedBase64Length <= MaxBase64ChunkSizeBytes)
        {
            // Small enough for a single Base64 string
            var base64Content = Convert.ToBase64String(fileBytes);
            payload = new Base64Payload(fileName, filePath, base64Content);
        }
        else
        {
            // Needs chunking
            var chunks = new List<ChunkInfo>();
            var partNumber = 1;
            // Calculate how many original bytes correspond roughly to MaxBase64ChunkSizeBytes
            // Base64 expands data by 4/3. So, original data for one chunk is roughly MaxBase64ChunkSizeBytes * 3/4.
            var originalBytesPerChunk = MaxBase64ChunkSizeBytes * 3 / 4;

            for (var offset = 0; offset < fileBytes.Length; offset += originalBytesPerChunk)
            {
                var length = Math.Min(originalBytesPerChunk, fileBytes.Length - offset);
                var chunkBytes = new byte[length];
                Array.Copy(fileBytes, offset, chunkBytes, 0, length);

                var partChecksum = ChecksumService.CalculateCRC32(chunkBytes);
                var base64ChunkData = Convert.ToBase64String(chunkBytes);

                chunks.Add(new ChunkInfo(partNumber++, base64ChunkData, partChecksum));
            }
            payload = new ChunkedBase64Payload(fileName, filePath, chunks.Count, overallChecksum, chunks);
        }

        return JsonPayloadSerializer.Serialize(payload);
    }

    /// <summary>
    /// Checks if the string content is suitable for direct embedding in a JSON string.
    /// This is a basic check for control characters (0x00-0x1F) excluding TAB, LF, CR.
    /// More sophisticated checks might be needed for complex cases or specific JSON parsers.
    /// </summary>
    private bool IsValidForRawJsonString(string content)
    {
        foreach (var c in content)
        {
            if (c < 0x20 && c != '\t' && c != '\n' && c != '\r')
            {
                // Contains a control character other than tab, newline, or carriage return
                return false;
            }
        }
        // Consider also checking for very high density of quotes or backslashes if that's a concern.
        // For this implementation, the control character check is the primary heuristic.
        return true;
    }
}


public static class FileService
{
    public static string? CalculateFileHash(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }
        try
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = sha256.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                Program.GetLocalizedString("ErrorCalculatingHash", filePath, ex.Message)
            );
            return null;
        }
    }

    public static string Base64Encode(string plainText) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));

    public static string Base64Decode(string base64EncodedData)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(base64EncodedData));
        }
        catch (FormatException ex)
        {
            Console.Error.WriteLine(
                Program.GetLocalizedString("ErrorDecodingBase64", ex.Message)
            );
            return string.Empty;
        }
    }

    public static string GetProjectRelativePath(string fullPath)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        if (
            !currentDirectory.EndsWith(Path.DirectorySeparatorChar.ToString())
            && !currentDirectory.EndsWith(Path.AltDirectorySeparatorChar.ToString())
        )
        {
            currentDirectory += Path.DirectorySeparatorChar;
        }
        var projectUri = new Uri(currentDirectory);
        var fileUri = new Uri(Path.GetFullPath(fullPath));

        if (projectUri.IsBaseOf(fileUri))
        {
            return Uri.UnescapeDataString(
                projectUri
                    .MakeRelativeUri(fileUri)
                    .ToString()
                    .Replace(Path.DirectorySeparatorChar, '/')
            );
        }
        return fullPath.Replace(Path.DirectorySeparatorChar, '/');
    }

    public static string GetFullPath(string projectRelativePath)
    {
        return Path.GetFullPath(projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}

public static class IgnoreService
{
    public const string IgnoreFileName = ".aiflowignore";

    private static readonly List<string> DefaultIgnorePatterns = new List<string>
        {
            BackupService.BackupsDirectoryName + "/",
            ".git/",
            ".vs/",
            ".vscode/",
            "bin/",
            "obj/",
            "**/bin/",
            "**/obj/",
            "*.lock",
            "*.suo",
            "*.user",
            AIFlowConfigService.ConfigFileName,
        };

    public static Matcher LoadIgnoreMatcher(string projectRootPath)
    {
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        foreach (var p in DefaultIgnorePatterns)
        {
            matcher.AddExclude(p);
        }

        var ignoreFilePath = Path.Combine(projectRootPath, IgnoreFileName);
        if (File.Exists(ignoreFilePath))
        {
            var patterns = File.ReadAllLines(ignoreFilePath)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                .ToList();

            foreach (var pattern in patterns)
            {
                if (pattern.StartsWith("!"))
                {
                    matcher.AddInclude(pattern.Substring(1));
                }
                else
                {
                    matcher.AddExclude(pattern);
                }
            }
        }
        return matcher;
    }

    public static bool IsFileIgnored(string relativeFilePath, Matcher matcher)
    {
        var normalizedPath = relativeFilePath.Replace(Path.DirectorySeparatorChar, '/');
        var result = matcher.Match(normalizedPath);
        return result.HasMatches;
    }

    public static bool IsFileIgnored(string relativeFilePath, string projectRootPath)
    {
        var matcher = LoadIgnoreMatcher(projectRootPath);
        return IsFileIgnored(relativeFilePath, matcher);
    }
}

