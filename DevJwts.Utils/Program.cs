var jwtKeyMaterial = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
Console.WriteLine(Convert.ToHexString(jwtKeyMaterial));
