<?php
declare(strict_types=1);

function maxValue(array $values): int
{
    $best = $values[0];
    foreach ($values as $value) {
        if ($value > $best) {
            $best = $value;
        }
    }
    return $best;
}

function countAbove(array $values, int $limit): int
{
    $count = 0;
    foreach ($values as $value) {
        if ($value > $limit) {
            $count++;
        }
    }
    return $count;
}

$n = (int) trim(fgets(STDIN));
$limit = (int) trim(fgets(STDIN));
$temperatures = [];
for ($i = 0; $i < $n; $i++) {
    $temperatures[] = (int) trim(fgets(STDIN));
}

echo "Дней: ", count($temperatures), "\n";
echo "Максимум: ", maxValue($temperatures), "\n";
echo "Дней выше ", $limit, ": ", countAbove($temperatures, $limit), "\n";
