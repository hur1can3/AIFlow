namespace AIFlow.Cli.Commands
{
    using System.CommandLine;
    using System.Linq;
    using AIFlow.Cli.Models;
    using AIFlow.Cli.Services;

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
                async (string retrievalId, int batchNumber) =>
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
}
