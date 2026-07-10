using Microsoft.Data.SqlClient;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;

namespace Authentication.Fido2.Extensions;

public static class LoggingExtensions
{
    public static void ConfigureSqlServerLogging(this WebApplicationBuilder builder)
    {
        var loggingConnectionString = builder.Configuration.GetConnectionString("LoggingConnection");

        if (string.IsNullOrWhiteSpace(loggingConnectionString))
        {
            throw new InvalidOperationException("Connection string 'LoggingConnection' was not found.");
        }

        EnsureLoggingDatabaseExists(loggingConnectionString);

        var minimumLevel = builder.Environment.IsDevelopment()
            ? LogEventLevel.Verbose
            : LogEventLevel.Error;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Microsoft", minimumLevel)
            .MinimumLevel.Override("Microsoft.AspNetCore", minimumLevel)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "Authentication.Fido2")
            .WriteTo.MSSqlServer(
                connectionString: loggingConnectionString,
                sinkOptions: new MSSqlServerSinkOptions
                {
                    TableName = "ApplicationLogs",
                    SchemaName = "dbo",
                    AutoCreateSqlTable = true,
                },
                restrictedToMinimumLevel: minimumLevel
            )
            .CreateLogger();

        builder.Host.UseSerilog();
    }

    private static void EnsureLoggingDatabaseExists(string loggingConnectionString)
    {
        var builder = new SqlConnectionStringBuilder(loggingConnectionString);
        var databaseName = builder.InitialCatalog;

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException(
                "Connection string 'LoggingConnection' must include a database name."
            );
        }

        var masterConnection = new SqlConnectionStringBuilder(loggingConnectionString)
        {
            InitialCatalog = "master",
        };

        using var connection = new SqlConnection(masterConnection.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        var escapedNameLiteral = databaseName.Replace("'", "''");
        var escapedNameIdentifier = databaseName.Replace("]", "]]" );

        command.CommandText =
            $"IF DB_ID(N'{escapedNameLiteral}') IS NULL CREATE DATABASE [{escapedNameIdentifier}];";

        command.ExecuteNonQuery();
    }
}