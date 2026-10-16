<?php
declare(strict_types=1);

/**
 * Строит таблицу ячеек: строки - регионы, столбцы - продукты, плюс итоги.
 *
 * @param list<array{string, string, int}> $records [регион, продукт, количество]
 * @return list<list<string>> строки таблицы, включая заголовок
 */
function pivot(array $records): array
{
    // TODO: суммируйте количество по паре (регион, продукт); отсортируйте
    // регионы и продукты побайтно (SORT_STRING!); добавьте строку и столбец total
    return [['region', 'total']];
}

/** Выравнивает столбцы: первый влево, остальные вправо. */
function render(array $table): string
{
    $widths = array_map(
        fn(array $column) => max(array_map('strlen', $column)),
        array_map(null, ...$table)
    );
    $lines = [];
    foreach ($table as $row) {
        $cells = [];
        foreach ($row as $i => $cell) {
            $cells[] = $i === 0 ? str_pad($cell, $widths[$i]) : str_pad($cell, $widths[$i], ' ', STR_PAD_LEFT);
        }
        $lines[] = implode(' | ', $cells);
    }
    return implode("\n", $lines) . "\n";
}

$tokens = preg_split('/\s+/', trim((string) stream_get_contents(STDIN)), -1, PREG_SPLIT_NO_EMPTY);
$n = (int) array_shift($tokens);
$records = array_map(
    fn(array $c) => [$c[0], $c[1], (int) $c[2]],
    array_chunk(array_slice($tokens, 0, $n * 3), 3)
);
echo render(pivot($records));
