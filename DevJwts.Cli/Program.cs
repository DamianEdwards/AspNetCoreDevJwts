using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.AddBranch<JwtSettings>("jwt", jwt =>
    {
        jwt.AddCommand<ListJwtCommand>("list");
        jwt.AddCommand<CreateJwtCommand>("create");
    });
});

return app.Run(args);

public class JwtSettings : CommandSettings
{
    public JwtSettings(string? project)
    {
        User = Environment.UserName;

        if (project is not null)
        {
            Project = Path.GetFullPath(project);
        }
        else
        {
            var csprojFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj");
            if (csprojFiles is { Length: 1 } projectPath)
            {
                Project = projectPath[0];
            }
        }
    }

    [CommandOption("-p|--project <PROJECT>")]
    public string? Project { get; init; }

    public string User { get; init; }

    public override ValidationResult Validate()
    {
        if (Project is { } project && !File.Exists(project))
        {
            return ValidationResult.Error($"Project {project} could not be found");
        }
        else if (Project is null)
        {
            return ValidationResult.Error($"A project could not be found or there were multiple projects in the current directory. Specify a project using the project option.");
        }

        return ValidationResult.Success();
    }
}

public class ListJwtSettings : JwtSettings
{
    public ListJwtSettings(string? project)
        : base(project)
    {

    }
}

public class CreateJwtSettings : JwtSettings
{
    public CreateJwtSettings(string? project, string? name)
        : base(project)
    {
        Name = name ?? User;
    }

    [CommandOption("-n|--name <NAME>")]
    public string? Name { get; init; }

    [CommandOption("-c|--claim <CLAIM>")]
    public string[]? Claims { get; init; }

    public override ValidationResult Validate()
    {
        return Name is { Length: > 0 }
            ? ValidationResult.Error("Current user name could not be determined, please specify a name for the JWT using the name option")
            : ValidationResult.Success();
    }
}

public class ListJwtCommand : Command<ListJwtSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] ListJwtSettings settings)
    {
        AnsiConsole.WriteLine($"{context.Name}");

        var userSecretsId = GetUserSecretsId(settings.Project!);

        if (userSecretsId is null)
        {
            AnsiConsole.WriteLine("Error: The specified project does not have a user secrets ID configured. Set a user secrets ID by running the command 'dotnet user-secrets init'.");
            return 1;
        }

        AnsiConsole.WriteLine($"Project file: {settings.Project}");
        AnsiConsole.WriteLine($"User secrets ID: {userSecretsId}");
        AnsiConsole.WriteLine($"Current user: {settings.User}");

        var projectConfiguration = new ConfigurationBuilder()
            .AddUserSecrets(userSecretsId)
            .Build();

        AnsiConsole.WriteLine(projectConfiguration.GetDebugView());

        return 0;
    }

    private string? GetUserSecretsId(string projectFilePath)
    {
        var projectDocument = XDocument.Load(projectFilePath, LoadOptions.PreserveWhitespace);
        var existingUserSecretsId = projectDocument.XPathSelectElements("//UserSecretsId").FirstOrDefault();
        
        if (existingUserSecretsId == null)
        {
            return null;
        }

        return existingUserSecretsId.Value;
    }

    public override ValidationResult Validate([NotNull] CommandContext context, [NotNull] ListJwtSettings settings)
    {
        return settings.Validate();
    }
}

public class CreateJwtCommand : Command<CreateJwtSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] CreateJwtSettings settings)
    {
        AnsiConsole.WriteLine($"{context.Name}");
        AnsiConsole.WriteLine($"Project file: {settings.Project}");
        AnsiConsole.WriteLine($"Current user: {settings.User}");

        return 0;
    }

    public override ValidationResult Validate([NotNull] CommandContext context, [NotNull] CreateJwtSettings settings)
    {
        return settings.Validate();
    }
}