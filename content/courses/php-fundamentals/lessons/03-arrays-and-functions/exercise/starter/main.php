<?php
declare(strict_types=1);

/**
 * @param list<array{string, string, int}> $rows строки [имя, отдел, зарплата]
 * @return list<array{dept: string, n: int, total: int, avg: float, top: string}>
 *         отделы по убыванию суммы зарплат, при равенстве - по имени отдела как строки (strcmp)
 */
function buildReport(array $rows): array
{
    // TODO: сгруппируйте $rows по отделу (foreach с деструктуризацией),
    // посчитайте n, total, avg и top, отсортируйте через usort, <=> и strcmp
    return [];
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
