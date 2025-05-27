namespace AIFlow.Cli.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using AIFlow.Cli.Models;

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
}
