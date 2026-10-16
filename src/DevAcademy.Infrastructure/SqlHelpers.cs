using System.Globalization;
using Microsoft.Data.Sqlite;

namespace DevAcademy.Infrastructure;

internal static class SqlHelpers
{
    public static SqliteCommand Cmd(this SqliteConnection c, string sql, params (string Name, object? Value)[] parameters)
    {
        var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    public static string Iso(DateTimeOffset t) => t.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    public static string? Iso(DateTimeOffset? t) => t is null ? null : Iso(t.Value);

    public static DateTimeOffset? ReadDate(this SqliteDataReader r, int i) =>
        r.IsDBNull(i) ? null : DateTimeOffset.Parse(r.GetString(i), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static DateTimeOffset ReadRequiredDate(this SqliteDataReader r, int i) =>
        ReadDate(r, i) ?? throw new InvalidDataException("Unexpected NULL date");
}
