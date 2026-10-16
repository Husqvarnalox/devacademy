<?php
declare(strict_types=1);

/** Дополняет пробелами справа до ширины $width ячеек терминала. */
function padRight(string $s, int $width): string
{
    return $s . str_repeat(' ', max(0, $width - mb_strwidth($s)));
}

/** Дополняет пробелами слева до ширины $width ячеек терминала. */
function padLeft(string $s, int $width): string
{
    return str_repeat(' ', max(0, $width - mb_strwidth($s))) . $s;
}

/** Копейки -> «1 234.50». */
function money(int $cents): string
{
    return number_format(intdiv($cents, 100), 0, '', ' ') . '.' . sprintf('%02d', $cents % 100);
}

/** «12», «12.5», «12.50» -> копейки; иначе null. */
function parsePrice(string $raw): ?int
{
    if (!preg_match('/^([0-9]+)(?:\.([0-9]{1,2}))?$/', $raw, $m)) {
        return null;
    }
    return (int) $m[1] * 100 + (int) str_pad($m[2] ?? '', 2, '0');
}

$rows = [];
$invalid = [];

while (($line = fgets(STDIN)) !== false) {
    $line = trim($line);
    if ($line === '') {
        continue;
    }
    $f = array_map('trim', explode(';', $line));
    $price = count($f) === 3 ? parsePrice($f[2]) : null;
    if ($price === null || $f[0] === '' || !preg_match('/^[1-9][0-9]*$/', $f[1])) {
        $invalid[] = $line;
        continue;
    }
    $qty = (int) $f[1];
    $rows[] = [$f[0], $qty, $qty * $price];
}

if ($rows === []) {
    echo "no rows\n";
} else {
    $sumQty = array_sum(array_column($rows, 1));
    $sumTotal = array_sum(array_column($rows, 2));
    $nameW = max(mb_strwidth('Товар'), mb_strwidth('Итого'), ...array_map(fn ($r) => mb_strwidth($r[0]), $rows));
    $qtyW = max(mb_strwidth('Кол'), strlen((string) $sumQty));
    $amtW = max(mb_strwidth('Сумма'), strlen(money($sumTotal)));

    $line = fn (string $a, string $b, string $c): string => padRight($a, $nameW) . ' | ' . padLeft($b, $qtyW) . ' | ' . padLeft($c, $amtW);
    $sep = str_repeat('-', $nameW) . '-+-' . str_repeat('-', $qtyW) . '-+-' . str_repeat('-', $amtW);

    echo $line('Товар', 'Кол', 'Сумма'), "\n", $sep, "\n";
    foreach ($rows as [$name, $qty, $total]) {
        echo $line($name, (string) $qty, money($total)), "\n";
    }
    echo $sep, "\n", $line('Итого', (string) $sumQty, money($sumTotal)), "\n";
}

foreach ($invalid as $bad) {
    echo "invalid: $bad\n";
}
