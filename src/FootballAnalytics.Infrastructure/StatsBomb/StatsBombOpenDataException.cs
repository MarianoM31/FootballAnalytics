namespace FootballAnalytics.Infrastructure.StatsBomb;

public sealed class StatsBombOpenDataException(string message, Exception? innerException = null) : Exception(message, innerException);
