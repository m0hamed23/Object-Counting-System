using System;
using System.Data.Common;
using System.Globalization;

namespace CountingWebAPI.Data
{
    public static class DbDataReaderExtensions
    {
        public static string GetStringSafe(this DbDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }

        public static string? GetNullableString(this DbDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        public static int GetInt32Safe(this DbDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            // In SQLite, GetInt32 can fail if the value is a long, so we read as long and cast.
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetInt64(ordinal));
        }

        public static int? GetNullableInt32(this DbDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? (int?)null : Convert.ToInt32(reader.GetInt64(ordinal));
        }
        
        public static bool GetBooleanSafe(this DbDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal)) return false;
            // SQLite stores booleans as integers (0 or 1)
            return Convert.ToInt32(reader.GetValue(ordinal)) != 0;
        }

        public static DateTime GetDateTimeSafe(this DbDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal)) return DateTime.MinValue;
            // SQLite stores datetimes as TEXT, needs parsing
            var dateString = reader.GetString(ordinal);
            if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var result))
            {
                return result;
            }
            // Fallback for different formats if necessary
            if (DateTime.TryParseExact(dateString, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result))
            {
                 return result.ToUniversalTime();
            }
            if (DateTime.TryParseExact(dateString, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result))
            {
                 return result.ToUniversalTime();
            }

            return DateTime.MinValue;
        }
    }
}