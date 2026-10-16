<?php
declare(strict_types=1);

/**
 * @param list<array{string, string, int}> $rows строки [имя, отдел, зарплата]
 * @return list<array{dept: string, n: int, total: int, avg: float, top: string}>
 */
function buildReport(array $rows): array
{
    /** @var array<string, list<array{string, int}>> $groups */
    $groups = [];
    foreach ($rows as [$name, $dept, $salary]) {
        $groups[$dept][] = [$name, $salary];
    }

    $report = array_map(function (array $members, int|string $dept): array {
        $total = array_sum(array_column($members, 1));
        $best = max(array_column($members, 1));
        $tops = array_column(array_filter($members, fn(array $m) => $m[1] === $best), 0);
        sort($tops, SORT_STRING);
        return [
            'dept' => (string) $dept,
            'n' => count($members),
            'total' => $total,
            'avg' => $total / count($members),
            'top' => $tops[0],
        ];
    }, $groups, array_keys($groups));

    usort($report, fn(array $a, array $b) => $b['total'] <=> $a['total'] ?: strcmp($a['dept'], $b['dept']));
    return $report;
}

$tokens = preg_split('/\s+/', trim((string) stream_get_contents(STDIN)), -1, PREG_SPLIT_NO_EMPTY);
$n = (int) array_shift($tokens);
$rows = array_map(
    fn(array $chunk) => [$chunk[0], $chunk[1], (int) $chunk[2]],
    array_chunk(array_slice($tokens, 0, $n * 3), 3)
);

foreach (buildReport($rows) as ['dept' => $dept, 'n' => $count, 'total' => $total, 'avg' => $avg, 'top' => $top]) {
    printf("%s: n=%d total=%d avg=%.1F top=%s\n", $dept, $count, $total, $avg, $top);
}
printf("all: n=%d total=%d\n", count($rows), array_sum(array_column($rows, 2)));
