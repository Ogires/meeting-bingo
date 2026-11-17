using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace MeetingBingo.Core.Rooms;

public static class PlayerToken
{
    // 128 random bits in unpadded Base64Url: 22 characters
    public static string New() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

    public static bool IsValid([NotNullWhen(true)] string? token) =>
        token is { Length: 22 } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
