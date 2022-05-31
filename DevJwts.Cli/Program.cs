using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Principal;
using System.Text.Json;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using DevJwts;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Globalization;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("dev-jwts");
    //config.PropagateExceptions();

    config.AddCommand<ListJwtCommand>("list")
        .WithDescription("Lists all JWTs for the specified project")
        .WithExample(new[] { "list" });
    
    config.AddCommand<CreateJwtCommand>("create")
        .WithDescription("Creates a JWT for the specified project")
        .WithExample(new[] { "create" })
        .WithExample(new[] { "create", "--name testuser" })
        .WithExample(new[] { "create", "--name testuser", "--claim scope=myapi:read" });
    
    config.AddCommand<PrintJwtCommand>("print")
        .WithDescription("Prints the details of the specified JWT")
        .WithExample(new[] { "print", "caa676ee" });
    
    config.AddCommand<DeleteJwtCommand>("delete")
        .WithDescription("Deletes the JWT with the specified ID in the specified project")
        .WithExample(new[] { "delete", "caa676ee" });
    
    config.AddCommand<ClearJwtCommand>("clear")
        .WithDescription("Deletes all JWTs for the specified project")
        .WithExample(new[] { "clear" })
        .WithExample(new[] { "clear", "--force" }); ;
    
    config.AddCommand<KeyCommand>("key")
        .WithDescription("Prints the key used for signing JWTs for the specified project");

    config.AddExample(new[] { "create" });
    config.AddExample(new[] { "create", "-n testuser", "--claim scope=myapi:read" });
    config.AddExample(new[] { "list" });
    config.AddExample(new[] { "delete", "caa676ee" });
    config.AddExample(new[] { "clear" });
});

return app.Run(args);

public class ListJwtCommand : JwtCommand<ListJwtCommand.Settings>
{
    public class Settings : JwtSettings
    {
        public Settings(string? project)
            : base(project)
        {

        }

        [CommandOption("--show-tokens")]
        [Description("Indicates whether JWT base64 strings should be shown")]
        public bool ShowTokens { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings.Project);
        ArgumentNullException.ThrowIfNull(UserSecretsId);

        var jwtStore = new JwtStore(UserSecretsId);

        DevJwtCliHelpers.PrintProjectDetails(settings.Project, UserSecretsId);

        if (jwtStore.Jwts is { Count: >0 } jwts)
        {
            var table = new Table();
            table.AddColumn("Id");
            table.AddColumn("Name");
            table.AddColumn("Audience");
            table.AddColumn("Issued");
            table.AddColumn("Expires");

            if (settings.ShowTokens)
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
}

public class CreateJwtCommand : JwtCommand<CreateJwtCommand.Settings>
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
        public Settings(string? project) : base(project) { }

        [CommandOption("-n|--name|-u|--user <NAME>")]
        [Description("The name of the user to create the JWT for. Defaults to the current environment user.")]
        public string? Name { get; init; }

        [CommandOption("--audience <AUDIENCE>")]
        [Description("The audience to create the JWT for. Defaults to the first HTTPS URL configured in the project's launchSettings.json.")]
        public string? Audience { get; init; }

        [CommandOption("--issuer <ISSUER>", IsHidden = true)]
        [Description("The issuer of the JWT. Defaults to the AspNetCoreDevJwts.")]
        public string? Issuer { get; init; }

        [CommandOption("-s|--scope <SCOPE>")]
        [Description("A scope claim to add to the JWT. Specify once for each scope.")]
        public string[]? Scopes { get; init; }

        [CommandOption("-r|--role <ROLE>")]
        [Description("A role claim to add to the JWT. Specify once for each role.")]
        public string[]? Roles { get; init; }

        [CommandOption("-c|--claim <CLAIM>")]
        [Description("Claims to add to the JWT. Specify once for each claim in the format \"name=value\".")]
        public string[]? Claims { get; init; }

        [CommandOption("--not-before <DATETIME>")]
        [Description(@"The UTC date & time the JWT should not be valid before in the format 'yyyy-MM-dd [[HH:mm[[:ss]]]]'. Defaults to the date & time the JWT is created.")]
        public string? NotBefore { get; init; }

        [CommandOption("--expires-on <DATETIME>")]
        [Description(@"The UTC date & time the JWT should expire in the format 'yyyy-MM-dd [[[[HH:mm]]:ss]]'. Defaults to 6 months after the --not-before date. " +
                     "Do not use this option in conjunction with the --valid-for option.")]
        public string? ExpiresOn { get; init; }

        [CommandOption("-v|--valid-for <TIMESPAN>")]
        [Description("The period the JWT should expire after. Specify using a number followed by a period type like 'd' for days, 'h' for hours, " +
                     "'m' for minutes, and 's' for seconds, e.g. '365d'. Do not use this option in conjunction with the --expires-on option.")]
        public string? ValidFor { get; init; }

        public override ValidationResult Validate()
        {
            var baseResult = base.Validate();

            if (!baseResult.Successful)
            {
                return baseResult;
            }

            if (ValidFor is not null && ExpiresOn is not null)
            {
                return ValidationResult.Error("Do not specify both --expires-on and --valid-for options. Specify either option, or none to get the default expiration.");
            }

            return ValidationResult.Success();
        }
    }

    public IDictionary<string, string?>? Claims { get; set; }

    public string? Name { get; private set; }

    public string? Audience { get; private set; }

    public DateTime NotBefore { get; private set; }

    public DateTime ExpiresOn { get; private set; }

    public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        ArgumentNullException.ThrowIfNull(UserSecretsId);
        ArgumentNullException.ThrowIfNull(Name);
        ArgumentNullException.ThrowIfNull(Audience);

        var keyMaterial = DevJwtCliHelpers.GetOrCreateSigningKeyMaterial(UserSecretsId);

        var jwtIssuer = new JwtIssuer(DevJwtsDefaults.Issuer, keyMaterial);
        var jwtToken = jwtIssuer.Create(Name, Audience, notBefore: NotBefore, expires: ExpiresOn, issuedAt: DateTime.UtcNow, settings.Scopes, settings.Roles, Claims);

        var jwtStore = new JwtStore(UserSecretsId);
        var jwt = Jwt.Create(jwtToken, JwtIssuer.WriteToken(jwtToken), settings.Scopes, settings.Roles, Claims);
        if (Claims is { } customClaims)
        {
            jwt.CustomClaims = customClaims;
        }
        jwtStore.Jwts.Add(jwtToken.Id, jwt);
        jwtStore.Save();

        AnsiConsole.MarkupLineInterpolated($"[green]JWT successfully created:[/]");
        DevJwtCliHelpers.PrintJwt(jwt, jwtToken);

        return 0;
    }

    public override ValidationResult Validate([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        var baseResult = base.Validate(context, settings);
        if (!baseResult.Successful)
        {
            return baseResult;
        }

        ArgumentNullException.ThrowIfNull(settings.Project);

        Name = settings.Name ?? Environment.UserName;

        Audience = settings.Audience;
        if (Audience is null)
        {
            if ((Audience = DevJwtCliHelpers.GetApplicationUrl(settings.Project)) is null)
            {
                return ValidationResult.Error("Could not determine the project's HTTPS URL. Please specify an audience for the JWT using the --audience option.");
            }
        }

        NotBefore = DateTime.UtcNow;
        if (settings.NotBefore is { })
        {
            if (!DateTime.TryParseExact(settings.NotBefore, _dateTimeFormats, CultureInfo.CurrentUICulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var notBefore))
            {
                return ValidationResult.Error(@"The date provided for --not-before could not be parsed. Ensure you use the format 'yyyy-MM-dd [[[[HH:mm]]:ss]]'.");
            }
            NotBefore = notBefore;
        }

        ExpiresOn = NotBefore.AddMonths(6);
        if (settings.ExpiresOn is { })
        {
            if (!DateTime.TryParseExact(settings.ExpiresOn, _dateTimeFormats, CultureInfo.CurrentUICulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var expiresOn))
            {
                return ValidationResult.Error(@"The date provided for --expires-on could not be parsed. Ensure you use the format 'yyyy-MM-dd [[[[HH:mm]]:ss]]'.");
            }
            ExpiresOn = expiresOn;
        }

        if (settings.ValidFor is { })
        {
            if (!TimeSpan.TryParseExact(settings.ValidFor, _timeSpanFormats, CultureInfo.CurrentUICulture, out var validForValue))
            {
                return ValidationResult.Error("The period provided for --valid-for could not be parsed. Ensure you use a format like '10d', '24h', etc.");
            }
            ExpiresOn = NotBefore.Add(validForValue);
        }

        if (settings.Claims is { Length: >0 } claimsInput)
        {
            if (!DevJwtCliHelpers.TryParseClaims(claimsInput, out IDictionary<string, string?> claims))
            {
                return ValidationResult.Error("Malformed claims supplied. Ensure each claim is in the format \"name=value\".");
            }
            Claims = claims;
        }

        return ValidationResult.Success();
    }
}

public class PrintJwtCommand : JwtCommand<PrintJwtCommand.Settings>
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

        [CommandOption("-f|--full")]
        [Description("Whether to show the full JWT contents in addition to the compact serialized format")]
        public bool Full { get; init; }

        public override ValidationResult Validate()
        {
            var baseResult = base.Validate();
            if (!baseResult.Successful)
            {
                return baseResult;
            }

            if (string.IsNullOrEmpty(Id))
            {
                return ValidationResult.Error("ID was not specified, please specify the ID of a JWT to print");
            }
            
            return ValidationResult.Success();
        }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        ArgumentNullException.ThrowIfNull(UserSecretsId);

        var jwtStore = new JwtStore(UserSecretsId);

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
        var jwt = jwtStore.Jwts[settings.Id];
        DevJwtCliHelpers.PrintJwt(jwt, settings.Full ? JwtIssuer.Extract(jwt.Token) : null);

        return 0;
    }
}

public class DeleteJwtCommand : JwtCommand<DeleteJwtCommand.Settings>
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
            var baseResult = base.Validate();
            if (!baseResult.Successful)
            {
                return baseResult;
            }

            if (string.IsNullOrEmpty(Id))
            {
                return ValidationResult.Error("ID was not specified, please specify the ID of a JWT to delete");
            }

            return ValidationResult.Success();
        }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        ArgumentNullException.ThrowIfNull(UserSecretsId);

        var jwtStore = new JwtStore(UserSecretsId);

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
}

public class ClearJwtCommand : JwtCommand<ClearJwtCommand.Settings>
{
    public class Settings : JwtSettings
    {
        public Settings(string? project)
            : base(project)
        {

        }

        [CommandOption("-f|--force")]
        [Description("Don't prompt for confirmation before deleting JWTs")]
        public bool Force { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        ArgumentNullException.ThrowIfNull(UserSecretsId);

        var jwtStore = new JwtStore(UserSecretsId);
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
}

public class KeyCommand : JwtCommand<KeyCommand.Settings>
{
    public class Settings : JwtSettings
    {
        public Settings(string? project)
            : base(project)
        {

        }

        [CommandOption("--reset")]
        [Description("Reset the signing key. This will invalidate all previously issued JWTs for this project.")]
        public bool Reset { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings.Project);
        ArgumentNullException.ThrowIfNull(UserSecretsId);

        DevJwtCliHelpers.PrintProjectDetails(settings.Project, UserSecretsId);

        if (settings.Reset == true)
        {
            if (AnsiConsole.Confirm("Are you sure you want to reset the JWT signing key? This will invalidate all JWTs previously issued for this project.", defaultValue: false))
            {
                var key = DevJwtCliHelpers.CreateSigningKeyMaterial(UserSecretsId, reset: true);
                AnsiConsole.MarkupLineInterpolated($"[grey]New signing key created:[/] {Convert.ToBase64String(key)}");
                return 0;
            }

            AnsiConsole.MarkupLine("Key reset cancelled.");
            return 0;
        }

        var projectConfiguration = new ConfigurationBuilder()
            .AddUserSecrets(UserSecretsId)
            .Build();
        var signingKeyMaterial = projectConfiguration[DevJwtsDefaults.SigningKeyConfigurationKey];

        if (signingKeyMaterial is null)
        {
            AnsiConsole.MarkupLine("Signing key for JWTs was not found. One will be created automatically when the first JWT is created, or you can force creation of a key with the --reset option.");
            return 0;
        }

        AnsiConsole.MarkupLineInterpolated($"[grey]Signing Key:[/] {signingKeyMaterial}");
        return 0;
    }
}

public abstract class JwtCommand<TSettings> : Command<TSettings> where TSettings : JwtSettings
{
    public string? UserSecretsId { get; set; }

    public override ValidationResult Validate([NotNull] CommandContext context, [NotNull] TSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings.Project);

        var baseResult = base.Validate(context, settings);
        if (!baseResult.Successful)
        {
            return baseResult;
        }

        var settingsResult = settings.Validate();
        if (!settingsResult.Successful)
        {
            return settingsResult;
        }

        if ((UserSecretsId = DevJwtCliHelpers.GetUserSecretsId(settings.Project)) is null)
        {
            return ValidationResult.Error("The specified project does not have a user secrets ID configured. Set a user secrets ID by running the command [yello]'dotnet user-secrets init'[/].");
        }

        return ValidationResult.Success();
    }
}

public abstract class JwtSettings : CommandSettings
{
    public JwtSettings(string? project)
    {
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
    public string? Project { get; }

    public string User { get; } = Environment.UserName;

    public override ValidationResult Validate()
    {
        var baseResult = base.Validate();
        if (!baseResult.Successful)
        {
            return baseResult;
        }

        if (Project is { } path && !File.Exists(path))
        {
            return ValidationResult.Error($"Project {path} could not be found");
        }
        else if (Project is null)
        {
            return ValidationResult.Error($"A project could not be found or there were multiple projects in the current directory. Specify a project using the project option.");
        }

        return ValidationResult.Success();
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
        var settingsTable = new Table { ShowHeaders = false, Border = TableBorder.None };
        settingsTable.AddColumn("Name");
        settingsTable.AddColumn("Value");
        settingsTable.AddRow(new Markup("[grey]Project:[/]"), new TextPath(projectPath));
        settingsTable.AddRow(new Markup("[grey]User Secrets ID:[/]"), new Text(userSecretsId));
        AnsiConsole.Write(settingsTable);
        AnsiConsole.WriteLine();
    }

    public static void PrintJwt(Jwt jwt, JwtSecurityToken? fullToken = null)
    {
        var table = new Table();
        table.Border(TableBorder.None);
        table.HideHeaders();
        table.AddColumns("Name", "Value");
        table.AddRow(new Markup("[bold grey]Id:[/]"), new Text(jwt.Id));
        table.AddRow(new Markup("[bold grey]Name:[/]"), new Text(jwt.Name));
        table.AddRow(new Markup("[bold grey]Audience:[/]"), new Text(jwt.Audience));
        table.AddRow(new Markup("[bold grey]Expires:[/]"), new Text(jwt.Expires.ToString("O")));
        table.AddRow(new Markup("[bold grey]Issued:[/]"), new Text(jwt.Issued.ToString("O")));
        table.AddRow(new Markup("[bold grey]Scopes:[/]"), new Text(jwt.Scopes is not null ? string.Join(", ", jwt.Scopes) : "[none]"));
        table.AddRow(new Markup("[bold grey]Roles:[/]"), new Text(jwt.Roles is not null ? string.Join(", ", jwt.Roles) : "[none]"));
        table.AddRow(new Markup("[bold grey]Custom Claims:[/]"), jwt.CustomClaims?.Count > 0
            ? new Rows(jwt.CustomClaims.Select(kvp => new Text(!string.IsNullOrEmpty(kvp.Value) ? $"{kvp.Key}={kvp.Value}" : kvp.Key)))
            : new Text("[none]"));
        if (fullToken is not null)
        {
            table.AddRow(new Markup("[bold grey]Token Header:[/]"), new Text(fullToken.Header.SerializeToJson()));
            table.AddRow(new Markup("[bold grey]Token Payload:[/]"), new Text(fullToken.Payload.SerializeToJson()));
        }
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[bold grey]Compact Token:[/]");
        Console.WriteLine(jwt.Token);
    }

    public static string? GetApplicationUrl(string project)
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

        return null;
    }

    public static bool TryParseClaims(string[] input, out IDictionary<string, string?> claims)
    {
        claims = new Dictionary<string, string?>();
        foreach (var claim in input)
        {
            var parts = claim.Split('=');
            if (parts.Length > 2)
            {
                return false;
            }

            var key = parts[0];
            var value = parts.Length == 2 ? parts[1] : null;

            claims.Add(key, value);
        }
        return true;
    }
}

public record Jwt(string Id, string Name, string Audience, DateTimeOffset NotBefore, DateTimeOffset Expires, DateTimeOffset Issued, string Token)
{
    public IEnumerable<string>? Scopes { get; set; } = new List<string>();

    public IEnumerable<string>? Roles { get; set; } = new List<string>();

    public IDictionary<string, string?>? CustomClaims { get; set; } = new Dictionary<string, string?>();

    public override string ToString() => Token;

    public static Jwt Create(JwtSecurityToken token, string encodedToken, IEnumerable<string>? scopes = null, IEnumerable<string>? roles = null, IDictionary<string, string?>? customClaims = null)
    {
        return new Jwt(token.Id, token.Subject, token.Audiences.FirstOrDefault() ?? throw new ArgumentException("Provided token has no audience", nameof(token)), token.ValidFrom, token.ValidTo, token.IssuedAt, encodedToken)
        {
            Scopes = scopes, 
            Roles = roles,
            CustomClaims = customClaims
        };
    }
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

    public JwtSecurityToken Create(string name, string audience, DateTime? notBefore, DateTime? expires, DateTime? issuedAt, IEnumerable<string>? scopes = null, IEnumerable<string>? roles = null, IDictionary<string, string?>? claims = null)
    {
        var identity = new GenericIdentity(name);

        identity.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, name));

        var id = Guid.NewGuid().ToString().GetHashCode().ToString("x");
        identity.AddClaim(new Claim(JwtRegisteredClaimNames.Jti, id));

        if (scopes is { } scopesToAdd)
        {
            identity.AddClaims(scopesToAdd.Select(s => new Claim("scope", s)));
        }

        if (roles is { } rolesToAdd)
        {
            identity.AddClaims(rolesToAdd.Select(r => new Claim(ClaimTypes.Role, r)));
        }

        if (claims is { Count: > 0 } claimsToAdd)
        {
            identity.AddClaims(claimsToAdd.Select(kvp => new Claim(kvp.Key, kvp.Value ?? "")));
        }

        var handler = new JwtSecurityTokenHandler();
        var jwtSigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256Signature);
        var jwtToken = handler.CreateJwtSecurityToken(Issuer, audience, identity, notBefore, expires, issuedAt, jwtSigningCredentials);
        return jwtToken;
    }

    public static string WriteToken(JwtSecurityToken token)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(token);
    }

    public static JwtSecurityToken Extract(string token) => new(token);

    public bool IsValid(string encodedToken)
    {
        var handler = new JwtSecurityTokenHandler();
        var tokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = _signingKey,
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateIssuerSigningKey = true
        };
        if (handler.ValidateToken(encodedToken, tokenValidationParameters, out _).Identity?.IsAuthenticated == true)
        {
            return true;
        }
        return false;
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
