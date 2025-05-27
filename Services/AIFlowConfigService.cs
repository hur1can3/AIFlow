namespace AIFlow.Cli.Services
{
    using System;
    using System.IO;
    using System.Text.Json;
    using AIFlow.Cli.Models;

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
}
