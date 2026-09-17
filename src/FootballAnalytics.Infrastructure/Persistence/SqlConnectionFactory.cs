using System.Data.Common;
using FootballAnalytics.Application.Persistence;
using Microsoft.Data.SqlClient;

namespace FootballAnalytics.Infrastructure.Persistence;

public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        this.connectionString = connectionString;
    }

    public DbConnection CreateConnection() => new SqlConnection(connectionString);
}
