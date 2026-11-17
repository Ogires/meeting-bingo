namespace MeetingBingo.Core.Phrases;

public enum PhraseCategory { Technical, Jargon, Situation }

public sealed record Phrase(int Id, string Text, PhraseCategory Category);
