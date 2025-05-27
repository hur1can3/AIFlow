namespace AIFlow.Cli.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Extensions.FileSystemGlobbing;

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
}
