using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Reflection;
using System.Resources;
using AIFlow.Cli.Commands;

public class Program
{
    private static ResourceManager? _resourceManager;

    public static string GetLocalizedString(string key, params object[] args)
    {
        _resourceManager ??= new ResourceManager(
            "AIFlow.Cli.Properties.Resources",
            Assembly.GetExecutingAssembly()
        );
        CultureInfo culture = CultureInfo.CurrentUICulture;
        try
        {
            string? format = _resourceManager.GetString(key, culture);
            if (format == null)
                return $"[{key}]"; // Fallback if key not found
            return args.Length > 0 ? string.Format(culture, format, args) : format;
        }
        catch (MissingManifestResourceException)
        {
            // Fallback for environments where resources might not be found
            if (key == "CliDescription")
                return "AIFlow CLI - Manages collaborative workflows with AI (fallback).";
            // Console.Error.WriteLine($"Warning: Resource manifest for 'AIFlow.Cli.Properties.Resources' not found. Using fallback for key '{key}'.");
            return $"[[MissingResources:{key}]]";
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Warning: Error loading localized string for key '{key}': {ex.Message}"
            );
            return $"[[ErrorLoadingResource:{key}]]";
        }
    }

    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand(GetLocalizedString("CliDescription"));
        rootCommand.AddCommand(InitCommand.Create());
        rootCommand.AddCommand(PrepareInputCommand.Create());
        rootCommand.AddCommand(IntegrateOutputCommand.Create());
        rootCommand.AddCommand(FetchOutputCommand.Create());
        rootCommand.AddCommand(StatusCommand.Create());
        rootCommand.AddCommand(BranchCommand.Create());
        rootCommand.AddCommand(CheckoutCommand.Create());
        rootCommand.AddCommand(TaskCommand.Create());
        rootCommand.AddCommand(ResolveCommand.Create());
        rootCommand.AddCommand(RevertIntegrationCommand.Create());
        rootCommand.AddCommand(SummaryCommand.Create());

        var commandLineBuilder = new CommandLineBuilder(rootCommand);
        commandLineBuilder.UseDefaults();
        var parser = commandLineBuilder.Build();
        return await parser.InvokeAsync(args);
    }
}
