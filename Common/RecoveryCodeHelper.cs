using System.Security.Cryptography;

namespace Authentication.Fido2.Common;

/// <summary>
/// Centralized recovery code generation and normalization logic.
/// Used by both MfaService and Fido2MfaService.
/// </summary>
public static class RecoveryCodeHelper
{
    private const int Length = 12;
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate()
    {
        var chars = new char[Length];
        for (var i = 0; i < Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        var value = new string(chars);
        return $"{value[..4]}-{value.Substring(4, 4)}-{value.Substring(8, 4)}";
    }

    public static string Normalize(string code) =>
        code.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}
