// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using System.Text;
using System.Text.Json;
using AIFlow.Cli.Models;
using AIFlow.Cli.Services;
using Microsoft.Extensions.FileSystemGlobbing;
using TaskStatus = AIFlow.Cli.Models.TaskStatus;

namespace AIFlow.Cli.Commands;

public static class BranchCommand
{
    public static Command Create()
    {
        var command = new Command(
            "branch",
            Program.GetLocalizedString("BranchCommandDescription")
        )
            {
                new Argument<string?>(
                    "branch-name",
                    () => null,
                    Program.GetLocalizedString("BranchNameArgumentDescription")
                ),
                new Option<bool>(
                    aliases: new[] { "--delete", "-d" },
                    description: "Conceptually delete a branch (not implemented, branches are dynamic based on tasks)."
                ),
            };
        var branchNameArgument = command.Arguments.OfType<Argument<string?>>().First();
        var deleteOption = command.Options.OfType<Option<bool>>().First(o => o.HasAlias("-d"));
        command.SetHandler(
            async (branchName, delete) =>
            {
                var config = AIFlowConfigService.LoadConfig();
                if (config == null)
                    return;

                if (delete)
                {
                    Console.WriteLine(Program.GetLocalizedString("BranchDeleteNotImplemented"));
                    return;
                }

                if (string.IsNullOrEmpty(branchName))
                {
                    Console.WriteLine(Program.GetLocalizedString("BranchListingBranches"));
                    var branches = new HashSet<string> { "main", "develop" };
                    foreach (var taskBranch in config.Tasks.Select(t => t.Branch).Distinct())
                    {
                        branches.Add(taskBranch);
                    }
                    foreach (var b in branches.OrderBy(b => b))
                    {
                        Console.WriteLine($"  {(b == config.CurrentBranch ? "" : " ")} {b}");
                    }
                }
                else
                {
                    if (
                        !branchName.StartsWith("feature/")
                        && !branchName.StartsWith("bugfix/")
                        && !branchName.StartsWith("release/")
                        && !branchName.StartsWith("hotfix/")
                        && branchName != "main"
                        && branchName != "develop"
                    )
                    {
                        Console.WriteLine(
                            Program.GetLocalizedString("BranchGitflowSuggestion", branchName)
                        );
                    }
                    Console.WriteLine(
                        Program.GetLocalizedString("BranchCreationNote", branchName)
                    );
                    Console.WriteLine(
                        Program.GetLocalizedString("UseCheckoutToSwitch", branchName)
                    );
                }

                await Task.CompletedTask; // Ensure all code paths return a Task
            },
            branchNameArgument,
            deleteOption
        );
        return command;
    }
}

public static class CheckoutCommand
{
    public static Command Create()
    {
        var command = new Command(
            "checkout",
            Program.GetLocalizedString("CheckoutCommandDescription")
        )
            {
                new Argument<string>(
                    "branch-name",
                    Program.GetLocalizedString("CheckoutBranchNameArgumentDescription")
                ),
            };
        var branchNameArgument = command.Arguments.OfType<Argument<string>>().First();
        command.SetHandler(
            (branchName) =>
            {
                var config = AIFlowConfigService.LoadConfig();
                if (config == null)
                    return;
                config.CurrentBranch = branchName;
                if (AIFlowConfigService.SaveConfig(config))
                    Console.WriteLine(
                        Program.GetLocalizedString("CheckoutSuccess", branchName)
                    );
                else
                    Console.Error.WriteLine(
                        Program.GetLocalizedString("CheckoutFailed", branchName)
                    );
            },
            branchNameArgument
        );
        return command;
    }
}

public static class FetchOutputCommand
{
    public static Command Create()
    {
        var command = new Command(
            "fetch-output",
            Program.GetLocalizedString("FetchOutputCommandDescription")
        )
            {
                new Option<string>(
                    aliases: new[] { "--retrieval-id", "-rid" },
                    description: Program.GetLocalizedString(
                        "FetchOutputRetrievalIdOptionDescription"
                    )
                )
                {
                    IsRequired = true,
                },
                new Option<int>(
                    aliases: new[] { "--batch", "-b" },
                    description: Program.GetLocalizedString("FetchOutputBatchOptionDescription")
                )
                {
                    IsRequired = true,
                },
            };
        command.SetHandler(
            async (retrievalId, batchNumber) =>
            {
                var config = AIFlowConfigService.LoadConfig();
                if (config?.ActiveAiRetrievalSession?.RetrievalGuid != retrievalId)
                {
                    Console.Error.WriteLine(
                        Program.GetLocalizedString("ErrorNoActiveRetrievalSession", retrievalId)
                    );
                    return; // Ensure the lambda exits early in this case
                }

                Console.WriteLine(Program.GetLocalizedString("FetchOutputInstructionTitle"));
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine(
                    Program.GetLocalizedString("FetchOutputAIMessage", batchNumber, retrievalId)
                );
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine(Program.GetLocalizedString("FetchOutputNextStep"));

                await Task.CompletedTask; // Ensure the lambda returns a Task
            },
            command.Options.OfType<Option<string>>().First(),
            command.Options.OfType<Option<int>>().First()
        );
        return command;
    }
}


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
            (context) =>
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
        var fullTemplatePath = Path.GetFullPath(templateFilePath);
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

            var taskIdCounter = newConfig.NextTaskId;
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
                            : null,
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

public static class IntegrateOutputCommand
{
    static Argument<string> aiJsonPayloadArgument = new Argument<string>(
        "ai-json-payload",
        Program.GetLocalizedString("IntegrateOutputPayloadArgumentDescription")
    );

    static Option<bool> dryRunOption = new Option<bool>(
        aliases: new[] { "--dry-run", "-dr" },
        description: Program.GetLocalizedString("DryRunOptionDescription")
    );

    public static Command Create()
    {
        var command = new Command(
            "integrate-output",
            Program.GetLocalizedString("IntegrateOutputCommandDescription")
        )
            {
                aiJsonPayloadArgument,
                dryRunOption,
            };

        command.SetHandler(
            (context) =>
            {
                var aiJsonPayload = context.ParseResult.GetValueForArgument(
                    aiJsonPayloadArgument
                );
                var isDryRun = context.ParseResult.GetValueForOption(dryRunOption);
                HandleIntegrateOutput(aiJsonPayload, isDryRun);
            }
        );
        return command;
    }

    private static void HandleIntegrateOutput(string aiJsonPayload, bool isDryRun)
    {
        if (isDryRun)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(Program.GetLocalizedString("DryRunModeActive"));
            Console.ResetColor();
        }
        var config = AIFlowConfigService.LoadConfig();
        if (config == null)
            return;
        var configForSimulation = isDryRun
            ? JsonSerializer.Deserialize<AIFlowFile>(JsonSerializer.Serialize(config))!
            : config;
        AiPreliminaryResponse? prelimResponse = null;
        AiOutputPackage? mainOutputPackage = null;
        AiBatchResponse? batchResponse = null;
        var changesWouldBeAppliedOrMergeFileCreated = false;
        var successfullyParsed = false;
        try
        {
            var prelimWrapper = JsonSerializer.Deserialize<AiPreliminaryResponseWrapper>(
                aiJsonPayload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
            if (prelimWrapper?.AiPreliminaryResponse?.RetrievalGuid != null)
            {
                prelimResponse = prelimWrapper.AiPreliminaryResponse;
                successfullyParsed = true;
            }
        }
        catch { }
        if (!successfullyParsed)
        {
            try
            {
                var batchWrapper = JsonSerializer.Deserialize<AiBatchResponseWrapper>(
                    aiJsonPayload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
                if (
                    batchWrapper?.AiBatchResponse?.RetrievalGuid != null
                    && batchWrapper.AiBatchResponse.Payload != null
                )
                {
                    batchResponse = batchWrapper.AiBatchResponse;
                    mainOutputPackage = batchResponse.Payload;
                    mainOutputPackage.RetrievalGuid = batchResponse.RetrievalGuid;
                    mainOutputPackage.BatchNumber = batchResponse.BatchNumber;
                    mainOutputPackage.TotalBatches = batchResponse.TotalBatches;
                    mainOutputPackage.IsLastBatch = batchResponse.IsLastBatch;
                    successfullyParsed = true;
                }
            }
            catch { }
        }
        if (!successfullyParsed)
        {
            try
            {
                var directWrapper = JsonSerializer.Deserialize<AiOutputPackageWrapper>(
                    aiJsonPayload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
                if (directWrapper?.AiOutputPackage?.AiChangesetId != null)
                {
                    mainOutputPackage = directWrapper.AiOutputPackage;
                    successfullyParsed = true;
                }
                else
                {
                    mainOutputPackage = JsonSerializer.Deserialize<AiOutputPackage>(
                        aiJsonPayload,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                    if (mainOutputPackage?.AiChangesetId != null)
                        successfullyParsed = true;
                }
            }
            catch { }
        }
        if (!successfullyParsed)
        {
            try
            {
                using var doc = JsonDocument.Parse(aiJsonPayload);
                var root = doc.RootElement;
                var hasAiChangesetId =
                    root.TryGetProperty("aiChangesetId", out _)
                    ||
                        root.TryGetProperty("aiOutputPackage", out var pkg)
                        && pkg.TryGetProperty("aiChangesetId", out _)

                    ||
                        root.TryGetProperty("aiBatchResponse", out var batch)
                        && batch.TryGetProperty("aiChangesetId", out _)

                    ||
                        root.TryGetProperty("aiPreliminaryResponse", out var prelim)
                        && prelim.TryGetProperty("aiChangesetId", out _)
                    ;
                var hasFileChanges =
                    root.TryGetProperty("fileChanges", out var fc)
                        && fc.ValueKind == JsonValueKind.Array
                    ||
                        root.TryGetProperty("aiOutputPackage", out pkg)
                        && pkg.TryGetProperty("fileChanges", out fc)
                        && fc.ValueKind == JsonValueKind.Array

                    ||
                        root.TryGetProperty("aiBatchResponse", out batch)
                        && batch.TryGetProperty("payload", out var batchPayload)
                        && batchPayload.TryGetProperty("fileChanges", out fc)
                        && fc.ValueKind == JsonValueKind.Array
                    ;
                if (hasAiChangesetId || hasFileChanges)
                {
                    Console.Error.WriteLine(
                        Program.GetLocalizedString("ErrorAIJsonValidButUnrecognizedStructure")
                    );
                }
                else
                {
                    Console.Error.WriteLine(
                        Program.GetLocalizedString("ErrorAIJsonValidButNotAIFlowFormat")
                    );
                }
            }
            catch (JsonException jsonEx)
            {
                Console.Error.WriteLine(
                    Program.GetLocalizedString("ErrorAIJsonMalformed", jsonEx.Message)
                );
            }
            return;
        }
        if (prelimResponse != null)
        {
            Console.WriteLine(
                Program.GetLocalizedString(
                    "AIOutputIsBatched",
                    prelimResponse.NumberOfBatches,
                    prelimResponse.RetrievalGuid
                )
            );
            if (!isDryRun)
            {
                configForSimulation.ActiveAiRetrievalSession = new ActiveRetrievalSession
                {
                    RetrievalGuid = prelimResponse.RetrievalGuid,
                    AiChangesetId = prelimResponse.AiChangesetId,
                    TotalBatches = prelimResponse.NumberOfBatches,
                    ReceivedBatchesCount = 0,
                    ReceivedBatchPayloads = new List<string>(),
                };
                AIFlowConfigService.SaveConfig(configForSimulation);
            }
            else
            {
                Console.WriteLine(Program.GetLocalizedString("DryRunWouldUpdateActiveSession"));
            }
            Console.WriteLine(
                Program.GetLocalizedString(
                    "RunFetchOutputCommand",
                    prelimResponse.RetrievalGuid,
                    1
                )
            );
            return;
        }
        if (mainOutputPackage == null)
        {
            Console.Error.WriteLine(Program.GetLocalizedString("ErrorInvalidAIJsonStructure"));
            return;
        }
        AIFlowTask? task = null;
        if (mainOutputPackage.TaskUpdates?.TaskId != null)
        {
            task = configForSimulation.Tasks.FirstOrDefault(t =>
                t.TaskId == mainOutputPackage.TaskUpdates.TaskId
            );
        }
        else if (!string.IsNullOrEmpty(mainOutputPackage.HumanRequestId))
        {
            task = configForSimulation.Tasks.FirstOrDefault(t =>
                t.TaskId == mainOutputPackage.HumanRequestId
                || t.HumanRequestGroupId == mainOutputPackage.HumanRequestId
            );
        }
        if (
            task == null
            && configForSimulation.ActiveAiRetrievalSession?.RetrievalGuid
                != mainOutputPackage.RetrievalGuid
        )
        {
            Console.Error.WriteLine(
                Program.GetLocalizedString(
                    "ErrorTaskNotFoundForAIResponse",
                    mainOutputPackage.TaskUpdates?.TaskId
                        ?? mainOutputPackage.HumanRequestId
                        ?? "N/A"
                )
            );
            return;
        }
        if (
            mainOutputPackage.RetrievalGuid != null
            && configForSimulation.ActiveAiRetrievalSession?.RetrievalGuid
                == mainOutputPackage.RetrievalGuid
        )
        {
            var session = configForSimulation.ActiveAiRetrievalSession;
            if (!isDryRun)
                session.ReceivedBatchPayloads.Add(aiJsonPayload);
            session.ReceivedBatchesCount++;
            Console.WriteLine(
                Program.GetLocalizedString(
                    "BatchReceived",
                    session.ReceivedBatchesCount,
                    session.TotalBatches,
                    session.RetrievalGuid
                )
            );
            if (session.ReceivedBatchesCount < session.TotalBatches)
            {
                if (!isDryRun)
                    AIFlowConfigService.SaveConfig(configForSimulation);
                else
                    Console.WriteLine(
                        Program.GetLocalizedString("DryRunWouldSaveConfigForBatch")
                    );
                Console.WriteLine(
                    Program.GetLocalizedString(
                        "RunFetchOutputCommand",
                        session.RetrievalGuid,
                        session.ReceivedBatchesCount + 1
                    )
                );
                return;
            }
            else
            {
                Console.WriteLine(
                    Program.GetLocalizedString(
                        "AllBatchesReceivedProcessing",
                        session.RetrievalGuid
                    )
                );
                var consolidatedFileChanges = new List<AiFileChange>();
                AiTaskUpdate? finalTaskUpdate = null;
                var payloadsToProcess = isDryRun
                    ? new List<string> { aiJsonPayload }
                    : session.ReceivedBatchPayloads;
                foreach (var batchJson in payloadsToProcess)
                {
                    var storedBatchWrapper = JsonSerializer.Deserialize<AiBatchResponseWrapper>(
                        batchJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                    var storedBatchPayload = storedBatchWrapper?.AiBatchResponse?.Payload;
                    if (storedBatchPayload?.FileChanges != null)
                        consolidatedFileChanges.AddRange(storedBatchPayload.FileChanges);
                    if (storedBatchPayload?.TaskUpdates != null)
                        finalTaskUpdate = storedBatchPayload.TaskUpdates;
                }
                mainOutputPackage.FileChanges = consolidatedFileChanges
                    .DistinctBy(fc => fc.Path)
                    .ToList();
                mainOutputPackage.TaskUpdates =
                    finalTaskUpdate ?? mainOutputPackage.TaskUpdates;
                if (!isDryRun)
                    configForSimulation.ActiveAiRetrievalSession = null;
                else
                    Console.WriteLine(
                        Program.GetLocalizedString("DryRunWouldClearActiveSession")
                    );
            }
        }
        string? backupId = null;
        if (
            !isDryRun
            && mainOutputPackage.FileChanges != null
            && mainOutputPackage.FileChanges.Any()
        )
        {
            var resourcesToBackup = new List<string>();
            foreach (var change in mainOutputPackage.FileChanges)
            {
                if (
                    change.Action.Equals("update", StringComparison.OrdinalIgnoreCase)
                    || change.Action.Equals("delete", StringComparison.OrdinalIgnoreCase)
                )
                {
                    var fullPath = FileService.GetFullPath(change.Path);
                    if (File.Exists(fullPath))
                    {
                        resourcesToBackup.Add(change.Path);
                    }
                }
            }
            if (resourcesToBackup.Any() || true)
            {
                backupId = BackupService.CreateBackup(
                    config,
                    resourcesToBackup,
                    task?.TaskId,
                    mainOutputPackage.AiChangesetId
                );
                if (string.IsNullOrEmpty(backupId))
                {
                    Console.Error.WriteLine(
                        Program.GetLocalizedString("BackupFailedProceedWithCaution")
                    );
                    Console.Write(Program.GetLocalizedString("PromptProceedWithoutBackup"));
                    if (Console.ReadLine()?.Trim().ToLowerInvariant() != "y")
                    {
                        Console.WriteLine(Program.GetLocalizedString("IntegrationAborted"));
                        return;
                    }
                }
            }
        }
        else if (
            isDryRun
            && mainOutputPackage.FileChanges != null
            && mainOutputPackage.FileChanges.Any()
        )
        {
            Console.WriteLine(Program.GetLocalizedString("DryRunWouldCreateBackup"));
        }
        var anyResourceSkippedOrMerged = false;
        if (mainOutputPackage.FileChanges != null)
        {
            foreach (var change in mainOutputPackage.FileChanges)
            {
                var fullPath = FileService.GetFullPath(change.Path);
                var trackedResource = configForSimulation.Resources.FirstOrDefault(r =>
                    r.Path == change.Path
                );
                if (change.Action.Equals("delete", StringComparison.OrdinalIgnoreCase))
                {
                    if (isDryRun)
                    {
                        Console.WriteLine(
                            Program.GetLocalizedString("DryRunWouldDeleteResource", change.Path)
                        );
                    }
                    else
                    {
                        if (File.Exists(fullPath))
                        {
                            File.Delete(fullPath);
                            Console.WriteLine(
                                Program.GetLocalizedString("ResourceDeleted", change.Path)
                            );
                        }
                        if (trackedResource != null)
                            configForSimulation.Resources.Remove(trackedResource);
                    }
                    changesWouldBeAppliedOrMergeFileCreated = true;
                    continue;
                }
                if (string.IsNullOrEmpty(change.ContentBase64))
                {
                    Console.WriteLine(
                        Program.GetLocalizedString("SkippingResourceNoContent", change.Path)
                    );
                    continue;
                }
                var aiResourceContent = FileService.Base64Decode(change.ContentBase64);
                if (trackedResource == null)
                {
                    trackedResource = new AIFlowResource
                    {
                        Path = change.Path,
                        Type = ResourceType.LocalFile,
                    };
                    if (!isDryRun)
                        configForSimulation.Resources.Add(trackedResource);
                    else
                        Console.WriteLine(
                            Program.GetLocalizedString(
                                "DryRunWouldAddTrackedResource",
                                change.Path
                            )
                        );
                }
                var applyAIChangesDirectly = true;
                if (
                    change.Action.Equals("update", StringComparison.OrdinalIgnoreCase)
                    && trackedResource.Type == ResourceType.LocalFile
                )
                {
                    string? currentLocalDiskContent = null;
                    string? currentLocalDiskHash = null;
                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            currentLocalDiskContent = File.ReadAllText(fullPath);
                        }
                        catch { }
                        currentLocalDiskHash = FileService.CalculateFileHash(fullPath);
                    }
                    if (
                        trackedResource.LastSentToAiHash != null
                        && currentLocalDiskHash != null
                        && currentLocalDiskHash != trackedResource.LastSentToAiHash
                    )
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine(
                            Program.GetLocalizedString(
                                "ConflictResourceChangedLocallyDetailed",
                                change.Path,
                                currentLocalDiskHash.Substring(
                                    0,
                                    Math.Min(currentLocalDiskHash.Length, 7)
                                ),
                                trackedResource.LastSentToAiHash.Substring(
                                    0,
                                    Math.Min(trackedResource.LastSentToAiHash.Length, 7)
                                ),
                                change.BasedOnHash?.Substring(
                                    0,
                                    Math.Min(change.BasedOnHash.Length, 7)
                                ) ?? "N/A"
                            )
                        );
                        Console.ResetColor();
                        if (isDryRun)
                        {
                            Console.WriteLine(
                                Program.GetLocalizedString(
                                    "DryRunWouldPromptConflict",
                                    change.Path
                                )
                            );
                            Console.WriteLine(
                                Program.GetLocalizedString(
                                    "DryRunSimulatingOverwrite",
                                    change.Path
                                )
                            );
                        }
                        else
                        {
                            string? choice = null;
                            while (choice != "o" && choice != "s" && choice != "m")
                            {
                                Console.Write(
                                    Program.GetLocalizedString(
                                        "PromptOverwriteSkipMerge",
                                        change.Path
                                    )
                                );
                                choice = Console.ReadLine()?.Trim().ToLowerInvariant();
                            }
                            if (choice == "s")
                            {
                                applyAIChangesDirectly = false;
                                trackedResource.Status = ResourceStatus.NeedsManualMerge;
                                Console.WriteLine(
                                    Program.GetLocalizedString(
                                        "SkippingAIChangesForResource",
                                        change.Path,
                                        trackedResource.Status
                                    )
                                );
                                anyResourceSkippedOrMerged = true;
                            }
                            else if (choice == "m")
                            {
                                applyAIChangesDirectly = false;
                                anyResourceSkippedOrMerged = true;
                                try
                                {
                                    var conflictContent = new StringBuilder();
                                    conflictContent.AppendLine("<<<<<<<CurrentLocalChanges");
                                    conflictContent.AppendLine(
                                        currentLocalDiskContent ?? string.Empty
                                    );
                                    conflictContent.AppendLine("=======");
                                    conflictContent.AppendLine(aiResourceContent);
                                    conflictContent.AppendLine(
                                        $">>>>>>> AI's Change (Task: {task?.TaskId ?? "N/A"} / AIFlow)"
                                    );
                                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                                    File.WriteAllText(fullPath, conflictContent.ToString());
                                    trackedResource.Status = ResourceStatus.NeedsManualMerge;
                                    trackedResource.ConflictResolutionFile = null;
                                    Console.WriteLine(
                                        Program.GetLocalizedString(
                                            "MergeMarkersWrittenToOriginalFile",
                                            change.Path
                                        )
                                    );
                                    Console.WriteLine(
                                        Program.GetLocalizedString(
                                            "ResolveOriginalFileInstruction",
                                            change.Path
                                        )
                                    );
                                }
                                catch (Exception ex)
                                {
                                    Console.Error.WriteLine(
                                        Program.GetLocalizedString(
                                            "ErrorWritingMergeMarkers",
                                            change.Path,
                                            ex.Message
                                        )
                                    );
                                    trackedResource.Status = ResourceStatus.NeedsManualMerge;
                                }
                            }
                        }
                    }
                    else if (
                        trackedResource.LastSentToAiHash != null
                        && change.BasedOnHash != null
                        && trackedResource.LastSentToAiHash != change.BasedOnHash
                    )
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine(
                            Program.GetLocalizedString(
                                "WarningAIUsedStaleVersion",
                                change.Path,
                                change.BasedOnHash,
                                trackedResource.LastSentToAiHash
                            )
                        );
                        Console.ResetColor();
                    }
                }
                if (applyAIChangesDirectly)
                {
                    if (isDryRun)
                    {
                        Console.WriteLine(
                            Program.GetLocalizedString(
                                change.Action.Equals(
                                    "create",
                                    StringComparison.OrdinalIgnoreCase
                                )
                                    ? "DryRunWouldCreateResource"
                                    : "DryRunWouldUpdateResource",
                                change.Path
                            )
                        );
                        if (trackedResource != null)
                        {
                            trackedResource.Status = ResourceStatus.AiModified;
                        }
                    }
                    else
                    {
                        if (trackedResource.Type == ResourceType.LocalFile)
                        {
                            try
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                                File.WriteAllText(fullPath, aiResourceContent);
                                Console.WriteLine(
                                    Program.GetLocalizedString(
                                        change.Action.Equals(
                                            "create",
                                            StringComparison.OrdinalIgnoreCase
                                        )
                                            ? "ResourceCreated"
                                            : "ResourceUpdated",
                                        change.Path
                                    )
                                );
                                trackedResource.LocalHash =
                                    FileService.CalculateFileHash(fullPath) ?? string.Empty;
                                trackedResource.Status = ResourceStatus.AiModified;
                                trackedResource.LastSentToAiHash = null;
                                trackedResource.ConflictResolutionFile = null;
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine(
                                    Program.GetLocalizedString(
                                        "ErrorWritingFile",
                                        change.Path,
                                        ex.Message
                                    )
                                );
                            }
                        }
                        else
                        {
                            Console.WriteLine(
                                Program.GetLocalizedString(
                                    "ResourceReferenceUpdated",
                                    change.Path,
                                    trackedResource.Type
                                )
                            );
                            trackedResource.Status = ResourceStatus.AiModified;
                        }
                    }
                    changesWouldBeAppliedOrMergeFileCreated = true;
                }
            }
        }
        if (task != null && mainOutputPackage.TaskUpdates != null)
        {
            var originalTaskStatus = task.Status;
            var newTaskStatus =
                mainOutputPackage.TaskUpdates.NewStatus ?? originalTaskStatus;
            var newAiNotes = mainOutputPackage.TaskUpdates.AiNotes ?? task.AiNotes;
            var newUpdatedAt = mainOutputPackage.TaskUpdates.UpdatedAt ?? DateTime.UtcNow;
            if (
                anyResourceSkippedOrMerged
                && (newTaskStatus == TaskStatus.Completed || newTaskStatus == TaskStatus.Done)
            )
            {
                newTaskStatus = TaskStatus.PendingHumanReview;
                if (isDryRun)
                    Console.WriteLine(
                        Program.GetLocalizedString(
                            "DryRunTaskStatusAdjustedForSkippedFiles",
                            task.TaskId,
                            newTaskStatus
                        )
                    );
                else
                    Console.WriteLine(
                        Program.GetLocalizedString(
                            "TaskStatusAdjustedForSkippedFiles",
                            task.TaskId,
                            newTaskStatus
                        )
                    );
            }
            if (isDryRun)
            {
                Console.WriteLine(
                    Program.GetLocalizedString(
                        "DryRunWouldUpdateTask",
                        task.TaskId,
                        newTaskStatus,
                        newAiNotes ?? "N/A"
                    )
                );
            }
            else
            {
                task.Status = newTaskStatus;
                task.AiNotes = newAiNotes;
                task.UpdatedAt = newUpdatedAt;
                Console.WriteLine(
                    Program.GetLocalizedString("TaskUpdated", task.TaskId, task.Status)
                );
            }
            changesWouldBeAppliedOrMergeFileCreated = true;
        }
        if (!isDryRun)
        {
            AIFlowConfigService.SaveConfig(configForSimulation);
        }
        else
        {
            Console.WriteLine(Program.GetLocalizedString("DryRunWouldSaveConfig"));
        }
        if (changesWouldBeAppliedOrMergeFileCreated || mainOutputPackage.TaskUpdates != null)
        {
            Console.WriteLine(
                isDryRun
                    ? Program.GetLocalizedString("DryRunSummaryChanges")
                    : Program.GetLocalizedString("AIOutputIntegrated")
            );
        }
        else
        {
            Console.WriteLine(
                isDryRun
                    ? Program.GetLocalizedString("DryRunSummaryNoChanges")
                    : Program.GetLocalizedString("AIOutputProcessedNoChangesApplied")
            );
        }
        if (isDryRun)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(Program.GetLocalizedString("DryRunModeEnded"));
            Console.ResetColor();
        }
    }
}

public static class PrepareInputCommand
{
    static Option<string?> taskIdOption = new Option<string?>(
        aliases: new[] { "--task-id", "-tid" },
        description: Program.GetLocalizedString("PrepareInputTaskIdOptionDescription")
    );
    static Option<string?> taskDescOption = new Option<string?>(
        aliases: new[] { "--task-desc", "-td" },
        description: Program.GetLocalizedString("PrepareInputTaskDescOptionDescription")
    );
    static Option<string[]?> resourcesOption = new Option<string[]?>(
        aliases: new[] { "--resource", "-r" },
        description: Program.GetLocalizedString("PrepareInputResourceOptionDescription")
    )
    {
        Arity = ArgumentArity.ZeroOrMore,
    };
    static Option<string[]?> newResourcesOption = new Option<string[]?>(
        aliases: new[] { "--new-resource", "-nr" },
        description: Program.GetLocalizedString("PrepareInputNewResourceOptionDescription")
    )
    {
        Arity = ArgumentArity.ZeroOrMore,
    };
    static Option<string?> continueTaskOption = new Option<string?>(
        aliases: new[] { "--continue-task" },
        description: Program.GetLocalizedString("PrepareInputContinueTaskOptionDescription")
    );
    static Option<string?> typeOption = new Option<string?>(
        aliases: new[] { "--type" },
        description: Program.GetLocalizedString("PrepareInputTypeOptionDescription_Optional"),
        parseArgument: result =>
        {
            if (!result.Tokens.Any())
                return null;
            var typeVal = result.Tokens.Single().Value;
            var validTypes = new[]
            {
                    TaskType.Story,
                    TaskType.Task,
                    TaskType.Bug,
                    TaskType.Epic,
                    TaskType.Spike,
            };
            if (!validTypes.Contains(typeVal, StringComparer.OrdinalIgnoreCase))
            {
                result.ErrorMessage = Program.GetLocalizedString(
                    "ErrorInvalidTaskType",
                    typeVal,
                    string.Join(", ", validTypes)
                );
                return null;
            }
            return validTypes.First(vt =>
                string.Equals(vt, typeVal, StringComparison.OrdinalIgnoreCase)
            );
        }
    );
    static Option<int?> storyPointsOption = new Option<int?>(
        aliases: new[] { "--story-points", "-sp" },
        description: Program.GetLocalizedString("PrepareInputStoryPointsOptionDescription")
    );
    static Option<string?> priorityOption = new Option<string?>(
        aliases: new[] { "--priority", "-p" },
        description: Program.GetLocalizedString(
            "PrepareInputPriorityOptionDescription_Optional"
        ),
        parseArgument: result =>
        {
            if (!result.Tokens.Any())
                return null;
            var prioVal = result.Tokens.Single().Value;
            var validPrios = new[]
            {
                    TaskPriority.Highest,
                    TaskPriority.High,
                    TaskPriority.Medium,
                    TaskPriority.Low,
                    TaskPriority.Lowest,
            };
            if (!validPrios.Contains(prioVal, StringComparer.OrdinalIgnoreCase))
            {
                result.ErrorMessage = Program.GetLocalizedString(
                    "ErrorInvalidTaskPriority",
                    prioVal,
                    string.Join(", ", validPrios)
                );
                return null;
            }
            return validPrios.First(vp =>
                string.Equals(vp, prioVal, StringComparison.OrdinalIgnoreCase)
            );
        }
    );
    static Option<string?> sprintOption = new Option<string?>(
        aliases: new[] { "--sprint" },
        description: Program.GetLocalizedString("PrepareInputSprintOptionDescription")
    );
    static Option<string?> epicLinkOption = new Option<string?>(
        aliases: new[] { "--epic-link" },
        description: Program.GetLocalizedString("PrepareInputEpicLinkOptionDescription")
    );
    static Option<DateTime?> dueDateOption = new Option<DateTime?>(
        aliases: new[] { "--due-date" },
        description: Program.GetLocalizedString("PrepareInputDueDateOptionDescription")
    );
    static Option<string[]?> labelsOption = new Option<string[]?>(
        aliases: new[] { "--labels", "-l" },
        description: Program.GetLocalizedString("PrepareInputLabelsOptionDescription")
    )
    {
        Arity = ArgumentArity.ZeroOrMore,
    };
    static Option<string?> assigneeOption = new Option<string?>(
        aliases: new[] { "--assignee" },
        description: Program.GetLocalizedString("PrepareInputAssigneeOptionDescription")
    );
    static Option<string?> branchOption = new Option<string?>(
        aliases: new[] { "--branch", "-b" },
        description: Program.GetLocalizedString("PrepareInputBranchOptionDescription")
    );
    static Option<string?> humanNotesOption = new Option<string?>(
        aliases: new[] { "--notes", "-n" },
        description: Program.GetLocalizedString("PrepareInputHumanNotesOptionDescription")
    );

    public static Command Create()
    { /* ... same as v1.7 ... */
        var command = new Command(
            "prepare-input",
            Program.GetLocalizedString("PrepareInputCommandDescription")
        );
        command.AddOption(taskIdOption);
        command.AddOption(taskDescOption);
        command.AddOption(resourcesOption);
        command.AddOption(newResourcesOption);
        command.AddOption(continueTaskOption);
        command.AddOption(typeOption);
        command.AddOption(storyPointsOption);
        command.AddOption(priorityOption);
        command.AddOption(sprintOption);
        command.AddOption(epicLinkOption);
        command.AddOption(dueDateOption);
        command.AddOption(labelsOption);
        command.AddOption(assigneeOption);
        command.AddOption(branchOption);
        command.AddOption(humanNotesOption);
        command.SetHandler(
            async (context) =>
            {
                var isInteractive =
                    context.ParseResult.CommandResult.Children.Count == 0
                    ||
                        context.ParseResult.HasOption(taskDescOption)
                        && !context.ParseResult.HasOption(taskIdOption)
                        && !context.ParseResult.HasOption(resourcesOption)
                        && !context.ParseResult.HasOption(newResourcesOption)
                        && !context.ParseResult.HasOption(continueTaskOption)
                    ;
                if (
                    isInteractive
                    && string.IsNullOrEmpty(
                        context.ParseResult.GetValueForOption(continueTaskOption)
                    )
                )
                {
                    await RunInteractiveMode(context);
                }
                else
                {
                    await HandlePrepareInput(context, false);
                }
            }
        );
        return command;
    }

    private static async Task RunInteractiveMode(InvocationContext context)
    { /* ... same as v1.7 ... */
        Console.WriteLine(Program.GetLocalizedString("PrepareInputInteractiveModeStart"));
        var config = AIFlowConfigService.LoadConfig();
        if (config == null)
            return;
        var taskId = context.ParseResult.GetValueForOption(taskIdOption);
        var taskDesc = context.ParseResult.GetValueForOption(taskDescOption);
        if (string.IsNullOrEmpty(taskDesc))
            taskDesc = Prompt(Program.GetLocalizedString("PromptTaskDesc"));
        if (string.IsNullOrEmpty(taskDesc))
        {
            Console.Error.WriteLine(Program.GetLocalizedString("ErrorTaskDescRequired"));
            return;
        }
        var resources = PromptForMultiple(
            Program.GetLocalizedString("PromptResourcesToInclude"),
            Program.GetLocalizedString("PromptResourceToAttachPath")
        );
        var newResources = PromptForMultiple(
            Program.GetLocalizedString("PromptNewResourcesToCreate"),
            Program.GetLocalizedString("PromptNewResourcePath")
        );
        Console.WriteLine(Program.GetLocalizedString("PromptAgileDataOptional"));
        var type = PromptOptional(
            Program.GetLocalizedString(
                "PromptTaskType",
                string.Join(
                    ", ",
                    TaskType.Story,
                    TaskType.Task,
                    TaskType.Bug,
                    TaskType.Epic,
                    TaskType.Spike
                )
            )
        );
        var storyPoints = PromptOptionalInt(Program.GetLocalizedString("PromptStoryPoints"));
        var priority = PromptOptional(
            Program.GetLocalizedString(
                "PromptTaskPriority",
                string.Join(
                    ", ",
                    TaskPriority.Highest,
                    TaskPriority.High,
                    TaskPriority.Medium,
                    TaskPriority.Low,
                    TaskPriority.Lowest
                )
            )
        );
        var sprint = PromptOptional(Program.GetLocalizedString("PromptSprint"));
        var epicLink = PromptOptional(Program.GetLocalizedString("PromptEpicLink"));
        var dueDate = PromptOptionalDate(Program.GetLocalizedString("PromptDueDate"));
        var labels = PromptForMultiple(
            Program.GetLocalizedString("PromptLabels"),
            Program.GetLocalizedString("PromptLabelToAdd")
        );
        var assignee = PromptOptional(Program.GetLocalizedString("PromptAssignee"), "ai");
        var branch = PromptOptional(
            Program.GetLocalizedString("PromptBranch"),
            config.CurrentBranch
        );
        var humanNotes = PromptOptional(Program.GetLocalizedString("PromptHumanNotes"));
        await HandlePrepareInputLogic(
            taskId,
            taskDesc,
            resources?.ToArray(),
            newResources?.ToArray(),
            null,
            type,
            storyPoints,
            priority,
            sprint,
            epicLink,
            dueDate,
            labels?.ToArray(),
            assignee,
            branch,
            humanNotes,
            context
        );
    }

    private static string? Prompt(string message, string? defaultValue = null)
    { /* ... same ... */
        Console.Write($"{message}{(defaultValue == null ? "" : $" [{defaultValue}]")}: ");
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
    }

    private static string? PromptOptional(string message, string? defaultValue = null)
    { /* ... same ... */
        var input = Prompt(
            $"{message} (optional, press Enter to skip{(defaultValue == null ? "" : $" or use default '{defaultValue}'")})",
            defaultValue
        );
        return string.IsNullOrWhiteSpace(input)
            ? defaultValue
            :
                input == defaultValue && string.IsNullOrWhiteSpace(Console.ReadLine()?.Trim())
                    ? defaultValue
                    : input
            ;
    }

    private static int? PromptOptionalInt(string message)
    { /* ... same ... */
        var input = PromptOptional(message);
        return int.TryParse(input, out var val) ? val : null;
    }

    private static DateTime? PromptOptionalDate(string message)
    { /* ... same ... */
        var input = PromptOptional(message + " (YYYY-MM-DD)");
        return DateTime.TryParse(input, out var val) ? val : null;
    }

    private static List<string>? PromptForMultiple(
        string initialMessage,
        string subsequentPrompt
    )
    { /* ... same ... */
        Console.WriteLine(
            initialMessage + Program.GetLocalizedString("PromptMultipleEndWithEmpty")
        );
        var items = new List<string>();
        while (true)
        {
            Console.Write($"{subsequentPrompt}: ");
            var item = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(item))
                break;
            items.Add(item);
        }
        return items.Any() ? items : null;
    }

    private static async Task HandlePrepareInputLogic(
        string? taskId,
        string? taskDesc,
        string[]? resources,
        string[]? newResources,
        string? continueTask,
        string? taskType,
        int? storyPoints,
        string? priority,
        string? sprint,
        string? epicLink,
        DateTime? dueDate,
        string[]? labels,
        string? assignee,
        string? branch,
        string? humanNotes,
        InvocationContext? cliContext = null
    )
    {
        // ... (Logic is the same as v1.7, ensuring "Resource" terminology is used where "File" was before)
        var config = AIFlowConfigService.LoadConfig();
        if (config == null)
            return;
        var projectRoot = Directory.GetCurrentDirectory();
        var ignoreMatcher = IgnoreService.LoadIgnoreMatcher(projectRoot);
        AIFlowTask? taskToProcess;
        var isNewTask = false;
        var isContinuingSplitRequest = !string.IsNullOrEmpty(continueTask);
        if (isContinuingSplitRequest)
        {
            taskToProcess = config.Tasks.FirstOrDefault(t => t.TaskId == continueTask);
            if (taskToProcess == null)
            {
                Console.Error.WriteLine(
                    Program.GetLocalizedString("ErrorTaskNotFoundForContinue", continueTask!)
                );
                return;
            }
            if (
                taskToProcess.Status != TaskStatus.PendingHumanInputParts
                || taskToProcess.HumanRequestGroupId == null
                || taskToProcess.HumanRequestTotalParts == null
                || taskToProcess.HumanRequestPartsSent >= taskToProcess.HumanRequestTotalParts
            )
            {
                Console.Error.WriteLine(
                    Program.GetLocalizedString(
                        "ErrorInvalidTaskStateForContinue",
                        continueTask!
                    )
                );
                return;
            }
            Console.WriteLine(
                Program.GetLocalizedString(
                    "ContinuingSplitRequest",
                    taskToProcess.TaskId,
                    taskToProcess.HumanRequestPartsSent + 1,
                    taskToProcess.HumanRequestTotalParts!
                )
            );
        }
        else
        {
            if (string.IsNullOrEmpty(taskId) && string.IsNullOrEmpty(taskDesc))
            {
                Console.Error.WriteLine(
                    Program.GetLocalizedString("ErrorTaskDescOrIdRequired")
                );
                return;
            }
            taskToProcess = config.Tasks.FirstOrDefault(t => t.TaskId == taskId);
            if (taskToProcess == null)
            {
                isNewTask = true;
                if (string.IsNullOrEmpty(taskDesc))
                {
                    Console.Error.WriteLine(
                        Program.GetLocalizedString("ErrorTaskDescRequiredForNewTask")
                    );
                    return;
                }
                taskToProcess = new AIFlowTask
                {
                    TaskId = taskId ?? $"task_{config.NextTaskId++}",
                    Description = taskDesc!,
                    Branch = branch ?? config.CurrentBranch,
                    Status = TaskStatus.ToDo,
                    CreatedAt = DateTime.UtcNow,
                    Type = taskType,
                    StoryPoints = storyPoints,
                    Priority = priority,
                    Sprint = sprint,
                    EpicLink = epicLink,
                    DueDate = dueDate,
                    Labels = labels?.ToList() ?? new List<string>(),
                    AssignedTo = assignee ?? "ai",
                    HumanNotes = humanNotes,
                };
                config.Tasks.Add(taskToProcess);
            }
            else
            {
                if (!string.IsNullOrEmpty(taskDesc))
                    taskToProcess.Description = taskDesc;
                var hasBranchOpt =
                    cliContext?.ParseResult.HasOption(branchOption)
                    ?? cliContext == null && branch != null;
                if (hasBranchOpt)
                    taskToProcess.Branch = branch ?? taskToProcess.Branch;
                var hasTypeOpt =
                    cliContext?.ParseResult.HasOption(typeOption)
                    ?? cliContext == null && taskType != null;
                if (hasTypeOpt)
                    taskToProcess.Type = taskType;
                else if (isNewTask && !hasTypeOpt)
                    taskToProcess.Type = null;
                var hasSpOpt =
                    cliContext?.ParseResult.HasOption(storyPointsOption)
                    ?? cliContext == null && storyPoints.HasValue;
                if (hasSpOpt)
                    taskToProcess.StoryPoints = storyPoints;
                else if (isNewTask && !hasSpOpt)
                    taskToProcess.StoryPoints = null;
                var hasPrioOpt =
                    cliContext?.ParseResult.HasOption(priorityOption)
                    ?? cliContext == null && priority != null;
                if (hasPrioOpt)
                    taskToProcess.Priority = priority;
                else if (isNewTask && !hasPrioOpt)
                    taskToProcess.Priority = null;
                var hasSprintOpt =
                    cliContext?.ParseResult.HasOption(sprintOption)
                    ?? cliContext == null && sprint != null;
                if (hasSprintOpt)
                    taskToProcess.Sprint = sprint;
                else if (isNewTask && !hasSprintOpt)
                    taskToProcess.Sprint = null;
                var hasEpicOpt =
                    cliContext?.ParseResult.HasOption(epicLinkOption)
                    ?? cliContext == null && epicLink != null;
                if (hasEpicOpt)
                    taskToProcess.EpicLink = epicLink;
                else if (isNewTask && !hasEpicOpt)
                    taskToProcess.EpicLink = null;
                var hasDueOpt =
                    cliContext?.ParseResult.HasOption(dueDateOption)
                    ?? cliContext == null && dueDate.HasValue;
                if (hasDueOpt)
                    taskToProcess.DueDate = dueDate;
                else if (isNewTask && !hasDueOpt)
                    taskToProcess.DueDate = null;
                var hasLabelsOpt =
                    cliContext?.ParseResult.HasOption(labelsOption)
                    ?? cliContext == null && labels != null;
                if (hasLabelsOpt)
                    taskToProcess.Labels = labels?.ToList() ?? taskToProcess.Labels;
                else if (isNewTask && !hasLabelsOpt)
                    taskToProcess.Labels = new List<string>();
                var hasAssigneeOpt =
                    cliContext?.ParseResult.HasOption(assigneeOption)
                    ?? cliContext == null && assignee != null;
                if (hasAssigneeOpt)
                    taskToProcess.AssignedTo = assignee ?? taskToProcess.AssignedTo;
                else if (isNewTask && !hasAssigneeOpt)
                    taskToProcess.AssignedTo = "ai";
                var hasNotesOpt =
                    cliContext?.ParseResult.HasOption(humanNotesOption)
                    ?? cliContext == null && humanNotes != null;
                if (hasNotesOpt)
                    taskToProcess.HumanNotes = humanNotes;
                else if (isNewTask && !hasNotesOpt)
                    taskToProcess.HumanNotes = null;
            }
            taskToProcess.RelatedResources.Clear();
        }
        taskToProcess.UpdatedAt = DateTime.UtcNow;
        var humanRequestPayload = new HumanRequestPayload
        {
            TaskId = taskToProcess.TaskId,
            TaskDescription = taskToProcess.Description,
            HumanRequestGroupId = taskToProcess.HumanRequestGroupId,
            FilesToProcess = new List<HumanRequestFileToProcess>(),
            FileData = new List<HumanRequestFileData>(),
        };
        long currentPayloadSize = 0;
        var allResourcesForTask =
            new List<(
                string path,
                string action,
                string? hashAtRequestTime,
                string? content,
                string type
            )>();
        if (resources != null)
        {
            foreach (var resourcePathInput in resources)
            {
                var fullPath = FileService.GetFullPath(resourcePathInput);
                var projectRelPath = FileService.GetProjectRelativePath(fullPath);
                if (IgnoreService.IsFileIgnored(projectRelPath, ignoreMatcher))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(
                        Program.GetLocalizedString(
                            "WarningResourceIgnored",
                            projectRelPath,
                            IgnoreService.IgnoreFileName
                        )
                    );
                    Console.ResetColor();
                    continue;
                }
                if (!File.Exists(fullPath))
                {
                    Console.Error.WriteLine(
                        Program.GetLocalizedString("ErrorResourceNotFound", projectRelPath)
                    );
                    continue;
                }
                var hash = FileService.CalculateFileHash(fullPath);
                if (hash == null)
                    continue;
                string? content = null;
                if (
                    !isContinuingSplitRequest
                    || taskToProcess.RelatedResources.Any(rr =>
                        rr.Path == projectRelPath
                        && rr.ContentDetail != null
                        && rr.SentInPart == null
                    )
                )
                {
                    content = await File.ReadAllTextAsync(fullPath);
                }
                allResourcesForTask.Add(
                    (projectRelPath, "update", hash, content, ResourceType.LocalFile)
                );
            }
        }
        if (newResources != null)
        {
            foreach (var resourcePathInput in newResources)
            {
                var projectRelPath = FileService.GetProjectRelativePath(
                    FileService.GetFullPath(resourcePathInput)
                );
                if (IgnoreService.IsFileIgnored(projectRelPath, ignoreMatcher))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(
                        Program.GetLocalizedString(
                            "WarningNewResourceIgnored",
                            projectRelPath,
                            IgnoreService.IgnoreFileName
                        )
                    );
                    Console.ResetColor();
                    continue;
                }
                allResourcesForTask.Add(
                    (projectRelPath, "create", null, null, ResourceType.LocalFile)
                );
            }
        }
        var currentPartNumber = isContinuingSplitRequest
            ? taskToProcess.HumanRequestPartsSent + 1
            : 1;
        var needsSplitting = false;
        long tempPayloadSizeEstimate = Encoding
            .UTF8.GetBytes(JsonSerializer.Serialize(humanRequestPayload))
            .Length;
        if (!isContinuingSplitRequest)
        {
            taskToProcess.RelatedResources.Clear();
            foreach (
                var (
                    path,
                    fileAction,
                    fileHash,
                    fileContent,
                    resourceType
                ) in allResourcesForTask
            )
            {
                var relatedResourceEntry = new AIFlowTaskRelatedResource
                {
                    Path = path,
                    Action = fileAction,
                    HashAtRequestTime = fileHash,
                    Type = resourceType,
                };
                taskToProcess.RelatedResources.Add(relatedResourceEntry);
                tempPayloadSizeEstimate +=
                    Encoding.UTF8.GetBytes(path).Length
                    + (
                        fileContent != null
                            ? Encoding
                                .UTF8.GetBytes(FileService.Base64Encode(fileContent))
                                .Length
                            : 0
                    )
                    + 100;
            }
            if (tempPayloadSizeEstimate > config.Config.MaxRequestPayloadSizeBytes)
            {
                needsSplitting = true;
                taskToProcess.HumanRequestGroupId ??= $"hrg_{config.NextHumanRequestGroupId++}";
                taskToProcess.Status = TaskStatus.PendingHumanInputParts;
                Console.WriteLine(
                    Program.GetLocalizedString(
                        "InfoRequestWillBeSplit",
                        taskToProcess.HumanRequestGroupId
                    )
                );
            }
            humanRequestPayload.HumanRequestGroupId = taskToProcess.HumanRequestGroupId;
        }
        humanRequestPayload.PartNumber = currentPartNumber;
        if (currentPartNumber == 1)
        {
            humanRequestPayload.TaskDescription = taskToProcess.Description;
            foreach (var relatedResource in taskToProcess.RelatedResources)
            {
                var resourceData = allResourcesForTask.FirstOrDefault(r =>
                    r.path == relatedResource.Path
                );
                var reqFile = new HumanRequestFileToProcess
                {
                    Path = relatedResource.Path,
                    Action = relatedResource.Action,
                    Hash = relatedResource.HashAtRequestTime,
                };
                if (
                    resourceData.content != null
                    && relatedResource.Type == ResourceType.LocalFile
                )
                {
                    var encodedContent = FileService.Base64Encode(resourceData.content);
                    long resourceSizeEstimate =
                        Encoding.UTF8.GetBytes(reqFile.Path).Length
                        + Encoding.UTF8.GetBytes(encodedContent).Length
                        + 100;
                    if (
                        currentPayloadSize + resourceSizeEstimate
                        < config.Config.MaxRequestPayloadSizeBytes * 0.9
                    )
                    {
                        reqFile.ContentBase64 = encodedContent;
                        currentPayloadSize += resourceSizeEstimate;
                        relatedResource.SentInPart = 1;
                    }
                    else
                    {
                        reqFile.ContentDetail = "provided_in_next_part";
                        needsSplitting = true;
                        relatedResource.ContentDetail = reqFile.ContentDetail;
                    }
                }
                else if (relatedResource.Type != ResourceType.LocalFile)
                {
                    reqFile.ContentDetail = $"type:{relatedResource.Type}";
                    relatedResource.SentInPart = 1;
                }
                humanRequestPayload.FilesToProcess!.Add(reqFile);
            }
        }
        else
        {
            var resourcesToSendInThisPart = taskToProcess
                .RelatedResources.Where(rr =>
                    rr.SentInPart == null
                    && rr.ContentDetail != null
                    && rr.Type == ResourceType.LocalFile
                )
                .ToList();
            foreach (var relatedResource in resourcesToSendInThisPart)
            {
                var resourceData = allResourcesForTask.FirstOrDefault(r =>
                    r.path == relatedResource.Path
                );
                if (resourceData.content == null)
                    continue;
                var encodedContent = FileService.Base64Encode(resourceData.content);
                long resourceSizeEstimate =
                    Encoding.UTF8.GetBytes(relatedResource.Path).Length
                    + Encoding.UTF8.GetBytes(encodedContent).Length
                    + 50;
                if (
                    currentPayloadSize + resourceSizeEstimate
                    < config.Config.MaxRequestPayloadSizeBytes * 0.95
                )
                {
                    humanRequestPayload.FileData!.Add(
                        new HumanRequestFileData
                        {
                            Path = relatedResource.Path,
                            ContentBase64 = encodedContent,
                        }
                    );
                    currentPayloadSize += resourceSizeEstimate;
                    relatedResource.SentInPart = currentPartNumber;
                }
                else
                {
                    break;
                }
            }
        }
        if (needsSplitting || taskToProcess.HumanRequestGroupId != null)
        {
            taskToProcess.HumanRequestGroupId ??= $"hrg_{config.NextHumanRequestGroupId++}";
            humanRequestPayload.HumanRequestGroupId = taskToProcess.HumanRequestGroupId;
            var moreFilesToSend = taskToProcess.RelatedResources.Any(rr =>
                rr.SentInPart == null
                && (
                    rr.ContentDetail != null
                    ||
                        rr.Type == ResourceType.LocalFile
                        && allResourcesForTask.FirstOrDefault(f => f.path == rr.Path).content
                            != null

                )
            );
            if (
                currentPartNumber == 1
                && !moreFilesToSend
                && (
                    humanRequestPayload.FileData == null
                    || humanRequestPayload.FileData.Count == 0
                )
            )
            {
                humanRequestPayload.TotalParts = 1;
                taskToProcess.HumanRequestTotalParts = 1;
                taskToProcess.Status = TaskStatus.PendingAiProcessing;
            }
            else if (moreFilesToSend)
            {
                humanRequestPayload.TotalParts = currentPartNumber + 1;
                taskToProcess.HumanRequestTotalParts = currentPartNumber + 1;
                taskToProcess.Status = TaskStatus.PendingHumanInputParts;
            }
            else
            {
                humanRequestPayload.TotalParts = currentPartNumber;
                taskToProcess.HumanRequestTotalParts = currentPartNumber;
                taskToProcess.Status = TaskStatus.PendingAiProcessing;
            }
        }
        else
        {
            humanRequestPayload.TotalParts = 1;
            taskToProcess.Status = TaskStatus.PendingAiProcessing;
        }
        taskToProcess.HumanRequestPartsSent = currentPartNumber;
        foreach (
            var relatedResource in taskToProcess.RelatedResources.Where(rr =>
                rr.SentInPart == currentPartNumber
                || currentPartNumber == 1 && rr.SentInPart == null && rr.ContentDetail == null
            )
        )
        {
            var trackedResource = config.Resources.FirstOrDefault(r =>
                r.Path == relatedResource.Path
            );
            if (trackedResource == null)
            {
                trackedResource = new AIFlowResource
                {
                    Path = relatedResource.Path,
                    Type = relatedResource.Type,
                };
                config.Resources.Add(trackedResource);
            }
            if (
                relatedResource.Action == "update"
                && relatedResource.HashAtRequestTime != null
                && relatedResource.Type == ResourceType.LocalFile
            )
            {
                trackedResource.LastSentToAiHash = relatedResource.HashAtRequestTime;
            }
            trackedResource.Status = ResourceStatus.AwaitingAiChanges;
        }
        var estimatedTokens = EstimateTokens(humanRequestPayload);
        if (estimatedTokens > config.Config.ApproxMaxAiContextTokens)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(
                Program.GetLocalizedString(
                    "WarningTokenEstimateExceedsLimit",
                    estimatedTokens,
                    config.Config.ApproxMaxAiContextTokens,
                    taskToProcess.TaskId,
                    humanRequestPayload.PartNumber
                )
            );
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine(
                Program.GetLocalizedString(
                    "InfoTokenEstimate",
                    estimatedTokens,
                    humanRequestPayload.PartNumber
                )
            );
        }
        AIFlowConfigService.SaveConfig(config);
        var finalPayload = new HumanRequestPart { HumanRequest = humanRequestPayload };
        var jsonOutput = JsonSerializer.Serialize(
            finalPayload,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System
                    .Text
                    .Json
                    .Serialization
                    .JsonIgnoreCondition
                    .WhenWritingNull,
            }
        );
        Console.WriteLine(Program.GetLocalizedString("RequestPackageReady"));
        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine(jsonOutput);
        Console.WriteLine("--------------------------------------------------");
        if (
            taskToProcess.Status == TaskStatus.PendingHumanInputParts
            && taskToProcess.HumanRequestTotalParts > taskToProcess.HumanRequestPartsSent
        )
        {
            Console.WriteLine(
                Program.GetLocalizedString("InstructionForNextPart", taskToProcess.TaskId)
            );
        }
        else if (taskToProcess.Status == TaskStatus.PendingAiProcessing)
        {
            Console.WriteLine(
                Program.GetLocalizedString("AllPartsSentAwaitingAI", taskToProcess.TaskId)
            );
        }
    }

    private static int EstimateTokens(HumanRequestPayload payload)
    { /* ... same as before ... */
        const int charsPerToken = 4;
        var totalChars = 0;
        if (!string.IsNullOrEmpty(payload.TaskDescription))
        {
            totalChars += payload.TaskDescription.Length;
        }
        if (payload.FilesToProcess != null)
        {
            foreach (var fileToProcess in payload.FilesToProcess)
            {
                if (!string.IsNullOrEmpty(fileToProcess.ContentBase64))
                {
                    try
                    {
                        var decodedContent = FileService.Base64Decode(
                            fileToProcess.ContentBase64
                        );
                        totalChars += decodedContent.Length;
                    }
                    catch { }
                }
            }
        }
        if (payload.FileData != null)
        {
            foreach (var fileData in payload.FileData)
            {
                if (!string.IsNullOrEmpty(fileData.ContentBase64))
                {
                    try
                    {
                        var decodedContent = FileService.Base64Decode(
                            fileData.ContentBase64
                        );
                        totalChars += decodedContent.Length;
                    }
                    catch { }
                }
            }
        }
        totalChars += 500;
        return totalChars / charsPerToken;
    }

    private static async Task HandlePrepareInput(
        InvocationContext context,
        bool isInteractiveContext = true
    )
    { /* ... same as before ... */
        await HandlePrepareInputLogic(
            context.ParseResult.GetValueForOption(taskIdOption),
            context.ParseResult.GetValueForOption(taskDescOption),
            context.ParseResult.GetValueForOption(resourcesOption),
            context.ParseResult.GetValueForOption(newResourcesOption),
            context.ParseResult.GetValueForOption(continueTaskOption),
            context.ParseResult.GetValueForOption(typeOption),
            context.ParseResult.GetValueForOption(storyPointsOption),
            context.ParseResult.GetValueForOption(priorityOption),
            context.ParseResult.GetValueForOption(sprintOption),
            context.ParseResult.GetValueForOption(epicLinkOption),
            context.ParseResult.GetValueForOption(dueDateOption),
            context.ParseResult.GetValueForOption(labelsOption),
            context.ParseResult.GetValueForOption(assigneeOption),
            context.ParseResult.GetValueForOption(branchOption),
            context.ParseResult.GetValueForOption(humanNotesOption),
            context
        );
    }
}

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
            var resourcePath = context.ParseResult.GetValueForArgument(
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
        var fullOriginalPath = FileService.GetFullPath(trackedResource.Path);
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
        var newHash =
            trackedResource.Type == ResourceType.LocalFile
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
            var resolvedContent = File.ReadAllText(fullOriginalPath);
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
                var proceed = Console.ReadLine()?.Trim().ToLowerInvariant();
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
            (context) =>
            {
                var list = context.ParseResult.GetValueForOption(listOption);
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
            var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
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
                $"  ID: {backup.BackupId,-20} Timestamp: {backup.TimestampUtc.ToLocalTime(),-25} Task: {backup.RelatedTaskId ?? "N/A",-15} Notes: {backup.Notes}"
            );
        }
    }
}

public static class StatusCommand
{
    static Option<bool> detailedOption = new Option<bool>(
        "--detailed",
        () => false,
        Program.GetLocalizedString("StatusDetailedOptionDescription")
    );
    static Option<string?> branchOption = new Option<string?>(
        "--branch",
        () => null,
        Program.GetLocalizedString("TaskListBranchOptionDescription")
    );
    static Option<bool> includeArchivedOption = new Option<bool>(
        "--include-archived",
        () => false,
        Program.GetLocalizedString("StatusIncludeArchivedOptionDescription")
    );

    public static Command Create()
    {
        var command = new Command("status", Program.GetLocalizedString("StatusCommandDescription"));
        command.AddOption(detailedOption);
        command.AddOption(branchOption);
        command.AddOption(includeArchivedOption);
        command.SetHandler(
            (context) =>
            {
                var detailed = context.ParseResult.GetValueForOption(detailedOption);
                var filterBranch = context.ParseResult.GetValueForOption(branchOption);
                var includeArchived = context.ParseResult.GetValueForOption(includeArchivedOption);
                HandleStatus(detailed, filterBranch, includeArchived);
            }
        );
        return command;
    }

    private static void HandleStatus(bool detailed, string? filterBranch, bool includeArchived)
    {
        var config = AIFlowConfigService.LoadConfig();
        if (config == null)
            return;
        Console.WriteLine(Program.GetLocalizedString("StatusProjectName", config.ProjectName));
        Console.WriteLine(Program.GetLocalizedString("StatusCurrentBranch", config.CurrentBranch));
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(Program.GetLocalizedString("StatusUnmanagedLocalChangesHeader"));
        Console.ResetColor();
        var unmanagedResources = new List<AIFlowResource>();
        foreach (var resource in config.Resources.OrderBy(r => r.Path))
        {
            if (
                resource.Type == ResourceType.LocalFile
                && !(
                    resource.Status == ResourceStatus.AiModified
                    || resource.Status == ResourceStatus.NeedsManualMerge
                    || resource.Status == ResourceStatus.Merged
                    || resource.Status == ResourceStatus.AwaitingAiChanges
                )
            )
            {
                var currentDiskHash =
                    FileService.CalculateFileHash(FileService.GetFullPath(resource.Path))
                    ?? "Missing";
                if (
                    currentDiskHash != "Missing"
                    && !string.IsNullOrEmpty(resource.LocalHash)
                    && resource.LocalHash != currentDiskHash
                )
                {
                    unmanagedResources.Add(resource);
                    Console.WriteLine(
                        Program.GetLocalizedString(
                            "StatusResourceLocallyModified",
                            resource.Path,
                            currentDiskHash.Substring(0, Math.Min(currentDiskHash.Length, 7)),
                            resource.LocalHash.Substring(0, Math.Min(resource.LocalHash.Length, 7))
                        )
                    );
                }
            }
        }
        if (!unmanagedResources.Any())
            Console.WriteLine(Program.GetLocalizedString("StatusNoUnmanagedChanges"));
        Console.WriteLine();
        Console.WriteLine(Program.GetLocalizedString("StatusTrackedResourcesHeader"));
        if (!config.Resources.Any())
            Console.WriteLine(Program.GetLocalizedString("StatusNoTrackedResources"));
        foreach (var resource in config.Resources.OrderBy(r => r.Path))
        {
            var currentDiskHash =
                resource.Type == ResourceType.LocalFile
                    ?
                        FileService.CalculateFileHash(FileService.GetFullPath(resource.Path))
                        ?? "Missing"

                    : "N/A";
            var statusIndicator = resource.Status;
            var conflictNote =
                resource.Status == ResourceStatus.NeedsManualMerge
                && resource.Type == ResourceType.LocalFile
                    ? " (Contains conflict markers)"
                    : "";
            //Console.WriteLine("  {resource.Path,-40} (Type: {resource.Type, -10} Status: {statusIndicator,-20} DiskHash: {(currentDiskHash == "N / A" ? "N / A" : currentDiskHash.Substring(0, Math.Min(currentDiskHash.Length,7)))}...){conflictNote}");
        }
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(Program.GetLocalizedString("StatusOverdueTasksHeader"));
        Console.ResetColor();
        var overdueTasksQuery = config.Tasks.Where(t =>
            t.DueDate.HasValue
            && t.DueDate.Value.Date < DateTime.UtcNow.Date
            && t.Status != TaskStatus.Done
            && t.Status != TaskStatus.Completed
        );
        if (!includeArchived)
            overdueTasksQuery = overdueTasksQuery.Where(t => t.Status != TaskStatus.Archived);
        var overdueTasks = overdueTasksQuery.OrderBy(t => t.DueDate).ToList();
        if (!overdueTasks.Any())
            Console.WriteLine(Program.GetLocalizedString("StatusNoOverdueTasks"));
        foreach (var task in overdueTasks)
        {
            Console.WriteLine(
                Program.GetLocalizedString(
                    "StatusOverdueTaskEntry",
                    task.TaskId,
                    task.DueDate.Value.ToShortDateString(),
                    task.Branch,
                    task.Description.Substring(0, Math.Min(task.Description.Length, 40))
                )
            );
        }
        Console.WriteLine();
        Console.WriteLine(Program.GetLocalizedString("StatusTasksHeader"));
        var tasksToDisplay = config.Tasks.AsEnumerable();
        if (!includeArchived)
        {
            tasksToDisplay = tasksToDisplay.Where(t => t.Status != TaskStatus.Archived);
        }
        if (!string.IsNullOrWhiteSpace(filterBranch))
        {
            tasksToDisplay = tasksToDisplay.Where(t =>
                t.Branch.Equals(filterBranch, StringComparison.OrdinalIgnoreCase)
            );
        }
        var tasksList = tasksToDisplay.ToList();
        if (!tasksList.Any())
            Console.WriteLine(
                includeArchived
                    ? Program.GetLocalizedString("StatusNoTasksIncludingArchived")
                    : Program.GetLocalizedString("StatusNoActiveTasks")
            );
        foreach (var task in tasksList.OrderByDescending(t => t.UpdatedAt))
        {
            var sb = new StringBuilder();
            sb.Append(
                "  Task ID: {task.TaskId,-10} Branch: {task.Branch,-15} Status: {task.Status,-15}"
            );
            if (!string.IsNullOrEmpty(task.Priority))
                sb.Append("Prio:task.Priority,âˆ’8");
            if (!string.IsNullOrEmpty(task.Type))
                sb.Append(" Type: {task.Type,-8}");
            sb.Append(
                " Desc: {task.Description.Substring(0, Math.Min(task.Description.Length, 30))}..."
            );
            Console.WriteLine(sb.ToString());
            if (detailed)
            {
                Console.Write("    Assigned: {task.AssignedTo}");
                if (task.StoryPoints.HasValue)
                    Console.Write(",SP:task.StoryPoints");
                if (!string.IsNullOrEmpty(task.Sprint))
                    Console.Write(", Sprint: {task.Sprint}");
                if (task.DueDate.HasValue)
                    Console.Write(",Due:task.DueDate.Value.ToShortDateString()");
                Console.WriteLine();
                if (task.Labels.Any())
                    Console.WriteLine("    Labels: {string.Join(", ", task.Labels)}");
                if (!string.IsNullOrEmpty(task.EpicLink))
                    Console.WriteLine("EpicLink:task.EpicLink");
                if (task.RelatedResources.Any())
                    Console.WriteLine("    Resources: {task.RelatedResources.Count}");
            }
        }
        Console.WriteLine();
        Console.WriteLine(Program.GetLocalizedString("StatusTaskSummaryHeader"));
        var statusSummary = config
            .Tasks.GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .OrderBy(s => s.Status)
            .ToList();
        if (!statusSummary.Any())
            Console.WriteLine(Program.GetLocalizedString("StatusNoTasksForSummary"));
        foreach (var summary in statusSummary)
        {
            Console.WriteLine(
                Program.GetLocalizedString("StatusTaskSummaryEntry", summary.Status, summary.Count)
            );
        }
        Console.WriteLine();
        if (config.ActiveAiRetrievalSession != null)
        {
            Console.WriteLine(
                Program.GetLocalizedString(
                    "StatusActiveRetrieval",
                    config.ActiveAiRetrievalSession.RetrievalGuid,
                    config.ActiveAiRetrievalSession.ReceivedBatchesCount,
                    config.ActiveAiRetrievalSession.TotalBatches
                )
            );
        }
    }
}

public static class SummaryCommand
{
    public static Command Create()
    {
        var command = new Command(
            "summary",
            Program.GetLocalizedString("SummaryCommandDescription")
        );
        command.SetHandler(() => HandleSummary());
        return command;
    }

    private static void HandleSummary()
    {
        var config = AIFlowConfigService.LoadConfig();
        if (config == null)
        {
            Console.Error.WriteLine(
                Program.GetLocalizedString("ErrorConfigNotLoadedForSummary")
            );
            return;
        }
        Console.WriteLine(Program.GetLocalizedString("SummaryHeader"));
        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine(
            Program.GetLocalizedString(
                "SummaryProjectInfo",
                config.ProjectName,
                config.CurrentBranch
            )
        );
        Console.WriteLine();
        Console.WriteLine(Program.GetLocalizedString("SummaryTaskOverviewHeader"));
        var activeTasks = config.Tasks.Where(t => t.Status != TaskStatus.Archived).ToList();
        Console.WriteLine(
            Program.GetLocalizedString("SummaryTotalActiveTasks", activeTasks.Count)
        );
        var taskStatusCounts = activeTasks
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .OrderBy(s => s.Status)
            .ToList();
        if (taskStatusCounts.Any())
        {
            foreach (var statusCount in taskStatusCounts)
            {
                Console.WriteLine($"  - {statusCount.Status}: {statusCount.Count}");
            }
        }
        else
        {
            Console.WriteLine(Program.GetLocalizedString("SummaryNoActiveTasksToSummarize"));
        }
        var overdueTasksCount = activeTasks.Count(t =>
            t.DueDate.HasValue
            && t.DueDate.Value.Date < DateTime.UtcNow.Date
            && t.Status != TaskStatus.Done
            && t.Status != TaskStatus.Completed
        );
        Console.WriteLine(
            Program.GetLocalizedString("SummaryOverdueTasksCount", overdueTasksCount)
        );
        Console.WriteLine();
        Console.WriteLine(Program.GetLocalizedString("SummaryResourceStatusHeader"));
        var awaitingAiChangesCount = config.Resources.Count(r =>
            r.Status == ResourceStatus.AwaitingAiChanges
        );
        var needsManualMergeCount = config.Resources.Count(r =>
            r.Status == ResourceStatus.NeedsManualMerge
        );
        var unmanagedLocalChangesCount = 0;
        foreach (var resource in config.Resources)
        {
            if (
                resource.Type == ResourceType.LocalFile
                && !(
                    resource.Status == ResourceStatus.AiModified
                    || resource.Status == ResourceStatus.NeedsManualMerge
                    || resource.Status == ResourceStatus.Merged
                    || resource.Status == ResourceStatus.AwaitingAiChanges
                )
            )
            {
                var currentDiskHash =
                    FileService.CalculateFileHash(FileService.GetFullPath(resource.Path))
                    ?? "Missing";
                if (
                    currentDiskHash != "Missing"
                    && !string.IsNullOrEmpty(resource.LocalHash)
                    && resource.LocalHash != currentDiskHash
                )
                {
                    unmanagedLocalChangesCount++;
                }
            }
        }
        Console.WriteLine(
            Program.GetLocalizedString("SummaryResourcesAwaitingAI", awaitingAiChangesCount)
        );
        Console.WriteLine(
            Program.GetLocalizedString("SummaryResourcesNeedMerge", needsManualMergeCount)
        );
        Console.WriteLine(
            Program.GetLocalizedString(
                "SummaryResourcesUnmanagedChanges",
                unmanagedLocalChangesCount
            )
        );
        Console.WriteLine();
        if (config.ActiveAiRetrievalSession != null)
        {
            Console.WriteLine(Program.GetLocalizedString("SummaryActiveRetrievalHeader"));
            Console.WriteLine(
                Program.GetLocalizedString(
                    "SummaryActiveRetrievalDetails",
                    config.ActiveAiRetrievalSession.ReceivedBatchesCount,
                    config.ActiveAiRetrievalSession.TotalBatches,
                    config.ActiveAiRetrievalSession.RetrievalGuid
                )
            );
            Console.WriteLine();
        }
        Console.WriteLine(Program.GetLocalizedString("SummaryUpcomingDueDatesHeader"));
        var upcomingTasks = activeTasks
            .Where(t => t.DueDate.HasValue && t.DueDate.Value.Date >= DateTime.UtcNow.Date)
            .OrderBy(t => t.DueDate.Value)
            .Take(3)
            .ToList();
        if (upcomingTasks.Any())
        {
            foreach (var task in upcomingTasks)
            {
                Console.WriteLine(
                    Program.GetLocalizedString(
                        "SummaryUpcomingTaskEntry",
                        task.DueDate.Value.ToShortDateString(),
                        task.TaskId,
                        task.Description.Substring(0, Math.Min(task.Description.Length, 40))
                    )
                );
            }
        }
        else
        {
            Console.WriteLine(Program.GetLocalizedString("SummaryNoUpcomingDueDates"));
        }
        Console.WriteLine("--------------------------------------------------");
    }
}

public static class TaskCommand
{
    static Option<string?> updateDescOption = new Option<string?>(
        "--desc",
        Program.GetLocalizedString("TaskUpdateDescOptionDescription")
    );
    static Option<string?> updateStatusOption = new Option<string?>(
        "--status",
        Program.GetLocalizedString("TaskUpdateStatusOptionDescription")
    );
    static Option<string?> updateTypeOption = new Option<string?>(
        "--type",
        Program.GetLocalizedString("TaskUpdateTypeOptionDescription_Optional")
    );
    static Option<int?> updateSpOption = new Option<int?>(
        "--sp",
        Program.GetLocalizedString("TaskUpdateSpOptionDescription")
    );
    static Option<string?> updatePriorityOption = new Option<string?>(
        "--priority",
        Program.GetLocalizedString("TaskUpdatePriorityOptionDescription_Optional")
    );
    static Option<string?> updateSprintOption = new Option<string?>(
        "--sprint",
        Program.GetLocalizedString("TaskUpdateSprintOptionDescription")
    );
    static Option<string?> updateEpicLinkOption = new Option<string?>(
        "--epic-link",
        Program.GetLocalizedString("TaskUpdateEpicLinkOptionDescription")
    );
    static Option<DateTime?> updateDueDateOption = new Option<DateTime?>(
        "--due-date",
        Program.GetLocalizedString("TaskUpdateDueDateOptionDescription")
    );
    static Option<string[]?> updateLabelsOption = new Option<string[]?>(
        "--labels",
        description: Program.GetLocalizedString("TaskUpdateLabelsOptionDescription")
    )
    {
        Arity = ArgumentArity.ZeroOrMore,
    };
    static Option<string?> updateAssigneeOption = new Option<string?>(
        "--assignee",
        Program.GetLocalizedString("TaskUpdateAssigneeOptionDescription")
    );
    static Option<string?> updateBranchOption = new Option<string?>(
        "--branch",
        Program.GetLocalizedString("TaskUpdateBranchOptionDescription")
    );
    static Option<string?> statusFilterOption = new Option<string?>(
        "--status",
        description: Program.GetLocalizedString("TaskListStatusOptionDescription")
    );
    static Option<string?> branchFilterOptionList = new Option<string?>(
        "--branch",
        description: Program.GetLocalizedString("TaskListBranchOptionDescription")
    );
    static Option<string?> assigneeFilterOption = new Option<string?>(
        "--assignee",
        description: Program.GetLocalizedString("TaskListAssigneeOptionDescription")
    );
    static Option<string?> priorityFilterOption = new Option<string?>(
        "--priority",
        description: Program.GetLocalizedString("TaskListPriorityOptionDescription")
    );
    static Option<string?> typeFilterOption = new Option<string?>(
        "--type",
        description: Program.GetLocalizedString("TaskListTypeOptionDescription")
    );
    static Option<string[]?> labelsFilterOption = new Option<string[]?>(
        "--label",
        description: Program.GetLocalizedString("TaskListLabelOptionDescription")
    )
    {
        Arity = ArgumentArity.ZeroOrMore,
    };
    static Option<string?> descContainsFilterOption = new Option<string?>(
        "--desc-contains",
        description: Program.GetLocalizedString("TaskListDescContainsOptionDescription")
    );
    static Option<DateTime?> createdAfterOption = new Option<DateTime?>(
        "--created-after",
        description: Program.GetLocalizedString("TaskListCreatedAfterOptionDescription")
    );
    static Option<DateTime?> createdBeforeOption = new Option<DateTime?>(
        "--created-before",
        description: Program.GetLocalizedString("TaskListCreatedBeforeOptionDescription")
    );
    static Option<DateTime?> updatedAfterOption = new Option<DateTime?>(
        "--updated-after",
        description: Program.GetLocalizedString("TaskListUpdatedAfterOptionDescription")
    );
    static Option<DateTime?> updatedBeforeOption = new Option<DateTime?>(
        "--updated-before",
        description: Program.GetLocalizedString("TaskListUpdatedBeforeOptionDescription")
    );
    static Option<DateTime?> dueAfterOption = new Option<DateTime?>(
        "--due-after",
        description: Program.GetLocalizedString("TaskListDueAfterOptionDescription")
    );
    static Option<DateTime?> dueBeforeOption = new Option<DateTime?>(
        "--due-before",
        description: Program.GetLocalizedString("TaskListDueBeforeOptionDescription")
    );
    static Option<bool> includeArchivedOptionList = new Option<bool>(
        "--include-archived",
        () => false,
        Program.GetLocalizedString("TaskListIncludeArchivedOptionDescription")
    );

    public static Command Create()
    {
        var taskCommand = new Command("task", Program.GetLocalizedString("TaskCommandDescription"));
        var listSubCommand = new Command(
            "list",
            Program.GetLocalizedString("TaskListSubCommandDescription")
        );
        listSubCommand.AddOption(statusFilterOption);
        listSubCommand.AddOption(branchFilterOptionList);
        listSubCommand.AddOption(assigneeFilterOption);
        listSubCommand.AddOption(priorityFilterOption);
        listSubCommand.AddOption(typeFilterOption);
        listSubCommand.AddOption(labelsFilterOption);
        listSubCommand.AddOption(descContainsFilterOption);
        listSubCommand.AddOption(createdAfterOption);
        listSubCommand.AddOption(createdBeforeOption);
        listSubCommand.AddOption(updatedAfterOption);
        listSubCommand.AddOption(updatedBeforeOption);
        listSubCommand.AddOption(dueAfterOption);
        listSubCommand.AddOption(dueBeforeOption);
        listSubCommand.AddOption(includeArchivedOptionList);
        listSubCommand.SetHandler(
            (context) =>
            {
                ListTasks(
                    context.ParseResult.GetValueForOption(statusFilterOption),
                    context.ParseResult.GetValueForOption(branchFilterOptionList),
                    context.ParseResult.GetValueForOption(assigneeFilterOption),
                    context.ParseResult.GetValueForOption(priorityFilterOption),
                    context.ParseResult.GetValueForOption(typeFilterOption),
                    context.ParseResult.GetValueForOption(labelsFilterOption),
                    context.ParseResult.GetValueForOption(descContainsFilterOption),
                    context.ParseResult.GetValueForOption(createdAfterOption),
                    context.ParseResult.GetValueForOption(createdBeforeOption),
                    context.ParseResult.GetValueForOption(updatedAfterOption),
                    context.ParseResult.GetValueForOption(updatedBeforeOption),
                    context.ParseResult.GetValueForOption(dueAfterOption),
                    context.ParseResult.GetValueForOption(dueBeforeOption),
                    context.ParseResult.GetValueForOption(includeArchivedOptionList)
                );
            }
        );
        taskCommand.AddCommand(listSubCommand);
        var viewSubCommand = new Command(
            "view",
            Program.GetLocalizedString("TaskViewSubCommandDescription")
        );
        var taskIdArgumentView = new Argument<string>(
            "task-id",
            Program.GetLocalizedString("TaskViewIdArgumentDescription")
        );
        viewSubCommand.AddArgument(taskIdArgumentView);
        viewSubCommand.SetHandler(
            (taskId) =>
            {
                ViewTask(taskId);
            },
            taskIdArgumentView
        );
        taskCommand.AddCommand(viewSubCommand);
        var updateSubCommand = new Command(
            "update",
            Program.GetLocalizedString("TaskUpdateSubCommandDescription")
        );
        var taskIdArgumentUpdate = new Argument<string>(
            "task-id",
            Program.GetLocalizedString("TaskUpdateIdArgumentDescription")
        );
        updateSubCommand.AddArgument(taskIdArgumentUpdate);
        updateSubCommand.AddOption(updateDescOption);
        updateSubCommand.AddOption(updateStatusOption);
        updateSubCommand.AddOption(updateTypeOption);
        updateSubCommand.AddOption(updateSpOption);
        updateSubCommand.AddOption(updatePriorityOption);
        updateSubCommand.AddOption(updateSprintOption);
        updateSubCommand.AddOption(updateEpicLinkOption);
        updateSubCommand.AddOption(updateDueDateOption);
        updateSubCommand.AddOption(updateLabelsOption);
        updateSubCommand.AddOption(updateAssigneeOption);
        updateSubCommand.AddOption(updateBranchOption);
        updateSubCommand.SetHandler(
            (context) =>
            {
                UpdateTask(context);
            }
        );
        taskCommand.AddCommand(updateSubCommand);
        var noteSubCommand = new Command(
            "note",
            Program.GetLocalizedString("TaskNoteSubCommandDescription")
        );
        var taskIdArgumentNote = new Argument<string>(
            "task-id",
            Program.GetLocalizedString("TaskNoteIdArgumentDescription")
        );
        var messageOptionNote = new Option<string>(
            aliases: new[] { "-m", "--message" },
            description: Program.GetLocalizedString("TaskNoteMessageOptionDescription")
        )
        {
            IsRequired = true,
        };
        noteSubCommand.AddArgument(taskIdArgumentNote);
        noteSubCommand.AddOption(messageOptionNote);
        noteSubCommand.SetHandler(
            (taskId, message) =>
            {
                AddNoteToTask(taskId, message);
            },
            taskIdArgumentNote,
            messageOptionNote
        );
        taskCommand.AddCommand(noteSubCommand);
        var archiveSubCommand = new Command(
            "archive",
            Program.GetLocalizedString("TaskArchiveSubCommandDescription")
        );
        var taskIdArgumentArchive = new Argument<string>(
            "task-id",
            Program.GetLocalizedString("TaskArchiveIdArgumentDescription")
        );
        archiveSubCommand.AddArgument(taskIdArgumentArchive);
        archiveSubCommand.SetHandler(
            (taskId) =>
            {
                ArchiveTask(taskId);
            },
            taskIdArgumentArchive
        );
        taskCommand.AddCommand(archiveSubCommand);
        return taskCommand;
    }

    private static void ListTasks(
        string? filterStatus,
        string? filterBranch,
        string? filterAssignee,
        string? filterPriority,
        string? filterType,
        string[]? filterLabels,
        string? filterDescContains,
        DateTime? createdAfter,
        DateTime? createdBefore,
        DateTime? updatedAfter,
        DateTime? updatedBefore,
        DateTime? dueAfter,
        DateTime? dueBefore,
        bool includeArchived
    )
    {
        var config = AIFlowConfigService.LoadConfig();
        if (config == null)
            return;
        Console.WriteLine(Program.GetLocalizedString("TaskListHeader"));
        var tasks = config.Tasks.AsQueryable();
        if (!includeArchived)
        {
            tasks = tasks.Where(t => t.Status != TaskStatus.Archived);
        }
        if (!string.IsNullOrEmpty(filterStatus))
            tasks = tasks.Where(t =>
                t.Status.Equals(filterStatus, StringComparison.OrdinalIgnoreCase)
            );
        if (!string.IsNullOrEmpty(filterBranch))
            tasks = tasks.Where(t =>
                t.Branch.Equals(filterBranch, StringComparison.OrdinalIgnoreCase)
            );
        if (!string.IsNullOrEmpty(filterAssignee))
            tasks = tasks.Where(t =>
                t.AssignedTo.Equals(filterAssignee, StringComparison.OrdinalIgnoreCase)
            );
        if (!string.IsNullOrEmpty(filterPriority))
            tasks = tasks.Where(t =>
                t.Priority != null
                && t.Priority.Equals(filterPriority, StringComparison.OrdinalIgnoreCase)
            );
        if (!string.IsNullOrEmpty(filterType))
            tasks = tasks.Where(t =>
                t.Type != null && t.Type.Equals(filterType, StringComparison.OrdinalIgnoreCase)
            );
        if (filterLabels != null && filterLabels.Any())
        {
            foreach (var label in filterLabels)
            {
                tasks = tasks.Where(t =>
                    t.Labels.Contains(label, StringComparer.OrdinalIgnoreCase)
                );
            }
        }
        if (!string.IsNullOrEmpty(filterDescContains))
            tasks = tasks.Where(t =>
                t.Description.Contains(filterDescContains, StringComparison.OrdinalIgnoreCase)
            );
        if (createdAfter.HasValue)
            tasks = tasks.Where(t => t.CreatedAt.Date >= createdAfter.Value.Date);
        if (createdBefore.HasValue)
            tasks = tasks.Where(t => t.CreatedAt.Date <= createdBefore.Value.Date);
        if (updatedAfter.HasValue)
            tasks = tasks.Where(t => t.UpdatedAt.Date >= updatedAfter.Value.Date);
        if (updatedBefore.HasValue)
            tasks = tasks.Where(t => t.UpdatedAt.Date <= updatedBefore.Value.Date);
        if (dueAfter.HasValue)
            tasks = tasks.Where(t =>
                t.DueDate.HasValue && t.DueDate.Value.Date >= dueAfter.Value.Date
            );
        if (dueBefore.HasValue)
            tasks = tasks.Where(t =>
                t.DueDate.HasValue && t.DueDate.Value.Date <= dueBefore.Value.Date
            );
        var taskList = tasks.ToList();
        if (!taskList.Any())
        {
            Console.WriteLine(
                includeArchived
                    ? Program.GetLocalizedString("TaskListNoTasksFoundIncludingArchived")
                    : Program.GetLocalizedString("TaskListNoActiveTasksFound")
            );
            return;
        }
        foreach (var task in taskList.OrderByDescending(t => t.UpdatedAt))
        {
            var sb = new StringBuilder();
            sb.Append("ID:task.TaskId,âˆ’10Branch:task.Branch,âˆ’15Status:task.Status,âˆ’15");
            if (!string.IsNullOrEmpty(task.Priority))
                sb.Append(" Prio: {task.Priority,-8}");
            if (!string.IsNullOrEmpty(task.Type))
                sb.Append("Type:task.Type,âˆ’8");
            sb.Append(
                " Desc: {task.Description.Substring(0, Math.Min(task.Description.Length, 25))}..."
            );
            Console.WriteLine(sb.ToString());
        }
    }

    private static void ViewTask(string taskId)
    {
        var config = AIFlowConfigService.LoadConfig();
        if (config == null)
            return;
        var task = config.Tasks.FirstOrDefault(t => t.TaskId == taskId);
        if (task == null)
        {
            Console.Error.WriteLine(Program.GetLocalizedString("TaskViewNotFound", taskId));
            return;
        }
        Console.WriteLine(Program.GetLocalizedString("TaskViewDetailsTitle", taskId));
        Console.WriteLine("Description:task.Description");
        Console.Write("  Branch: {task.Branch,-15} Status: {task.Status,-15}");
        if (!string.IsNullOrEmpty(task.Priority))
            Console.Write("Priority:task.Priority,âˆ’8");
        if (!string.IsNullOrEmpty(task.Type))
            Console.Write(" Type: {task.Type}");
        Console.WriteLine();
        Console.Write("AssignedTo:task.AssignedTo");
        if (task.StoryPoints.HasValue)
            Console.Write(", Story Points: {task.StoryPoints}");
        Console.WriteLine();
        if (!string.IsNullOrEmpty(task.Sprint))
            Console.Write("Sprint:task.Sprint");
        if (task.DueDate.HasValue)
            //Console.Write("{(string.IsNullOrEmpty(task.Sprint) ? "  " : ", ")}Due Date: {task.DueDate.Value.ToShortDateString()}");
            if (!string.IsNullOrEmpty(task.Sprint) || task.DueDate.HasValue)
                Console.WriteLine();
        if (!string.IsNullOrEmpty(task.EpicLink))
            Console.WriteLine("EpicLink:task.EpicLink");
        if (task.Labels.Any())
            Console.WriteLine("  Labels: {string.Join(", ", task.Labels)}");
        Console.WriteLine(
            "  Created: {task.CreatedAt.ToLocalTime()}, Updated: {task.UpdatedAt.ToLocalTime()}"
        );
        if (task.RelatedResources.Any())
        {
            Console.WriteLine(Program.GetLocalizedString("TaskViewRelatedResourcesHeader"));
            foreach (var rr in task.RelatedResources)
                Console.WriteLine("    - {rr.Path} (Type: {rr.Type}, Action: {rr.Action})");
        }
        if (!string.IsNullOrEmpty(task.HumanNotes))
            Console.WriteLine("HumanNotes:Environment.NewLinetask.HumanNotes");
        if (!string.IsNullOrEmpty(task.AiNotes))
            Console.WriteLine("  AI Notes: {Environment.NewLine}{task.AiNotes}");
    }

    private static void UpdateTask(InvocationContext context)
    {
        // Fix for CS1061: Use context.ParseResult.CommandResult.Command instead of context.Command
        var taskId = context.ParseResult.GetValueForArgument(
            context
                .ParseResult.CommandResult.Command.Arguments.OfType<Argument<string>>()
                .First(a => a.Name == "task-id")
        );

        // Fix for CS0305: Specify the type argument for List<>
        var config = AIFlowConfigService.LoadConfig();
        if (config == null)
            return;

        var task = config.Tasks.FirstOrDefault(t => t.TaskId == taskId);
        if (task == null)
        {
            Console.Error.WriteLine(Program.GetLocalizedString("TaskUpdateNotFound", taskId));
            return;
        }

        var updated = false;

        // Fix for IDE0007: Use 'var' instead of explicit type
        bool IsOptionProvided(Option option) => context.ParseResult.FindResultFor(option) != null;

        if (IsOptionProvided(updateDescOption))
        {
            task.Description =
                context.ParseResult.GetValueForOption(updateDescOption) ?? task.Description;
            updated = true;
        }
        if (IsOptionProvided(updateStatusOption))
        {
            task.Status = context.ParseResult.GetValueForOption(updateStatusOption) ?? task.Status;
            updated = true;
        }
        if (IsOptionProvided(updateTypeOption))
        {
            task.Type = context.ParseResult.GetValueForOption(updateTypeOption);
            updated = true;
        }
        if (IsOptionProvided(updateSpOption))
        {
            task.StoryPoints = context.ParseResult.GetValueForOption(updateSpOption);
            updated = true;
        }
        if (IsOptionProvided(updatePriorityOption))
        {
            task.Priority = context.ParseResult.GetValueForOption(updatePriorityOption);
            updated = true;
        }
        if (IsOptionProvided(updateSprintOption))
        {
            task.Sprint = context.ParseResult.GetValueForOption(updateSprintOption);
            updated = true;
        }
        if (IsOptionProvided(updateEpicLinkOption))
        {
            task.EpicLink = context.ParseResult.GetValueForOption(updateEpicLinkOption);
            updated = true;
        }
        if (IsOptionProvided(updateDueDateOption))
        {
            task.DueDate = context.ParseResult.GetValueForOption(updateDueDateOption);
            updated = true;
        }
        if (IsOptionProvided(updateLabelsOption))
        {
            task.Labels =
                context.ParseResult.GetValueForOption(updateLabelsOption)?.ToList()
                ?? new List<string>(); // Fix for CS0305: Specify type argument for List<>
            updated = true;
        }
        if (IsOptionProvided(updateAssigneeOption))
        {
            task.AssignedTo =
                context.ParseResult.GetValueForOption(updateAssigneeOption) ?? task.AssignedTo;
            updated = true;
        }
        if (IsOptionProvided(updateBranchOption))
        {
            task.Branch = context.ParseResult.GetValueForOption(updateBranchOption) ?? task.Branch;
            updated = true;
        }

        if (updated)
        {
            task.UpdatedAt = DateTime.UtcNow;
            if (AIFlowConfigService.SaveConfig(config))
            {
                Console.WriteLine(Program.GetLocalizedString("TaskUpdateSuccess", taskId));
            }
            else
            {
                Console.Error.WriteLine(Program.GetLocalizedString("TaskUpdateFailedSave", taskId));
            }
        }
        else
        {
            Console.WriteLine(Program.GetLocalizedString("TaskUpdateNoChanges", taskId));
        }
    }

    private static void AddNoteToTask(string taskId, string message)
    {
        var config = AIFlowConfigService.LoadConfig();
        if (config == null)
            return;
        var task = config.Tasks.FirstOrDefault(t => t.TaskId == taskId);
        if (task == null)
        {
            Console.Error.WriteLine(Program.GetLocalizedString("TaskNoteErrorNotFound", taskId));
            return;
        }
        var formattedNote = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Human]: {message}";
        if (string.IsNullOrEmpty(task.HumanNotes))
        {
            task.HumanNotes = formattedNote;
        }
        else
        {
            task.HumanNotes += Environment.NewLine + Environment.NewLine + formattedNote;
        }
        task.UpdatedAt = DateTime.UtcNow;
        if (AIFlowConfigService.SaveConfig(config))
        {
            Console.WriteLine(Program.GetLocalizedString("TaskNoteAddedSuccess", taskId));
        }
        else
        {
            Console.Error.WriteLine(Program.GetLocalizedString("TaskNoteErrorSaving", taskId));
        }
    }

    private static void ArchiveTask(string taskId)
    {
        var config = AIFlowConfigService.LoadConfig();
        if (config == null)
            return;
        var task = config.Tasks.FirstOrDefault(t => t.TaskId == taskId);
        if (task == null)
        {
            Console.Error.WriteLine(Program.GetLocalizedString("TaskArchiveErrorNotFound", taskId));
            return;
        }
        if (task.Status == TaskStatus.Archived)
        {
            Console.WriteLine(Program.GetLocalizedString("TaskArchiveAlreadyArchived", taskId));
            return;
        }
        task.Status = TaskStatus.Archived;
        task.UpdatedAt = DateTime.UtcNow;
        if (AIFlowConfigService.SaveConfig(config))
        {
            Console.WriteLine(Program.GetLocalizedString("TaskArchiveSuccess", taskId));
        }
        else
        {
            Console.Error.WriteLine(Program.GetLocalizedString("TaskArchiveErrorSaving", taskId));
        }
    }
}
