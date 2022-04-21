namespace DevJwts;

internal static class DevJwtsDefaults
{
    public static string Issuer => "AspNetCoreDevJwts";

    public static string SigningKeyConfigurationKey => $"{Issuer}:KeyMaterial";

    // TODO: Probably should make this longer
    public static int SigningKeyLength => 16;
}
