using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Threading.Tasks;

namespace CountingWebAPI.Data
{
    public class SqliteDatabaseHelper : IDatabaseHelper
    {
        private readonly string _connectionString;
        private readonly ILogger<SqliteDatabaseHelper> _logger;

        public SqliteDatabaseHelper(IConfiguration configuration, ILogger<SqliteDatabaseHelper> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? configuration.GetSection("Database:ConnectionString").Value
                ?? throw new InvalidOperationException("Database connection string is not configured.");
            _logger = logger;

            var dbPath = _connectionString.Replace("Data Source=", "", StringComparison.OrdinalIgnoreCase);
            var dbDir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
            {
                try
                {
                    Directory.CreateDirectory(dbDir);
                    _logger.LogInformation($"Created database directory: {dbDir}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to create database directory: {dbDir}");
                }
            }
        }

        public DbConnection GetConnection() => new SqliteConnection(_connectionString);

        public async Task<int> ExecuteNonQueryAsync(string sql, params DbParameter[] parameters)
        {
            _logger.LogDebug("Executing NonQuery: {SQL} with {ParamCount} params.", sql, parameters?.Length ?? 0);
            await using var connection = GetConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            if (parameters != null) command.Parameters.AddRange(parameters);
            return await command.ExecuteNonQueryAsync();
        }

        public async Task<T?> ExecuteScalarAsync<T>(string sql, params DbParameter[] parameters)
        {
            _logger.LogDebug("Executing Scalar: {SQL} with {ParamCount} params.", sql, parameters?.Length ?? 0);
            await using var connection = GetConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            if (parameters != null) command.Parameters.AddRange(parameters);
            var result = await command.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value) return default;
            try
            {
                return (T)Convert.ChangeType(result, typeof(T));
            }
            catch (InvalidCastException ex)
            {
                _logger.LogError(ex, "Failed to cast scalar result to type {TypeName}", typeof(T).Name);
                return default;
            }
        }
        
        public async Task<long> ExecuteInsertAndGetLastIdAsync(string sql, params DbParameter[] parameters)
        {
            _logger.LogDebug("Executing Insert: {SQL} with {ParamCount} params.", sql, parameters?.Length ?? 0);
            await using var connection = GetConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql + "; SELECT last_insert_rowid();";
            if (parameters != null) command.Parameters.AddRange(parameters);
            var result = await command.ExecuteScalarAsync();
            return result != null ? Convert.ToInt64(result) : -1;
        }

        public async Task<List<T>> QueryAsync<T>(string sql, Func<DbDataReader, T> map, params DbParameter[] parameters)
        {
            _logger.LogDebug("Executing Query: {SQL} with {ParamCount} params.", sql, parameters?.Length ?? 0);
            var list = new List<T>();
            await using var connection = GetConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            if (parameters != null) command.Parameters.AddRange(parameters);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(map(reader));
            return list;
        }

        public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, Func<DbDataReader, T> map, params DbParameter[] parameters) where T : class
        {
            _logger.LogDebug("Executing QuerySingleOrDefault: {SQL} with {ParamCount} params.", sql, parameters?.Length ?? 0);
            await using var connection = GetConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            if (parameters != null) command.Parameters.AddRange(parameters);
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync()) return map(reader);
            return null;
        }

        public DbParameter CreateParameter(string name, object value) => new SqliteParameter(name, value ?? DBNull.Value);
    }
}