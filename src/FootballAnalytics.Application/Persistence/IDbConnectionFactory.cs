using System.Data.Common;

namespace FootballAnalytics.Application.Persistence;

public interface IDbConnectionFactory
{
    DbConnection CreateConnection();
}
