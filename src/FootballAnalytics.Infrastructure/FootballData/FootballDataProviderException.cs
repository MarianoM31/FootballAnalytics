namespace FootballAnalytics.Infrastructure.FootballData;

public sealed class FootballDataProviderException(string message, Exception? innerException = null) : Exception(message, innerException);
