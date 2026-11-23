using MeetingBingo.Core.Rooms;

namespace MeetingBingo.Core.Tests.Rooms;

public class RoomCodeTests
{
    [Fact]
    public void New_codes_have_five_characters_from_the_alphabet()
    {
        for (var i = 0; i < 1000; i++)
        {
            var code = RoomCode.New();
            Assert.Equal(RoomCode.Length, code.Length);
            Assert.All(code, c => Assert.Contains(c, RoomCode.Alphabet));
        }
    }

    [Fact]
    public void The_alphabet_has_31_characters_and_no_look_alikes()
    {
        Assert.Equal(31, RoomCode.Alphabet.Length);
        foreach (var c in "01ILO") Assert.DoesNotContain(c, RoomCode.Alphabet);
    }

    [Theory]
    [InlineData("k7qmp", "K7QMP")]
    [InlineData(" K7QMP ", "K7QMP")]
    public void Valid_codes_are_normalised(string input, string expected)
    {
        Assert.True(RoomCode.TryNormalize(input, out var code));
        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("K7QM")]
    [InlineData("K7QMPX")]
    [InlineData("K7QM0")]
    [InlineData("K7QMI")]
    [InlineData("K7QM!")]
    public void Invalid_codes_are_rejected(string? input) =>
        Assert.False(RoomCode.TryNormalize(input, out _));
}

public class PlayerTokenTests
{
    [Fact]
    public void New_tokens_are_valid_and_unique()
    {
        var tokens = Enumerable.Range(0, 1000).Select(_ => PlayerToken.New()).ToList();
        Assert.All(tokens, t => Assert.True(PlayerToken.IsValid(t)));
        Assert.Equal(tokens.Count, tokens.Distinct().Count());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaa+")]   // 22 characters, one not allowed
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaa")]  // 23 characters
    public void Malformed_tokens_are_rejected(string? token) =>
        Assert.False(PlayerToken.IsValid(token));
}