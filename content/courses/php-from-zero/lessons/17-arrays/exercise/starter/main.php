<?php
declare(strict_types=1);

// Допишите тела двух функций. Код ниже читает ввод и вызывает ваши функции - его менять не нужно.
// Готовые функции max и array_filter пока не используйте: обойдите массив циклом foreach.

/** Наибольшее значение в непустом массиве. */
function maxValue(array $values): int
{
    // TODO
    return 0;
}

/** Сколько значений в массиве строго больше $limit. */
function countAbove(array $values, int $limit): int
{
    // TODO
    return 0;
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
