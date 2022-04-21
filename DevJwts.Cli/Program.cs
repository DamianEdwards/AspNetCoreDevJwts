using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
    config.SetApplicationName("dev-jwts");

    config.AddExample(new[] { "create" });
    config.AddExample(new[] { "create", "-n testuser", "--claim scope=myapi:read" });
    config.AddExample(new[] { "list" });
    config.AddExample(new[] { "delete", "caa676ee" });
    config.AddExample(new[] { "clear" });

    config.AddCommand<ListJwtCommand>("list")
        .WithDescription("Lists all dev JWTs for the specified project")
        .WithExample(new[] { "list" });
    
    config.AddCommand<CreateJwtCommand>("create")
        .WithDescription("Creates a dev JWT for the specified project")
        .WithExample(new[] { "create" })
        .WithExample(new[] { "create", "-n testuser" })
        .WithExample(new[] { "create", "-n testuser", "--claim scope=myapi:read" });
    
    config.AddCommand<PrintJwtCommand>("print")
        .WithDescription("Prints the details of the specified dev JWT")
        .WithExample(new[] { "print", "caa676ee" });
    
    config.AddCommand<DeleteJwtCommand>("delete")
        .WithDescription("Deletes the dev JWT with the specified ID in the specified project")
        .WithExample(new[] { "delete", "caa676ee" });
    
    config.AddCommand<ClearJwtCommand>("clear")
        .WithDescription("Deletes all dev JWTs for the specified project")
        .WithExample(new[] { "clear" })
        .WithExample(new[] { "clear", "--force" }); ;
    
    config.AddCommand<KeyCommand>("key")
        .WithDescription("Prints the key used for signing dev JWTs for the specified project");
});

return app.Run(args);

public class ListJwtCommand : Command<ListJwtCommand.Settings>
{
    public class Settings : JwtSettings
    {
        public Settings(string? project)
            : base(project)
        {

        }

        [CommandOption("--show-tokens")]
        [Description("Indicates whether JWT base64 strings should be shown")]
        public bool? ShowTokens { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        var userSecretsId = DevJwtCliHelpers.GetUserSecretsId(settings.Project!);

        if (userSecretsId is null)
        {
            AnsiConsole.WriteLine("Error: The specified project does not have a user secrets ID configured. Set a user secrets ID by running the command 'dotnet user-secrets init'.");
            return 1;
        }

        var jwtStore = new JwtStore(userSecretsId);

        var settingsTable = new Table();
        settingsTable.ShowHeaders = false;
        settingsTable.Border(TableBorder.None);
        settingsTable.AddColumn("Name");
        settingsTable.AddColumn("Value");
        settingsTable.AddRow(new Markup("[grey]Project:[/]"), new TextPath(settings.Project!));
        settingsTable.AddRow(new Markup("[grey]User Secrets ID:[/]"), new Text(userSecretsId));
        AnsiConsole.Write(settingsTable);
        AnsiConsole.WriteLine();

        if (jwtStore.Jwts is { Count: >0 } jwts)
        {
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

    public override ValidationResult Validate([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        return base.Validate(context, settings);
    }
}

public class CreateJwtCommand : Command<CreateJwtCommand.Settings>
{
    private static readonly string[] _dateTimeFormats = new[] {
        "yyyy-MM-dd", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy/MM/dd", "yyyy/MM/dd HH:mm", "yyyy/MM/dd HH:mm:ss" };
    private static readonly string[] _timeSpanFormats = new[] {
        @"d\dh\hm\ms\s", @"d\dh\hm\m", @"d\dh\h", @"d\d",
        @"h\hm\ms\s", @"h\hm\m", @"h\h",
        @"m\ms\s", @"m\m",
        @"s\s"
    };

    public class Settings : JwtSettings
    {
        public Settings(string? project, string? name, string? audience, string? issuer, string? notBeforeOption, string? expiresOnOption, string? validFor)
            : base(project)
        {
            Name = name ?? User;
            Audience = audience ?? DevJwtCliHelpers.GetApplicationUrl(Project!);
            Issuer = issuer ?? DevJwtsDefaults.Issuer;
            
            if (notBeforeOption is { })
            {
                if (DateTime.TryParseExact(notBeforeOption, _dateTimeFormats, CultureInfo.CurrentUICulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var notBefore))
                {
                    NotBefore = notBefore;
                }
            }
            else
            {
                NotBefore = DateTime.UtcNow;
            }
            if (!NotBefore.HasValue)
            {
                // Invalid input
                return;
            }

            ExpiresOnOption = expiresOnOption;
            ValidFor = validFor;
            if (ExpiresOnOption is not null && ValidFor is not null)
            {
                // Invalid input
                return;
            }
            else if (expiresOnOption is null && validFor is null)
            {
                // Default
                ExpiresOn = NotBefore.Value.AddMonths(6);
                return;
            }

            if (expiresOnOption is { })
            {
                if (DateTime.TryParseExact(expiresOnOption, _dateTimeFormats, CultureInfo.CurrentUICulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var expiresOn))
                {
                    ExpiresOn = expiresOn;
                }
            }

            if (validFor is { })
            {
                if (TimeSpan.TryParseExact(validFor, _timeSpanFormats, CultureInfo.CurrentUICulture, out var validForValue))
                {
                    ExpiresOn = NotBefore.Value.Add(validForValue);
                }
            }

            if (!ExpiresOn.HasValue)
            {
                // Invalid input
                return;
            }
        }

        [CommandOption("-n|--name <NAME>")]
        [Description("The name of the user to create the JWT for. Defaults to the current environment user.")]
        public string Name { get; }

        [CommandOption("--audience <AUDIENCE>")]
        [Description("The audience to create the JWT for. Defaults to the first HTTPS URL configured in the project's launchSettings.json.")]
        public string Audience { get; }

        [CommandOption("--issuer <ISSUER>", IsHidden = true)]
        [Description("The issuer of the JWT. Defaults to the AspNetCoreDevJwts.")]
        public string Issuer { get; }

        [CommandOption("-c|--claim <CLAIM>")]
        [Description("Claims to add to the JWT. Specify once for each claim in the format \"name=value\".")]
        public string[]? Claims { get; init; }

        [CommandOption("--not-before <DATETIME>")]
        [Description(@"The UTC date & time the JWT should not be valid before in the format 'yyyy-MM-dd [[HH:mm[[:ss]]]]'. Defaults to the date & time the JWT is created.")]
        public string? NotBeforeOption { get; }

        public DateTime? NotBefore { get; }

        [CommandOption("--expires-on <DATETIME>")]
        [Description(@"The UTC date & time the JWT should expire in the format 'yyyy-MM-dd [[[[HH:mm]]:ss]]'. Defaults to 6 months after the --not-before date. " +
                     "Do not use this option in conjunction with the --valid-for option.")]
        public string? ExpiresOnOption { get; }

        [CommandOption("-v|--valid-for <TIMESPAN>")]
        [Description("The period the JWT should expire after. Specify using a number followed by a period type like 'd' for days, 'h' for hours, " +
                     "'m' for minutes, and 's' for seconds, e.g. '365d'. Do not use this option in conjunction with the --expires-on option.")]
        public string? ValidFor { get; }

        public DateTime? ExpiresOn { get; init; }

        public override ValidationResult Validate()
        {
            if (Name is null or { Length: < 1 })
            {
                return ValidationResult.Error("Current user name could not be determined, please specify a name for the JWT using the name option.");
            }

            if (!NotBefore.HasValue)
            {
                return ValidationResult.Error(@"The date provided for --not-before could not be parsed. Ensure you use the format 'yyyy-MM-dd [[[[HH:mm]]:ss]]'.");
            }

            if (ValidFor is not null && ExpiresOnOption is not null)
            {
                return ValidationResult.Error("Do not specify both --expires-on and --valid-for options. Specify either option, or none to get the default expiration.");
            }

            if (!ExpiresOn.HasValue)
            {
                return ExpiresOnOption is not null
                    ? ValidationResult.Error(@"The date provided for --expires-on could not be parsed. Ensure you use the format 'yyyy-MM-dd [[[[HH:mm]]:ss]]'.")
                    : ValidationResult.Error(@"The period provided for --valid-for could not be parsed. Ensure you use a format like '10d', '24h', etc.");
            }

            return base.Validate();
        }
    }

    public IDictionary<string, string>? Claims { get; set; }

    public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        var userSecretsId = DevJwtCliHelpers.GetUserSecretsId(settings.Project!);

        if (userSecretsId is null)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] The specified project does not have a user secrets ID configured. Set a user secrets ID by running the command [yello]'dotnet user-secrets init'[/].");
            return 1;
        }

        var keyMaterial = DevJwtCliHelpers.GetOrCreateSigningKeyMaterial(userSecretsId);

        var jwtIssuer = new JwtIssuer(DevJwtsDefaults.Issuer, keyMaterial);
        var jwt = jwtIssuer.Create(settings.Name, settings.Audience, notBefore: settings.NotBefore!.Value, expires: settings.ExpiresOn!.Value, issued: DateTime.UtcNow, Claims);
        var jwtStore = new JwtStore(userSecretsId);
        jwtStore.Jwts.Add(jwt.Id, jwt);
        jwtStore.Save();

        AnsiConsole.MarkupLineInterpolated($"[green]JWT successfully created:[/]");
        DevJwtCliHelpers.PrintJwt(jwt);

        return 0;
    }

    public override ValidationResult Validate([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        var settingsResult = settings.Validate();
        if (!settingsResult.Successful)
        {
            return settingsResult;
        }

        if (settings.Claims is { Length: >0 } claimsInput)
        {
            var parsedClaims = DevJwtCliHelpers.TryParseClaims(claimsInput, out IDictionary<string, string> claims);
            if (!parsedClaims)
            {
                return ValidationResult.Error("Malformed claims supplied. Ensure each claim is in the format \"name=value\".");
            }
            Claims = claims;
        }

        return base.Validate(context, settings);
    }
}

public class PrintJwtCommand : Command<PrintJwtCommand.Settings>
{
    public class Settings : JwtSettings
    {
        public Settings(string? project, string id)
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

    public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
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

    public override ValidationResult Validate([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        return settings.Validate();
    }
}

public class DeleteJwtCommand : Command<DeleteJwtCommand.Settings>
{
    public class Settings : JwtSettings
    {
        public Settings(string? project, string id)
            : base(project)
        {
            Id = id;
        }

        [CommandArgument(0, "[id]")]
        [Description("The ID of the JWT to delete")]
        public string Id { get; }

        public override ValidationResult Validate()
        {
            return Id is null or { Length: < 1 }
                ? ValidationResult.Error("ID was not specified, please specify the ID of a JWT to delete")
                : base.Validate();
        }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
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

        jwtStore.Jwts.Remove(settings.Id);
        jwtStore.Save();

        AnsiConsole.MarkupLineInterpolated($"[green]Deleted JWT with ID[/] [yellow]{settings.Id}[/]");

        return 0;
    }

    public override ValidationResult Validate([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        return settings.Validate();
    }
}

public class ClearJwtCommand : Command<ClearJwtCommand.Settings>
{
    public class Settings : JwtSettings
    {
        public Settings(string? project)
            : base(project)
        {

        }

        [CommandOption("-f|--force")]
        [Description("Don't prompt for confirmation before deleting JWTs")]
        public bool? Force { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        var userSecretsId = DevJwtCliHelpers.GetUserSecretsId(settings.Project!);

        if (userSecretsId is null)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] The specified project does not have a user secrets ID configured. Set a user secrets ID by running the command [yello]'dotnet user-secrets init'[/].");
            return 1;
        }

        var jwtStore = new JwtStore(userSecretsId);
        var count = jwtStore.Jwts.Count;

        AnsiConsole.Markup($"[grey]Project:[/] ");
        AnsiConsole.Write(new TextPath(settings.Project!));
        AnsiConsole.WriteLine();

        if (count == 0)
        {
            AnsiConsole.MarkupLine("There are no JWTs to delete");
            return 0;
        }

        if (settings.Force != true && !AnsiConsole.Confirm($"Are you sure you want to delete {count} JWT(s) for this project?", defaultValue: false))
        {
            AnsiConsole.MarkupLine("Cancelled, no JWTs were deleted");
            return 0;
        }

        jwtStore.Jwts.Clear();
        jwtStore.Save();

        AnsiConsole.MarkupLineInterpolated($"[green]Deleted[/] [yellow]{count}[/] JWT(s) successfully");

        return 0;
    }

    public override ValidationResult Validate([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        return settings.Validate();
    }
}

public class KeyCommand : Command<KeyCommand.Settings>
{
    public class Settings : JwtSettings
    {
        public Settings(string? project)
            : base(project)
        {

        }

        [CommandOption("--reset")]
        [Description("Reset the signing key. This will invalidate all previously issued JWTs for this project.")]
        public bool? Reset { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        var userSecretsId = DevJwtCliHelpers.GetUserSecretsId(settings.Project!);

        if (userSecretsId is null)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] The specified project does not have a user secrets ID configured. Set a user secrets ID by running the command [yello]'dotnet user-secrets init'[/].");
            return 1;
        }

        DevJwtCliHelpers.PrintProjectDetails(settings.Project!, userSecretsId);

        if (settings.Reset == true)
        {
            if (AnsiConsole.Confirm("Are you sure you want to reset the JWT signing key? This will invalidate all JWTs previously issued for this project.", defaultValue: false))
            {
                var key = DevJwtCliHelpers.CreateSigningKeyMaterial(userSecretsId, reset: true);
                AnsiConsole.MarkupLineInterpolated($"[grey]New signing key created:[/] {Convert.ToBase64String(key)}");
                return 0;
            }

            AnsiConsole.MarkupLine("Key reset cancelled.");
            return 0;
        }

        var projectConfiguration = new ConfigurationBuilder()
            .AddUserSecrets(userSecretsId)
            .Build();
        var signingKeyMaterial = projectConfiguration[DevJwtsDefaults.SigningKeyConfigurationKey];

        if (signingKeyMaterial is null)
        {
            AnsiConsole.MarkupLine("Signing key for dev JWTs was not found. One will be created automatically when the first JWT is created.");
            return 0;
        }

        AnsiConsole.MarkupLineInterpolated($"[grey]Signing Key:[/] {signingKeyMaterial}");
        return 0;
    }
}

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
            // TODO: Support vbproj & fsproj too
            var csprojFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj");
            if (csprojFiles is [var path])
            {
                Project = path;
            }
        }
    }

    [CommandOption("-p|--project <PROJECT>")]
    [Description("The path of the project to operate on. Defaults to the project in the current directory.")]
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

    public static byte[] GetOrCreateSigningKeyMaterial(string userSecretsId, bool reset = false)
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

        return CreateSigningKeyMaterial(userSecretsId);
    }

    public static byte[] CreateSigningKeyMaterial(string userSecretsId, bool reset = false)
    {
        // Create signing material and save to user secrets
        var newKeyMaterial = System.Security.Cryptography.RandomNumberGenerator.GetBytes(DevJwtsDefaults.SigningKeyLength);
        var secretsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "UserSecrets", userSecretsId, "secrets.json");
        
        IDictionary<string, string>? secrets = null;
        if (File.Exists(secretsFilePath))
        {
            using var secretsFileStream = new FileStream(secretsFilePath, FileMode.Open, FileAccess.Read);
            if (secretsFileStream.Length > 0)
            {
                secrets = JsonSerializer.Deserialize<IDictionary<string, string>>(secretsFileStream) ?? new Dictionary<string, string>();
            }
        }

        secrets ??= new Dictionary<string, string>();

        if (reset && secrets.ContainsKey(DevJwtsDefaults.SigningKeyConfigurationKey))
        {
            secrets.Remove(DevJwtsDefaults.SigningKeyConfigurationKey);
        }
        secrets.Add(DevJwtsDefaults.SigningKeyConfigurationKey, Convert.ToBase64String(newKeyMaterial));
        
        using var secretsWriteStream = new FileStream(secretsFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        JsonSerializer.Serialize(secretsWriteStream, secrets);

        return newKeyMaterial;
    }

    public static void PrintProjectDetails(string projectPath, string userSecretsId)
    {
        var settingsTable = new Table();
        settingsTable.ShowHeaders = false;
        settingsTable.Border(TableBorder.None);
        settingsTable.AddColumn("Name");
        settingsTable.AddColumn("Value");
        settingsTable.AddRow(new Markup("[grey]Project:[/]"), new TextPath(projectPath));
        settingsTable.AddRow(new Markup("[grey]User Secrets ID:[/]"), new Text(userSecretsId));
        AnsiConsole.Write(settingsTable);
        AnsiConsole.WriteLine();
    }

    public static void PrintJwt(Jwt jwt)
    {
        var table = new Table();
        table.Border(TableBorder.None);
        table.HideHeaders();
        table.AddColumns("Name", "Value");
        table.AddRow(new Markup("[bold grey]Id:[/]"), new Text(jwt.Id));
        table.AddRow(new Markup("[bold grey]Name:[/]"), new Text(jwt.Name));
        table.AddRow(new Markup("[bold grey]Audience:[/]"), new Text(jwt.Audience));
        table.AddRow(new Markup("[bold grey]Claims:[/]"), jwt.Claims?.Count > 0 ? new Rows(jwt.Claims.Select(kvp => new Text($"{kvp.Key}={kvp.Value}"))) : new Text("[none]"));
        table.AddRow(new Markup("[bold grey]Expires:[/]"), new Text(jwt.Expires.ToString("O")));
        table.AddRow(new Markup("[bold grey]Issued:[/]"), new Text(jwt.Issued.ToString("O")));
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[bold grey]Token:[/]");
        Console.WriteLine(jwt.Token);
    }

    public static string GetApplicationUrl(string project)
    {
        // TODO: Figure out what to do if no HTTPS addresses exist
        // TODO: Handle error cases, missing properties/content, etc.
        var launchSettingsFilePath = Path.Combine(Path.GetDirectoryName(project)!, "Properties", "launchSettings.json");
        if (File.Exists(launchSettingsFilePath))
        {
            using var launchSettingsFileStream = new FileStream(launchSettingsFilePath, FileMode.Open, FileAccess.Read);
            if (launchSettingsFileStream.Length > 0)
            {
                var launchSettingsJson = JsonDocument.Parse(launchSettingsFileStream);
                if (launchSettingsJson.RootElement.TryGetProperty("profiles", out var profiles))
                {
                    var profilesEnumerator = profiles.EnumerateObject();
                    foreach (var profile in profilesEnumerator)
                    {
                        if (profile.Value.TryGetProperty("commandName", out var commandName))
                        {
                            if (commandName.ValueEquals("Project"))
                            {
                                if (profile.Value.TryGetProperty("applicationUrl", out var applicationUrl))
                                {
                                    var value = applicationUrl.GetString();
                                    if (value is { } applicationUrls)
                                    {
                                        var urls = applicationUrls.Split(";");
                                        var firstHttpsUrl = urls.FirstOrDefault(u => u.StartsWith("https:"));
                                        if (firstHttpsUrl is { } result)
                                        {
                                            return result;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // Kestrel default
        return "https://localhost:5001";
    }

    public static bool TryParseClaims(string[] input, out IDictionary<string, string> claims)
    {
        claims = new Dictionary<string, string>();
        foreach (var claim in input)
        {
            var parts = claim.Split('=');
            if (parts.Length != 2)
            {
                return false;
            }
            var key = parts[0];
            var value = parts[1];
            // Collapse multiple scopes into single space-delimited field
            if (string.Equals("scope", key, StringComparison.Ordinal) && claims.ContainsKey("scope"))
            {
                var existingScope = claims["scope"];
                claims["scope"] = $"{existingScope} {value}";
            }
            else
            {
                // TODO: Handle other duplicate claims
                claims.Add(key, value);
            }
        }
        return true;
    }
}

public record Jwt(string Id, string Name, string Audience, DateTimeOffset NotBefore, DateTimeOffset Expires, DateTimeOffset Issued, string Token)
{
    public IDictionary<string, string>? Claims { get; set; } = new Dictionary<string, string>();

    public override string ToString() => Token;
}

public class JwtIssuer
{
    private readonly SymmetricSecurityKey _signingKey;

    public JwtIssuer(string issuer, byte[] signingKeyMaterial)
    {
        Issuer = issuer;
        _signingKey = new SymmetricSecurityKey(signingKeyMaterial);
    }

    public string Issuer { get; }

    public Jwt Create(string name, string audience, DateTime notBefore, DateTime expires, DateTime issued, IDictionary<string, string>? claims = null)
    {
        var identity = new GenericIdentity(name);
        if (claims is { Count: > 0 } claimsToAdd)
        {
            identity.AddClaims(claimsToAdd.Select(kvp => new Claim(kvp.Key, kvp.Value)));
        }
        
        var handler = new JwtSecurityTokenHandler();
        var jwtSigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256Signature);
        var token = handler.CreateEncodedJwt(Issuer, audience, identity, notBefore, expires, issued, jwtSigningCredentials);

        var id = Guid.NewGuid().ToString().GetHashCode().ToString("x");
        var jwt = new Jwt(id, name, audience, notBefore, expires, issued, token);
        if (claims is not null)
        {
            foreach (var claim in claims)
            {
                jwt.Claims?.Add(claim);
            }
        }
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
            if (fileStream.Length > 0)
            {
                Jwts = JsonSerializer.Deserialize<IDictionary<string, Jwt>>(fileStream, _jsonSerializerOptions) ?? new Dictionary<string, Jwt>();
            }
        }
    }

    public void Save()
    {
        if (Jwts is not null)
        {
            using var fileStream = new FileStream(_filePath, FileMode.Create, FileAccess.Write);
            JsonSerializer.Serialize<IDictionary<string, Jwt>>(fileStream, Jwts, _jsonSerializerOptions);
        }
    }
}
