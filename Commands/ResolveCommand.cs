namespace AIFlow.Cli.Commands
{
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.IO;
    using System.Linq;
    using AIFlow.Cli.Models;
    using AIFlow.Cli.Services;

    public static class ResolveCommand
    {
        private static readonly ResolveCommandHandler s_resolveCommandHandler = new();
        private static Argument<string> s_resourcePathArgument = new Argument<string>(
            "resource-path",
            Program.GetLocalizedString("ResolveResourcePathArgumentDescription")
        );

        public static Command Create()
        {
            var command = new Command(
                "resolve",
                Program.GetLocalizedString("ResolveCommandDescription")
            )
            {
                s_resourcePathArgument,
            };

            var resourcePathArgument = command.Arguments.OfType<Argument<string>>().First();
            command.Handler = s_resolveCommandHandler;

            return command;
        }

        private class ResolveCommandHandler : ICommandHandler
        {
            public int Invoke(InvocationContext context)
            {
                throw new NotImplementedException();
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var resourcePath = context.ParseResult.GetValueForArgument<string>(
                    s_resourcePathArgument
                );
                return await HandleResolveAsync(resourcePath);
            }
        }

        private static async Task<int> HandleResolveAsync(string relativeResourcePath)
        {
            var config = AIFlowConfigService.LoadConfig();
            if (config == null)
                return 0;
            var normalizedRelativePath = FileService.GetProjectRelativePath(relativeResourcePath);
            var trackedResource = config.Resources.FirstOrDefault(r =>
                r.Path.Equals(normalizedRelativePath, StringComparison.OrdinalIgnoreCase)
            );
            if (trackedResource == null)
            {
                Console.Error.WriteLine(
                    Program.GetLocalizedString(
                        "ResolveErrorResourceNotFoundInAIFlow",
                        normalizedRelativePath
                    )
                );
                return 0;
            }
            if (trackedResource.Status != ResourceStatus.NeedsManualMerge)
            {
                Console.WriteLine(
                    Program.GetLocalizedString(
                        "ResolveResourceNotMarkedForMerge",
                        normalizedRelativePath,
                        trackedResource.Status
                    )
                );
                return 0;
            }
            string fullOriginalPath = FileService.GetFullPath(trackedResource.Path);
            if (trackedResource.Type == ResourceType.LocalFile && !File.Exists(fullOriginalPath))
            {
                Console.Error.WriteLine(
                    Program.GetLocalizedString(
                        "ResolveErrorOriginalFileNotFound",
                        trackedResource.Path
                    )
                );
                return 0;
            }
            Console.WriteLine(
                Program.GetLocalizedString("ResolveConfirmingResolution", trackedResource.Path)
            );
            Console.WriteLine(
                Program.GetLocalizedString(
                    "ResolveAssumingUserResolvedInOriginal",
                    trackedResource.Path
                )
            );
            string? newHash =
                (trackedResource.Type == ResourceType.LocalFile)
                    ? FileService.CalculateFileHash(fullOriginalPath)
                    : trackedResource.LocalHash;
            if (trackedResource.Type == ResourceType.LocalFile && newHash == null)
            {
                Console.Error.WriteLine(
                    Program.GetLocalizedString(
                        "ResolveErrorCalculatingNewHash",
                        trackedResource.Path
                    )
                );
                return 0;
            }
            if (trackedResource.Type == ResourceType.LocalFile)
            {
                string resolvedContent = File.ReadAllText(fullOriginalPath);
                if (
                    resolvedContent.Contains("<<<<<<<")
                    && resolvedContent.Contains("=======")
                    && resolvedContent.Contains(">>>>>>>")
                )
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(
                        Program.GetLocalizedString(
                            "WarningConflictMarkersStillPresent",
                            trackedResource.Path
                        )
                    );
                    Console.ResetColor();
                    Console.Write(Program.GetLocalizedString("PromptProceedAnyway"));
                    string? proceed = Console.ReadLine()?.Trim().ToLowerInvariant();
                    if (proceed != "y")
                    {
                        Console.WriteLine(Program.GetLocalizedString("ResolveAborted"));
                        return 0;
                    }
                }
            }
            trackedResource.LocalHash = newHash ?? trackedResource.LocalHash;
            trackedResource.Status = ResourceStatus.Merged;
            trackedResource.ConflictResolutionFile = null;
            trackedResource.LastSentToAiHash = null;
            if (AIFlowConfigService.SaveConfig(config))
            {
                Console.WriteLine(
                    Program.GetLocalizedString(
                        "ResolveSuccess",
                        trackedResource.Path,
                        trackedResource.Status
                    )
                );
                return 1;
            }
            else
            {
                Console.Error.WriteLine(
                    Program.GetLocalizedString("ResolveErrorSavingConfig", trackedResource.Path)
                );
                return 0;
            }
        }
    }
}
