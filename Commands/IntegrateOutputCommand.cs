namespace AIFlow.Cli.Commands
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using AIFlow.Cli.Models;
    using AIFlow.Cli.Services;
    using AIFlow.Cli.Services.Payloads;

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
                (InvocationContext context) =>
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
            AIFlowFile configForSimulation = isDryRun
                ? JsonSerializer.Deserialize<AIFlowFile>(JsonSerializer.Serialize(config))!
                : config;
            AiPreliminaryResponse? prelimResponse = null;
            AiOutputPackage? mainOutputPackage = null;
            AiBatchResponse? batchResponse = null;
            bool changesWouldBeAppliedOrMergeFileCreated = false;
            bool successfullyParsed = false;
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
                    using JsonDocument doc = JsonDocument.Parse(aiJsonPayload);
                    JsonElement root = doc.RootElement;
                    bool hasAiChangesetId =
                        root.TryGetProperty("aiChangesetId", out _)
                        || (
                            root.TryGetProperty("aiOutputPackage", out var pkg)
                            && pkg.TryGetProperty("aiChangesetId", out _)
                        )
                        || (
                            root.TryGetProperty("aiBatchResponse", out var batch)
                            && batch.TryGetProperty("aiChangesetId", out _)
                        )
                        || (
                            root.TryGetProperty("aiPreliminaryResponse", out var prelim)
                            && prelim.TryGetProperty("aiChangesetId", out _)
                        );
                    bool hasFileChanges =
                        root.TryGetProperty("fileChanges", out JsonElement fc)
                            && fc.ValueKind == JsonValueKind.Array
                        || (
                            root.TryGetProperty("aiOutputPackage", out pkg)
                            && pkg.TryGetProperty("fileChanges", out fc)
                            && fc.ValueKind == JsonValueKind.Array
                        )
                        || (
                            root.TryGetProperty("aiBatchResponse", out batch)
                            && batch.TryGetProperty("payload", out var batchPayload)
                            && batchPayload.TryGetProperty("fileChanges", out fc)
                            && fc.ValueKind == JsonValueKind.Array
                        );
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
            bool anyResourceSkippedOrMerged = false;
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
                    string aiResourceContent = FileService.Base64Decode(change.ContentBase64);
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
                    bool applyAIChangesDirectly = true;
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
                string originalTaskStatus = task.Status;
                string newTaskStatus =
                    mainOutputPackage.TaskUpdates.NewStatus ?? originalTaskStatus;
                string? newAiNotes = mainOutputPackage.TaskUpdates.AiNotes ?? task.AiNotes;
                DateTime newUpdatedAt = mainOutputPackage.TaskUpdates.UpdatedAt ?? DateTime.UtcNow;
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
}
