// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AIFlow.Cli.Commands;

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Text;
using AIFlow.Cli.Models;
using AIFlow.Cli.Services;

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
            (InvocationContext context) =>
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
                string currentDiskHash =
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
            string currentDiskHash =
                resource.Type == ResourceType.LocalFile
                    ? (
                        FileService.CalculateFileHash(FileService.GetFullPath(resource.Path))
                        ?? "Missing"
                    )
                    : "N/A";
            string statusIndicator = resource.Status;
            string conflictNote =
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
