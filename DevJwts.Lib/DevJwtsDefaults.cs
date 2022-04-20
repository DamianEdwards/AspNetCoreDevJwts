namespace DevJwts;

internal static class DevJwtsDefaults
{
    public static string Issuer => "AspNetCoreDevJwts";

    public static string SigningKeyConfigurationKey => $"{Issuer}:KeyMaterial";

    public static int SigningKeyLength => 16;
}
