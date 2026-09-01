using System.Security.Cryptography;
using FoodTraceability.Modules.Identity.Application.Authentication;

namespace FoodTraceability.Modules.Identity.Infrastructure.Authentication;

internal sealed class CryptographicRefreshTokenProtector : IRefreshTokenProtector
{
    private const int TokenSizeInBytes = 32;

    public GeneratedRefreshToken Generate()
    {
        var plainText = EncodeBase64Url(RandomNumberGenerator.GetBytes(TokenSizeInBytes));
        return new GeneratedRefreshToken(plainText, Hash(plainText));
    }

    public string Hash(string plainTextToken)
    {
        ArgumentNullException.ThrowIfNull(plainTextToken);

        var tokenBytes = System.Text.Encoding.UTF8.GetBytes(plainTextToken);
        return Convert.ToHexString(SHA256.HashData(tokenBytes));
    }

    private static string EncodeBase64Url(ReadOnlySpan<byte> value)
    {
        return Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
