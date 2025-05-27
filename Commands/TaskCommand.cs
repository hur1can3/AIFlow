// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AIFlow.Cli.Commands;

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Text;
using AIFlow.Cli.Models;
using AIFlow.Cli.Services;

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
            (InvocationContext context) =>
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
                    context.ParseResult.GetValueForOption<bool>(includeArchivedOptionList)
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
            (string taskId) =>
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
            (InvocationContext context) =>
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
            (string taskId, string message) =>
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
            (string taskId) =>
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

        bool updated = false;

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
        string formattedNote = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Human]: {message}";
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
