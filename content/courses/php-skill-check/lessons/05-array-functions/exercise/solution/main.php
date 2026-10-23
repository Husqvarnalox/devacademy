<?php
declare(strict_types=1);

// Допишите четыре функции. Код ниже читает данные и печатает результат их вызова - менять его не нужно.
// Строка записи: "имя,город,балл". Первая строка ввода - минимальный балл для списка лучших.

/**
 * Имена тех, чей балл не меньше $min, списком (с ключами 0, 1, 2, ...):
 * сначала по убыванию балла, при равенстве - по имени по возрастанию.
 *
 * @param list<array{name: string, city: string, score: int}> $rows
 * @return list<string>
 */
function topScorers(array $rows, int $min): array
{
    $good = array_filter($rows, fn(array $row): bool => $row["score"] >= $min);
    usort($good, fn(array $a, array $b): int => [$b["score"], $a["name"]] <=> [$a["score"], $b["name"]]);
    return array_column($good, "name");
}

/**
 * Сумма баллов по городам: город => сумма. Города отсортированы по названию.
 *
 * @param list<array{name: string, city: string, score: int}> $rows
 * @return array<string, int>
 */
function totalsByCity(array $rows): array
{
    $totals = [];
    foreach ($rows as $row) {
        $totals[$row["city"]] = ($totals[$row["city"]] ?? 0) + $row["score"];
    }
    ksort($totals);
    return $totals;
}

/**
 * Средний балл. Для пустого списка - 0.0.
 *
 * @param list<array{name: string, city: string, score: int}> $rows
 */
function averageScore(array $rows): float
{
    if ($rows === []) {
        return 0.0;
    }
    return array_sum(array_column($rows, "score")) / count($rows);
}

/**
 * Имена по городам: город => список имён в порядке ввода. Города отсортированы по названию.
 *
 * @param list<array{name: string, city: string, score: int}> $rows
 * @return array<string, list<string>>
 */
function namesByCity(array $rows): array
{
    $groups = [];
    foreach ($rows as $row) {
        $groups[$row["city"]][] = $row["name"];
    }
    ksort($groups);
    return $groups;
}

$min = (int) trim((string) fgets(STDIN));
$rows = [];
while (($line = fgets(STDIN)) !== false) {
    $parts = explode(",", trim($line));
    $score = count($parts) === 3 ? filter_var($parts[2], FILTER_VALIDATE_INT) : false;
    if ($score === false) {
        continue;
    }
    $rows[] = ["name" => $parts[0], "city" => $parts[1], "score" => $score];
}

$top = topScorers($rows, $min);
echo "top: ", $top === [] ? "-" : implode(", ", $top), "\n";
foreach (totalsByCity($rows) as $city => $total) {
    echo "{$city}: {$total}\n";
}
echo "avg: ", number_format(averageScore($rows), 2, '.', ''), "\n";
foreach (namesByCity($rows) as $city => $names) {
    echo "{$city} - ", implode(", ", $names), "\n";
}
