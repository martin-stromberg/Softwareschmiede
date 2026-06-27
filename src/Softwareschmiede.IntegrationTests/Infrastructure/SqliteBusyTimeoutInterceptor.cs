using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace Softwareschmiede.IntegrationTests.Infrastructure;

internal sealed class SqliteBusyTimeoutInterceptor(int busyTimeoutMs = 2000) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => SetBusyTimeout(connection);

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        SetBusyTimeout(connection);
        return Task.CompletedTask;
    }

    private void SetBusyTimeout(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA busy_timeout = {busyTimeoutMs};";
        cmd.ExecuteNonQuery();
    }
}
