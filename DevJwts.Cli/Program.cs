using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Principal;
using System.Text.Json;
using System.Xml.Linq;
using System.Xml.XPath;
using DevJwts;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.AddBranch<JwtSettings>("jwt", jwt =>
    {
        jwt.AddCommand<ListJwtCommand>("list");
        jwt.AddCommand<CreateJwtCommand>("create");
        jwt.AddCommand<PrintJwtCommand>("print");
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
            if (csprojFiles is [var path])
            {
                Project = path;
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

        return base.Validate();
    }
}

public class ListJwtSettings : JwtSettings
{
    public ListJwtSettings(string? project)
        : base(project)
    {

    }

    [CommandOption("--show-tokens")]
    [Description("Indicates whether JWT base64 strings should be shown")]
    public bool? ShowTokens { get; set; }
}

public class CreateJwtSettings : JwtSettings
{
    public CreateJwtSettings(string? project, string? name, string? audience)
        : base(project)
    {
        Name = name ?? User;
        // TODO: Revisit how to set default audience, e.g. reading from project launchSettings.json file to get applicationUrl
        Audience = audience ?? "https://localhost:5001";
    }

    [CommandOption("-n|--name <NAME>")]
    public string Name { get; init; }

    [CommandOption("-a|--audience <AUDIENCE>")]
    public string Audience { get; init; }

    [CommandOption("-c|--claim <CLAIM>")]
    public string[]? Claims { get; init; }

    public override ValidationResult Validate()
    {
        return Name is null or { Length: <1 }
            ? ValidationResult.Error("Current user name could not be determined, please specify a name for the JWT using the name option")
            : base.Validate();
    }
}

public class PrintJwtSettings : JwtSettings
{
    public PrintJwtSettings(string? project, string id)
        : base(project)
    {
        Id = id;
    }

    [CommandArgument(0, "[id]")]
    [Description("The ID of the JWT to print")]
    public string Id { get; }

    public override ValidationResult Validate()
    {
        return Id is null or { Length: < 1 }
            ? ValidationResult.Error("ID was not specified, please specify the ID of a JWT to print")
            : base.Validate();
    }
}

public class ListJwtCommand : Command<ListJwtSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] ListJwtSettings settings)
    {
        var userSecretsId = DevJwtCliHelpers.GetUserSecretsId(settings.Project!);

        if (userSecretsId is null)
        {
            AnsiConsole.WriteLine("Error: The specified project does not have a user secrets ID configured. Set a user secrets ID by running the command 'dotnet user-secrets init'.");
            return 1;
        }

        //AnsiConsole.WriteLine($"Project file: {settings.Project}");
        //AnsiConsole.WriteLine($"User secrets ID: {userSecretsId}");
        //AnsiConsole.WriteLine($"Current user: {settings.User}");
        //var projectConfiguration = new ConfigurationBuilder()
        //    .AddUserSecrets(userSecretsId)
        //    .Build();
        //AnsiConsole.WriteLine(projectConfiguration.GetDebugView());

        var jwtStore = new JwtStore(userSecretsId);

        if (jwtStore.Jwts is { Count: >0 } jwts)
        {
            AnsiConsole.MarkupLine($"Dev JWTs");
            var settingsTable = new Table();
            settingsTable.ShowHeaders = false;
            settingsTable.Border(TableBorder.None);
            settingsTable.AddColumn("Name");
            settingsTable.AddColumn("Value");
            settingsTable.AddRow(new Markup("[grey]Project:[/]"), new TextPath(settings.Project!));
            settingsTable.AddRow(new Markup("[grey]User Secrets ID:[/]"), new Text(userSecretsId));
            AnsiConsole.Write(settingsTable);
            AnsiConsole.WriteLine();

            var table = new Table();
            table.AddColumn("Id");
            table.AddColumn("Name");
            table.AddColumn("Audience");
            table.AddColumn("Issued");
            table.AddColumn("Expires");

            if (settings.ShowTokens == true)
            {
                table.AddColumn("Encoded Token");
            }

            foreach (var jwtRow in jwts)
            {
                var jwt = jwtRow.Value;
                if (settings.ShowTokens == true)
                {
                    table.AddRow(jwt.Id, jwt.Name, jwt.Audience, jwt.Issued.ToString("O"), jwt.Expires.ToString("O"), jwt.Token);
                }
                else
                {
                    table.AddRow(jwt.Id, jwt.Name, jwt.Audience, jwt.Issued.ToString("O"), jwt.Expires.ToString("O"));
                }
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"[yellow]{jwts.Count}[/] JWT(s) listed");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]No JWTs created yet![/]");
        }

        return 0;
    }

    public override ValidationResult Validate([NotNull] CommandContext context, [NotNull] ListJwtSettings settings)
    {
        return base.Validate(context, settings);
    }
}

public class CreateJwtCommand : Command<CreateJwtSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] CreateJwtSettings settings)
    {
        //AnsiConsole.WriteLine($"{context.Name}");

        var userSecretsId = DevJwtCliHelpers.GetUserSecretsId(settings.Project!);

        if (userSecretsId is null)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] The specified project does not have a user secrets ID configured. Set a user secrets ID by running the command [yello]'dotnet user-secrets init'[/].");
            return 1;
        }

        //AnsiConsole.WriteLine($"Project file: {settings.Project}");
        //AnsiConsole.WriteLine($"Current user: {settings.User}");

        var keyMaterial = DevJwtCliHelpers.GetOrCreateSigningKeyMaterial(userSecretsId);

        var jwtIssuer = new JwtIssuer(DevJwtsDefaults.Issuer, keyMaterial);
        // TODO: Build claims dictionary from command options
        var jwt = jwtIssuer.Create(settings.Name, settings.Audience);
        var jwtStore = new JwtStore(userSecretsId);
        jwtStore.Jwts.Add(jwt.Id, jwt);
        jwtStore.Save();

        AnsiConsole.MarkupLineInterpolated($"[green]JWT successfully created:[/]");
        DevJwtCliHelpers.PrintJwt(jwt);

        return 0;
    }

    public override ValidationResult Validate([NotNull] CommandContext context, [NotNull] CreateJwtSettings settings)
    {
        return base.Validate(context, settings);
    }
}

public class PrintJwtCommand : Command<PrintJwtSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] PrintJwtSettings settings)
    {
        var userSecretsId = DevJwtCliHelpers.GetUserSecretsId(settings.Project!);

        if (userSecretsId is null)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] The specified project does not have a user secrets ID configured. Set a user secrets ID by running the command [yello]'dotnet user-secrets init'[/].");
            return 1;
        }

        var jwtStore = new JwtStore(userSecretsId);

        if (!jwtStore.Jwts.ContainsKey(settings.Id))
        {
            AnsiConsole.MarkupLineInterpolated($"[red bold]Error:[/] The JWT with ID [yellow]{settings.Id}[/] was not found");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Use the [yellow]list[/] command to list all JWTs for this project");
            AnsiConsole.Markup("[grey]Project:[/] ");
            AnsiConsole.Write(new TextPath(settings.Project!));
            return 1;
        }

        AnsiConsole.MarkupLineInterpolated($"[green]Found JWT with ID[/] [yellow]{settings.Id}[/]");
        DevJwtCliHelpers.PrintJwt(jwtStore.Jwts[settings.Id]);

        return 0;
    }

    public override ValidationResult Validate([NotNull] CommandContext context, [NotNull] PrintJwtSettings settings)
    {
        return settings.Validate();
    }
}

internal static class DevJwtCliHelpers
{
    public static string? GetUserSecretsId(string projectFilePath)
    {
        var projectDocument = XDocument.Load(projectFilePath, LoadOptions.PreserveWhitespace);
        var existingUserSecretsId = projectDocument.XPathSelectElements("//UserSecretsId").FirstOrDefault();

        if (existingUserSecretsId == null)
        {
            return null;
        }

        return existingUserSecretsId.Value;
    }

    public static byte[] GetOrCreateSigningKeyMaterial(string userSecretsId)
    {
        var projectConfiguration = new ConfigurationBuilder()
            .AddUserSecrets(userSecretsId)
            .Build();

        var signingKeyMaterial = projectConfiguration[DevJwtsDefaults.SigningKeyConfigurationKey];

        var keyMaterial = new byte[16];
        if (signingKeyMaterial is not null && Convert.TryFromBase64String(signingKeyMaterial, keyMaterial, out var bytesWritten) && bytesWritten == DevJwtsDefaults.SigningKeyLength)
        {
            return keyMaterial;
        }

        // Create signing material and save to user secrets
        var newKeyMaterial = System.Security.Cryptography.RandomNumberGenerator.GetBytes(DevJwtsDefaults.SigningKeyLength);
        var secretsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "UserSecrets", userSecretsId, "secrets.json");
        using var secretsFileStream = new FileStream(secretsFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        IDictionary<string, string> secrets;
        if (secretsFileStream.Length > 0)
        {
            secrets = JsonSerializer.Deserialize<IDictionary<string, string>>(secretsFileStream) ?? new Dictionary<string, string>();
        }
        else
        {
            secrets = new Dictionary<string, string>();
        }
        secrets.Add(DevJwtsDefaults.SigningKeyConfigurationKey, Convert.ToBase64String(newKeyMaterial));
        JsonSerializer.Serialize(secretsFileStream, secrets);

        return newKeyMaterial;
    }

    public static void PrintJwt(Jwt jwt)
    {
        var table = new Table();
        table.Border(TableBorder.None);
        table.HideHeaders();
        table.AddColumn(new TableColumn("Key"));
        table.AddColumn(new TableColumn("Value"));
        table.AddRow(new Markup("[bold grey]Id:[/]"), new Text(jwt.Id));
        table.AddRow(new Markup("[bold grey]Name:[/]"), new Text(jwt.Name));
        table.AddRow(new Markup("[bold grey]Audience:[/]"), new Text(jwt.Audience));
        table.AddRow(new Markup("[bold grey]Expires:[/]"), new Text(jwt.Expires.ToString("O")));
        // TODO: Print claims
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[bold grey]Token:[/]");
        Console.WriteLine(jwt.Token);
    }
}

public record Jwt(string Id, string Name, string Audience, DateTimeOffset NotBefore, DateTimeOffset Expires, DateTimeOffset Issued, string Token)
{
    public IDictionary<string, string> Claims { get; } = new Dictionary<string, string>();

    public override string ToString() => Token;
}

public class JwtIssuer
{
    private static readonly int _jwtExpireInDays = 28;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtIssuer(string issuer, byte[] signingKeyMaterial)
    {
        Issuer = issuer;
        _signingKey = new SymmetricSecurityKey(signingKeyMaterial);
    }

    public string Issuer { get; }

    public Jwt Create(string name, string audience, IDictionary<string, string>? claims = null)
    {
        var identity = new GenericIdentity(name);
        if (claims is { Count: > 0 } claimsToAdd)
        {
            identity.AddClaims(claimsToAdd.Select(kvp => new Claim(kvp.Key, kvp.Value)));
        }

        var issued = DateTime.UtcNow;
        var notBefore = issued;
        var expires = issued.AddDays(_jwtExpireInDays);
        
        var handler = new JwtSecurityTokenHandler();
        var jwtSigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256Signature);
        var token = handler.CreateEncodedJwt(Issuer, audience, identity, notBefore, expires, issued, jwtSigningCredentials);

        var id = Guid.NewGuid().ToString().GetHashCode().ToString("x");
        var jwt = new Jwt(id, name, audience, notBefore, expires, issued, token);
        return jwt;
    }
}

public class JwtStore
{
    private static readonly string FileName = "dev-jwts.json";
    private readonly string _userSecretsId;
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonSerializerOptions = JsonSerializerOptions.Default;

    public JwtStore(string userSecretsId)
    {
        _userSecretsId = userSecretsId;
        _filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "UserSecrets", _userSecretsId, FileName);
        Load();
    }

    public IDictionary<string, Jwt> Jwts { get; private set; } = new Dictionary<string, Jwt>();

    public void Load()
    {
        if (File.Exists(_filePath))
        {
            using var fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read);
            Jwts = JsonSerializer.Deserialize<IDictionary<string, Jwt>>(fileStream, _jsonSerializerOptions) ?? new Dictionary<string, Jwt>();
        }
    }

    public void Save()
    {
        if (Jwts is not null)
        {
            using var fileStream = new FileStream(_filePath, FileMode.OpenOrCreate, FileAccess.Write);
            JsonSerializer.Serialize<IDictionary<string, Jwt>>(fileStream, Jwts, _jsonSerializerOptions);
        }
    }
}
