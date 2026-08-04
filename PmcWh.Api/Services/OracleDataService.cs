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
        command.BindByName = true; // ODP.NET binds by ordinal position by default — always bind by :name instead.
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

    /// <summary>
    /// Dùng cho INSERT/UPDATE/DELETE (khác QueryAsync là dùng cho SELECT).
    /// Trả về số dòng bị ảnh hưởng.
    /// </summary>
    public async Task<int> ExecuteAsync(string sql, params OracleParameter[] parameters)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.BindByName = true; // ODP.NET binds by ordinal position by default — always bind by :name instead.
        command.Parameters.AddRange(parameters);

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Phân trang kiểu Oracle 10g — không có OFFSET/FETCH (12c+) nên phải bọc
    /// innerSql (một câu SELECT ... ORDER BY ... bình thường, chưa phân trang)
    /// bằng 2 lớp ROWNUM. Trả về đúng dữ liệu của 1 trang + tổng số dòng.
    /// </summary>
    public async Task<PagedResult<Dictionary<string, object?>>> QueryPagedAsync(
        string innerSql, int page, int pageSize, params OracleParameter[] parameters)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var startRow = (page - 1) * pageSize;
        var endRow = startRow + pageSize;

        var pagedSql = $@"
            SELECT * FROM (
                SELECT inner_query.*, ROWNUM AS rnum, COUNT(*) OVER () AS total_count
                FROM ({innerSql}) inner_query
                WHERE ROWNUM <= :pmcEndRow
            )
            WHERE rnum > :pmcStartRow";

        var allParams = parameters
            .Concat(new[]
            {
                new OracleParameter("pmcEndRow", endRow),
                new OracleParameter("pmcStartRow", startRow),
            })
            .ToArray();

        var rows = (await QueryAsync(pagedSql, allParams)).ToList();

        var totalCount = 0;
        if (rows.Count > 0 && rows[0].TryGetValue("TOTAL_COUNT", out var tc) && tc != null)
        {
            totalCount = Convert.ToInt32(tc);
        }

        foreach (var row in rows)
        {
            row.Remove("RNUM");
            row.Remove("TOTAL_COUNT");
        }

        return new PagedResult<Dictionary<string, object?>>
        {
            Items = rows,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    /// <summary>
    /// Chạy nhiều câu INSERT/UPDATE/DELETE trong CÙNG 1 transaction (commit/rollback chung) —
    /// dùng cho import theo batch hoặc bất kỳ thao tác nào cần nhiều câu SQL ăn khớp với nhau
    /// (vd. update Materials + insert StockMovements). Trả về tổng số dòng bị ảnh hưởng.
    /// </summary>
    public async Task<int> ExecuteBatchAsync(IEnumerable<(string Sql, OracleParameter[] Parameters)> statements)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        var totalAffected = 0;
        try
        {
            foreach (var (sql, parameters) in statements)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                command.CommandType = CommandType.Text;
                command.BindByName = true;
                command.Parameters.AddRange(parameters);
                totalAffected += await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return totalAffected;
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
