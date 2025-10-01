using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;

namespace CountingWebAPI.Data
{
    public interface IDatabaseHelper
    {
        DbConnection GetConnection();
        Task<int> ExecuteNonQueryAsync(string sql, params DbParameter[] parameters);
        Task<T?> ExecuteScalarAsync<T>(string sql, params DbParameter[] parameters);
        Task<long> ExecuteInsertAndGetLastIdAsync(string sql, params DbParameter[] parameters);
        Task<List<T>> QueryAsync<T>(string sql, Func<DbDataReader, T> map, params DbParameter[] parameters);
        Task<T?> QuerySingleOrDefaultAsync<T>(string sql, Func<DbDataReader, T> map, params DbParameter[] parameters) where T : class;
        DbParameter CreateParameter(string name, object value);
    }
}