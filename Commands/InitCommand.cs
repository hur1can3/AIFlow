namespace AIFlow.Cli.Commands
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using AIFlow.Cli.Models;
    using AIFlow.Cli.Services;

    public static class InitCommand
    {
        static Option<string?> templateNameOption = new Option<string?>(
            aliases: new[] { "--template", "-t" },
            description: Program.GetLocalizedString("InitTemplateNameOptionDescription")
        );

        static Option<string?> templateFileOption = new Option<string?>(
            aliases: new[] { "--template-file" },
            getDefaultValue: () => "aiflow_templates.json",
            description: Program.GetLocalizedString("InitTemplateFileOptionDescription")
        );

        public static Command Create()
        {
            var command = new Command("init", Program.GetLocalizedString("InitCommandDescription"));
            command.AddOption(templateNameOption);
            command.AddOption(templateFileOption);

            command.SetHandler(
                (InvocationContext context) =>
                {
                    var templateName = context.ParseResult.GetValueForOption(templateNameOption);
                    var templateFilePath = context.ParseResult.GetValueForOption(
                        templateFileOption
                    );
                    HandleInit(templateName, templateFilePath);
                }
            );
            return command;
        }

        private static void HandleInit(string? templateName, string? templateFilePath)
        {
            Console.WriteLine(Program.GetLocalizedString("InitializingAIFlowProject"));
            var projectRoot = Directory.GetCurrentDirectory();
            var configFilePath = Path.Combine(projectRoot, AIFlowConfigService.ConfigFileName);

            if (File.Exists(configFilePath))
            {
                Console.WriteLine(
                    Program.GetLocalizedString(
                        "AIFlowProjectAlreadyExists",
                        AIFlowConfigService.ConfigFileName
                    )
                );
                return;
            }

            AIFlowFile aiflowConfig;

            if (!string.IsNullOrEmpty(templateName) && !string.IsNullOrEmpty(templateFilePath))
            {
                aiflowConfig = LoadFromTemplate(projectRoot, templateName, templateFilePath);
                if (aiflowConfig == null)
                {
                    Console.WriteLine(Program.GetLocalizedString("InitFallingBackToDefault"));
                    aiflowConfig = new AIFlowFile
                    {
                        ProjectName = Path.GetFileName(projectRoot) ?? "MyAIFlowProject",
                    };
                }
                else
                {
                    Console.WriteLine(
                        Program.GetLocalizedString("InitAppliedTemplate", templateName)
                    );
                }
            }
            else
            {
                aiflowConfig = new AIFlowFile
                {
                    ProjectName = Path.GetFileName(projectRoot) ?? "MyAIFlowProject",
                };
            }

            if (AIFlowConfigService.SaveConfig(aiflowConfig, projectRoot))
            {
                Console.WriteLine(
                    Program.GetLocalizedString(
                        "AIFlowProjectInitialized",
                        AIFlowConfigService.ConfigFileName
                    )
                );
                Console.WriteLine(Program.GetLocalizedString("DefaultBranchIsDevelop"));

                var ignoreFilePath = Path.Combine(projectRoot, IgnoreService.IgnoreFileName);
                if (!File.Exists(ignoreFilePath))
                {
                    try
                    {
                        File.WriteAllText(
                            ignoreFilePath,
                            Program.GetLocalizedString("SampleAIFlowIgnoreContent")
                        );
                        Console.WriteLine(
                            Program.GetLocalizedString(
                                "SampleAIFlowIgnoreCreated",
                                IgnoreService.IgnoreFileName
                            )
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(
                            Program.GetLocalizedString(
                                "ErrorCreatingSampleIgnoreFile",
                                IgnoreService.IgnoreFileName,
                                ex.Message
                            )
                        );
                    }
                }
            }
            else
            {
                Console.Error.WriteLine(
                    Program.GetLocalizedString("FailedToInitializeAIFlowProject")
                );
            }
        }

        private static AIFlowFile? LoadFromTemplate(
            string projectRoot,
            string templateName,
            string templateFilePath
        )
        {
            string fullTemplatePath = Path.GetFullPath(templateFilePath);
            if (!File.Exists(fullTemplatePath))
            {
                Console.Error.WriteLine(
                    Program.GetLocalizedString("InitErrorTemplateFileNotFound", templateFilePath)
                );
                return null;
            }

            try
            {
                var templateJson = File.ReadAllText(fullTemplatePath);
                var templateCollection = JsonSerializer.Deserialize<FlowTemplateCollection>(
                    templateJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                var selectedTemplate = templateCollection?.Templates.FirstOrDefault(t =>
                    t.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase)
                );
                if (selectedTemplate == null)
                {
                    Console.Error.WriteLine(
                        Program.GetLocalizedString(
                            "InitErrorTemplateNotFoundInFile",
                            templateName,
                            templateFilePath
                        )
                    );
                    return null;
                }

                var newConfig = new AIFlowFile
                {
                    ProjectName = Path.GetFileName(projectRoot) ?? $"ProjectFrom_{templateName}",
                    CurrentBranch = "develop", // Default, can be overridden by template.ConfigOverrides
                    Roadmap = new List<string>(
                        selectedTemplate.InitialRoadmap ?? Enumerable.Empty<string>()
                    ),
                    Config = new AIFlowConfigSettings(),
                    NextTaskId = 1,
                    NextHumanRequestGroupId = 101,
                };

                if (selectedTemplate.ConfigOverrides != null)
                {
                    if (!string.IsNullOrEmpty(selectedTemplate.ConfigOverrides.CurrentBranch))
                        newConfig.CurrentBranch = selectedTemplate.ConfigOverrides.CurrentBranch;
                    if (selectedTemplate.ConfigOverrides.MaxRequestPayloadSizeBytes > 0)
                        newConfig.Config.MaxRequestPayloadSizeBytes = selectedTemplate
                            .ConfigOverrides
                            .MaxRequestPayloadSizeBytes;
                    if (selectedTemplate.ConfigOverrides.MaxAiSingleMessageSizeBytes > 0)
                        newConfig.Config.MaxAiSingleMessageSizeBytes = selectedTemplate
                            .ConfigOverrides
                            .MaxAiSingleMessageSizeBytes;
                    if (selectedTemplate.ConfigOverrides.ApproxMaxAiContextTokens > 0)
                        newConfig.Config.ApproxMaxAiContextTokens = selectedTemplate
                            .ConfigOverrides
                            .ApproxMaxAiContextTokens;
                }

                int taskIdCounter = newConfig.NextTaskId;
                foreach (var taskStub in selectedTemplate.InitialTasks)
                {
                    var newTask = new AIFlowTask
                    {
                        TaskId = taskStub.TaskId ?? $"task_{taskIdCounter++}",
                        Description = taskStub.Description,
                        Branch = taskStub.Branch ?? newConfig.CurrentBranch,
                        Status = taskStub.Status ?? TaskStatus.ToDo,
                        AssignedTo = taskStub.AssignedTo ?? "ai",
                        Type = taskStub.Type,
                        StoryPoints = taskStub.StoryPoints,
                        Priority = taskStub.Priority,
                        Sprint = taskStub.Sprint,
                        EpicLink = taskStub.EpicLink,
                        DueDate =
                            !string.IsNullOrEmpty(taskStub.DueDate)
                            && DateTime.TryParse(taskStub.DueDate, out var dt)
                                ? dt
                                : (DateTime?)null,
                        Labels = taskStub.Labels?.ToList() ?? new List<string>(),
                        HumanNotes = taskStub.HumanNotes,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        RelatedResources = new List<AIFlowTaskRelatedResource>(),
                    };
                    if (taskStub.RelatedResources != null)
                    {
                        foreach (var resStub in taskStub.RelatedResources)
                        {
                            newTask.RelatedResources.Add(
                                new AIFlowTaskRelatedResource
                                {
                                    Path = resStub.Path,
                                    Type = resStub.Type,
                                    Action = resStub.Action,
                                }
                            );
                        }
                    }
                    newConfig.Tasks.Add(newTask);
                }
                newConfig.NextTaskId = taskIdCounter;

                foreach (var resourceStub in selectedTemplate.InitialResources)
                {
                    var newResource = new AIFlowResource
                    {
                        Path = resourceStub.Path,
                        Type = resourceStub.Type,
                        Status = resourceStub.Status ?? ResourceStatus.Unmodified,
                        Notes = resourceStub.Notes,
                    };
                    if (
                        resourceStub.Type == ResourceType.LocalFile
                        && !string.IsNullOrEmpty(resourceStub.Path)
                    )
                    {
                        var fullResourcePath = FileService.GetFullPath(
                            Path.Combine(projectRoot, resourceStub.Path)
                        ); // Ensure path is relative to project root
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(fullResourcePath)!);
                            File.WriteAllText(
                                fullResourcePath,
                                resourceStub.InitialContent
                                    ?? $"# Placeholder for {resourceStub.Path}{Environment.NewLine}"
                            );
                            newResource.LocalHash =
                                FileService.CalculateFileHash(fullResourcePath) ?? string.Empty;
                            Console.WriteLine(
                                Program.GetLocalizedString(
                                    "InitCreatedPlaceholderResource",
                                    resourceStub.Path
                                )
                            );
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine(
                                Program.GetLocalizedString(
                                    "InitErrorCreatingPlaceholderResource",
                                    resourceStub.Path,
                                    ex.Message
                                )
                            );
                        }
                    }
                    newConfig.Resources.Add(newResource);
                }
                return newConfig;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    Program.GetLocalizedString(
                        "InitErrorProcessingTemplateFile",
                        templateFilePath,
                        ex.Message
                    )
                );
                return null;
            }
        }
    }
}
