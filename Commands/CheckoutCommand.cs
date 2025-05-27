namespace AIFlow.Cli.Commands
{
    using System.CommandLine;
    using System.Linq;
    using AIFlow.Cli.Models;
    using AIFlow.Cli.Services;

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
                (string branchName) =>
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
}
