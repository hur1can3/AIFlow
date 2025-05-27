namespace AIFlow.Cli.Commands
{
    using System;
    using System.CommandLine;
    using System.IO;
    using System.Linq;
    using System.Text;
    using AIFlow.Cli.Models;
    using AIFlow.Cli.Services;

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
            int awaitingAiChangesCount = config.Resources.Count(r =>
                r.Status == ResourceStatus.AwaitingAiChanges
            );
            int needsManualMergeCount = config.Resources.Count(r =>
                r.Status == ResourceStatus.NeedsManualMerge
            );
            int unmanagedLocalChangesCount = 0;
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
                    string currentDiskHash =
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
}
