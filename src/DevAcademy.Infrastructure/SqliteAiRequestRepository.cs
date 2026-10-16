using DevAcademy.Application.Ports;
using static DevAcademy.Infrastructure.SqlHelpers;

namespace DevAcademy.Infrastructure;

public sealed class SqliteAiRequestRepository(SqliteDatabase db) : IAiRequestRepository
{
    public async Task<long> AddAsync(AiRequestRecord r, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd("""
            INSERT INTO ai_requests (provider, model, scenario, started_at, finished_at, input_tokens, output_tokens, approx_cost_usd, status, error_kind)
            VALUES ($p, $m, $s, $st, $f, $i, $o, $c, $status, $e);
            SELECT last_insert_rowid();
            """,
            ("$p", r.Provider), ("$m", r.Model), ("$s", r.Scenario), ("$st", Iso(r.StartedAt)), ("$f", Iso(r.FinishedAt)),
            ("$i", r.InputTokens), ("$o", r.OutputTokens), ("$c", r.ApproxCostUsd is { } d ? (double)d : null),
            ("$status", r.Status), ("$e", r.ErrorKind));
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<IReadOnlyList<AiRequestRecord>> GetRecentAsync(int limit, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd("""
            SELECT id, provider, model, scenario, started_at, finished_at, input_tokens, output_tokens, approx_cost_usd, status, error_kind
            FROM ai_requests ORDER BY started_at DESC, id DESC LIMIT $n
            """, ("$n", limit));
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        var list = new List<AiRequestRecord>();
        while (await rd.ReadAsync(ct))
            list.Add(new AiRequestRecord(rd.GetInt64(0), rd.GetString(1), rd.GetString(2), rd.GetString(3), rd.ReadRequiredDate(4), rd.ReadDate(5),
                rd.IsDBNull(6) ? null : rd.GetInt32(6), rd.IsDBNull(7) ? null : rd.GetInt32(7),
                rd.IsDBNull(8) ? null : (decimal)rd.GetDouble(8), rd.GetString(9), rd.IsDBNull(10) ? null : rd.GetString(10)));
        return list;
    }

    public async Task<decimal> SumCostSinceAsync(DateTimeOffset since, CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd("SELECT COALESCE(SUM(approx_cost_usd), 0) FROM ai_requests WHERE started_at >= $s", ("$s", Iso(since)));
        return (decimal)Convert.ToDouble(await cmd.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<int> DeleteAllAsync(CancellationToken ct = default)
    {
        await using var c = db.Open();
        await using var cmd = c.Cmd("DELETE FROM ai_requests");
        return await cmd.ExecuteNonQueryAsync(ct);
    }
}
