using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace MeetingBingo.Core.Rooms;

public static class RoomCode
{
    // No 0/O or 1/I/L: 31 characters, so 31^5 ≈ 28.6 million codes
    public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    public const int Length = 5;

    public static string New() => RandomNumberGenerator.GetString(Alphabet, Length);

    public static bool TryNormalize(string? input, [NotNullWhen(true)] out string? code)
    {
        code = null;
        if (input is null) return false;
        var candidate = input.Trim().ToUpperInvariant();
        if (candidate.Length != Length || !candidate.All(c => Alphabet.Contains(c))) return false;
        code = candidate;
        return true;
    }
}
