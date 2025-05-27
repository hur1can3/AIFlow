namespace AIFlow.Cli.Commands
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Linq;
    using AIFlow.Cli.Models;
    using AIFlow.Cli.Services;

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
                async (string? branchName, bool delete) =>
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
}
