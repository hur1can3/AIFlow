namespace AIFlow.Cli.Commands
{
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using AIFlow.Cli.Models;
    using AIFlow.Cli.Services;

    public static class RevertIntegrationCommand
    {
        static Option<bool> listOption = new Option<bool>(
            aliases: new[] { "--list", "-l" },
            description: Program.GetLocalizedString("RevertListOptionDescription")
        );
        static Option<string?> idOption = new Option<string?>(
            aliases: new[] { "--id" },
            description: Program.GetLocalizedString("RevertIdOptionDescription")
        );
        static Option<bool> lastOption = new Option<bool>(
            aliases: new[] { "--last" },
            description: Program.GetLocalizedString("RevertLastOptionDescription")
        );
        static Option<bool> forceOption = new Option<bool>(
            aliases: new[] { "--force", "-f" },
            description: Program.GetLocalizedString("RevertForceOptionDescription")
        );

        public static Command Create()
        {
            var command = new Command(
                "revert",
                Program.GetLocalizedString("RevertCommandDescription")
            );
            command.AddOption(listOption);
            command.AddOption(idOption);
            command.AddOption(lastOption);
            command.AddOption(forceOption);
            command.SetHandler(
                (InvocationContext context) =>
                {
                    var list = context.ParseResult.GetValueForOption<bool>(listOption);
                    var backupId = context.ParseResult.GetValueForOption(idOption);
                    var useLast = context.ParseResult.GetValueForOption(lastOption);
                    var force = context.ParseResult.GetValueForOption(forceOption);
                    HandleRevert(list, backupId, useLast, force);
                }
            );
            return command;
        }

        private static void HandleRevert(bool list, string? backupId, bool useLast, bool force)
        {
            if (list)
            {
                ListAvailableBackups();
                return;
            }
            string? targetBackupId = null;
            var availableBackups = BackupService.ListBackups();
            if (useLast)
            {
                targetBackupId = availableBackups.FirstOrDefault()?.BackupId;
                if (targetBackupId == null)
                {
                    Console.Error.WriteLine(
                        Program.GetLocalizedString("RevertErrorNoBackupsFound")
                    );
                    return;
                }
                Console.WriteLine(
                    Program.GetLocalizedString("RevertSelectedLastBackup", targetBackupId)
                );
            }
            else if (!string.IsNullOrEmpty(backupId))
            {
                if (availableBackups.Any(b => b.BackupId == backupId))
                {
                    targetBackupId = backupId;
                }
                else
                {
                    Console.Error.WriteLine(
                        Program.GetLocalizedString("RevertErrorSpecificBackupNotFound", backupId)
                    );
                    ListAvailableBackups();
                    return;
                }
            }
            else
            {
                Console.Error.WriteLine(Program.GetLocalizedString("RevertSpecifyIdOrLast"));
                ListAvailableBackups();
                return;
            }
            if (targetBackupId == null)
                return;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(Program.GetLocalizedString("RevertWarning", targetBackupId));
            Console.ResetColor();
            if (!force)
            {
                Console.Write(Program.GetLocalizedString("PromptConfirmRevert"));
                string? confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (confirm != "yes")
                {
                    Console.WriteLine(Program.GetLocalizedString("RevertAbortedByUser"));
                    return;
                }
            }
            if (BackupService.RestoreBackup(targetBackupId))
            {
                Console.WriteLine(Program.GetLocalizedString("RevertSuccess", targetBackupId));
            }
            else
            {
                Console.Error.WriteLine(Program.GetLocalizedString("RevertFailed", targetBackupId));
            }
        }

        private static void ListAvailableBackups()
        {
            var backups = BackupService.ListBackups();
            if (!backups.Any())
            {
                Console.WriteLine(Program.GetLocalizedString("RevertNoBackupsAvailable"));
                return;
            }
            Console.WriteLine(Program.GetLocalizedString("RevertAvailableBackupsHeader"));
            foreach (var backup in backups)
            {
                Console.WriteLine(
                    $"  ID: {backup.BackupId, -20} Timestamp: {backup.TimestampUtc.ToLocalTime(), -25} Task: {backup.RelatedTaskId ?? "N/A", -15} Notes: {backup.Notes}"
                );
            }
        }
    }
}
