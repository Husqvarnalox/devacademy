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
    $cells = [];
    $regions = [];
    $products = [];
    foreach ($records as [$region, $product, $qty]) {
        // имена вроде "10" становятся int-ключами, поэтому храним исходные строки отдельно
        $cells[$region][$product] = ($cells[$region][$product] ?? 0) + $qty;
        $regions[$region] = (string) $region;
        $products[$product] = (string) $product;
    }
    $regions = array_values($regions);
    $products = array_values($products);
    sort($regions, SORT_STRING);
    sort($products, SORT_STRING);

    $table = [['region', ...$products, 'total']];
    $columnTotals = array_fill_keys($products, 0);
    $grand = 0;
    foreach ($regions as $region) {
        $row = [$region];
        $sum = 0;
        foreach ($products as $product) {
            $value = $cells[$region][$product] ?? 0;
            $row[] = (string) $value;
            $sum += $value;
            $columnTotals[$product] += $value;
        }
        $row[] = (string) $sum;
        $grand += $sum;
        $table[] = $row;
    }
    $table[] = ['total', ...array_map('strval', array_values($columnTotals)), (string) $grand];
    return $table;
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
