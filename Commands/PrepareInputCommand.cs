namespace AIFlow.Cli.Commands
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.CommandLine.Parsing;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using AIFlow.Cli.Models;
    using AIFlow.Cli.Services;
    using AIFlow.Cli.Services.Payloads;
    using Microsoft.Extensions.FileSystemGlobbing;

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
                async (InvocationContext context) =>
                {
                    bool isInteractive =
                        context.ParseResult.CommandResult.Children.Count == 0
                        || (
                            context.ParseResult.HasOption(taskDescOption)
                            && !context.ParseResult.HasOption(taskIdOption)
                            && !context.ParseResult.HasOption(resourcesOption)
                            && !context.ParseResult.HasOption(newResourcesOption)
                            && !context.ParseResult.HasOption(continueTaskOption)
                        );
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
            string? taskId = context.ParseResult.GetValueForOption(taskIdOption);
            string? taskDesc = context.ParseResult.GetValueForOption(taskDescOption);
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
            string? type = PromptOptional(
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
            int? storyPoints = PromptOptionalInt(Program.GetLocalizedString("PromptStoryPoints"));
            string? priority = PromptOptional(
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
            string? sprint = PromptOptional(Program.GetLocalizedString("PromptSprint"));
            string? epicLink = PromptOptional(Program.GetLocalizedString("PromptEpicLink"));
            DateTime? dueDate = PromptOptionalDate(Program.GetLocalizedString("PromptDueDate"));
            var labels = PromptForMultiple(
                Program.GetLocalizedString("PromptLabels"),
                Program.GetLocalizedString("PromptLabelToAdd")
            );
            string? assignee = PromptOptional(Program.GetLocalizedString("PromptAssignee"), "ai");
            string? branch = PromptOptional(
                Program.GetLocalizedString("PromptBranch"),
                config.CurrentBranch
            );
            string? humanNotes = PromptOptional(Program.GetLocalizedString("PromptHumanNotes"));
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
                : (
                    input == defaultValue && string.IsNullOrWhiteSpace(Console.ReadLine()?.Trim())
                        ? defaultValue
                        : input
                );
        }

        private static int? PromptOptionalInt(string message)
        { /* ... same ... */
            var input = PromptOptional(message);
            return int.TryParse(input, out int val) ? val : null;
        }

        private static DateTime? PromptOptionalDate(string message)
        { /* ... same ... */
            var input = PromptOptional(message + " (YYYY-MM-DD)");
            return DateTime.TryParse(input, out DateTime val) ? val : null;
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
            Matcher ignoreMatcher = IgnoreService.LoadIgnoreMatcher(projectRoot);
            AIFlowTask? taskToProcess;
            bool isNewTask = false;
            bool isContinuingSplitRequest = !string.IsNullOrEmpty(continueTask);
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
                    bool hasBranchOpt =
                        cliContext?.ParseResult.HasOption(PrepareInputCommand.branchOption)
                        ?? (cliContext == null && branch != null);
                    if (hasBranchOpt)
                        taskToProcess.Branch = branch ?? taskToProcess.Branch;
                    bool hasTypeOpt =
                        cliContext?.ParseResult.HasOption(PrepareInputCommand.typeOption)
                        ?? (cliContext == null && taskType != null);
                    if (hasTypeOpt)
                        taskToProcess.Type = taskType;
                    else if (isNewTask && !hasTypeOpt)
                        taskToProcess.Type = null;
                    bool hasSpOpt =
                        cliContext?.ParseResult.HasOption(PrepareInputCommand.storyPointsOption)
                        ?? (cliContext == null && storyPoints.HasValue);
                    if (hasSpOpt)
                        taskToProcess.StoryPoints = storyPoints;
                    else if (isNewTask && !hasSpOpt)
                        taskToProcess.StoryPoints = null;
                    bool hasPrioOpt =
                        cliContext?.ParseResult.HasOption(PrepareInputCommand.priorityOption)
                        ?? (cliContext == null && priority != null);
                    if (hasPrioOpt)
                        taskToProcess.Priority = priority;
                    else if (isNewTask && !hasPrioOpt)
                        taskToProcess.Priority = null;
                    bool hasSprintOpt =
                        cliContext?.ParseResult.HasOption(PrepareInputCommand.sprintOption)
                        ?? (cliContext == null && sprint != null);
                    if (hasSprintOpt)
                        taskToProcess.Sprint = sprint;
                    else if (isNewTask && !hasSprintOpt)
                        taskToProcess.Sprint = null;
                    bool hasEpicOpt =
                        cliContext?.ParseResult.HasOption(PrepareInputCommand.epicLinkOption)
                        ?? (cliContext == null && epicLink != null);
                    if (hasEpicOpt)
                        taskToProcess.EpicLink = epicLink;
                    else if (isNewTask && !hasEpicOpt)
                        taskToProcess.EpicLink = null;
                    bool hasDueOpt =
                        cliContext?.ParseResult.HasOption(PrepareInputCommand.dueDateOption)
                        ?? (cliContext == null && dueDate.HasValue);
                    if (hasDueOpt)
                        taskToProcess.DueDate = dueDate;
                    else if (isNewTask && !hasDueOpt)
                        taskToProcess.DueDate = null;
                    bool hasLabelsOpt =
                        cliContext?.ParseResult.HasOption(PrepareInputCommand.labelsOption)
                        ?? (cliContext == null && labels != null);
                    if (hasLabelsOpt)
                        taskToProcess.Labels = labels?.ToList() ?? taskToProcess.Labels;
                    else if (isNewTask && !hasLabelsOpt)
                        taskToProcess.Labels = new List<string>();
                    bool hasAssigneeOpt =
                        cliContext?.ParseResult.HasOption(PrepareInputCommand.assigneeOption)
                        ?? (cliContext == null && assignee != null);
                    if (hasAssigneeOpt)
                        taskToProcess.AssignedTo = assignee ?? taskToProcess.AssignedTo;
                    else if (isNewTask && !hasAssigneeOpt)
                        taskToProcess.AssignedTo = "ai";
                    bool hasNotesOpt =
                        cliContext?.ParseResult.HasOption(PrepareInputCommand.humanNotesOption)
                        ?? (cliContext == null && humanNotes != null);
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
            int currentPartNumber = isContinuingSplitRequest
                ? taskToProcess.HumanRequestPartsSent + 1
                : 1;
            bool needsSplitting = false;
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
                        string encodedContent = FileService.Base64Encode(resourceData.content);
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
                    string encodedContent = FileService.Base64Encode(resourceData.content);
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
                bool moreFilesToSend = taskToProcess.RelatedResources.Any(rr =>
                    rr.SentInPart == null
                    && (
                        rr.ContentDetail != null
                        || (
                            rr.Type == ResourceType.LocalFile
                            && allResourcesForTask.FirstOrDefault(f => f.path == rr.Path).content
                                != null
                        )
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
                    || (currentPartNumber == 1 && rr.SentInPart == null && rr.ContentDetail == null)
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
            int estimatedTokens = EstimateTokens(humanRequestPayload);
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
            int totalChars = 0;
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
                            string decodedContent = FileService.Base64Decode(
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
                            string decodedContent = FileService.Base64Decode(
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
}
