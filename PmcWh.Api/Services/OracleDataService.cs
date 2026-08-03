using System.Data;
using Oracle.ManagedDataAccess.Client;
using PmcWh.Api.Models;

namespace PmcWh.Api.Services;

public class OracleDataService
{
    private readonly string _connectionString;

    public OracleDataService(IConfiguration configuration)
    {
        var options = configuration.GetSection(OracleConnectionOptions.SectionName).Get<OracleConnectionOptions>() ?? new OracleConnectionOptions();
        _connectionString = !string.IsNullOrWhiteSpace(options.ConnectionString)
            ? options.ConnectionString
            : BuildConnectionString(options);
    }

    public async Task<IEnumerable<Dictionary<string, object?>>> QueryAsync(string sql, params OracleParameter[] parameters)
    {
        var result = new List<Dictionary<string, object?>>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.Parameters.AddRange(parameters);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            result.Add(row);
        }

        return result;
    }

    private static string BuildConnectionString(OracleConnectionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DataSource))
        {
            throw new InvalidOperationException("Oracle DataSource is not configured.");
        }

        return $"User Id={options.Username};Password={options.Password};Data Source={options.DataSource}";
    }
}
