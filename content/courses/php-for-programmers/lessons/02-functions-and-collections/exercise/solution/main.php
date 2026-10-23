<?php
declare(strict_types=1);

// Допишите четыре функции. Код ниже читает команды и печатает результат их вызова - менять его не нужно.

/**
 * Различные числа из списка по возрастанию. Результат - список (ключи 0, 1, 2, ...).
 */
function uniqueSorted(array $numbers): array
{
    $unique = array_unique($numbers);
    sort($unique);
    return $unique;
}

/**
 * Индекс первого элемента, равного $needle по === (строки сравниваются как строки), или null, если такого нет.
 */
function findIndex(array $items, string $needle): ?int
{
    $index = array_search($needle, $items, true);
    return $index === false ? null : $index;
}

/**
 * Сколько раз встретилось каждое слово. Слова разделены одним пробелом, регистр не важен (кириллица тоже).
 * Ключ - слово в нижнем регистре, порядок - по первому появлению.
 */
function countWords(string $text): array
{
    $counts = [];
    foreach (explode(" ", mb_strtolower($text)) as $word) {
        if ($word === "") {
            continue;
        }
        $counts[$word] = ($counts[$word] ?? 0) + 1;
    }
    return $counts;
}

/**
 * Новые цены: каждая цена увеличена на $percent процентов, дробная часть надбавки отбрасывается (intdiv).
 * Ключи сохраняются, переданный массив не меняется.
 */
function raisePrices(array $prices, int $percent): array
{
    foreach ($prices as $name => $price) {
        $prices[$name] = $price + intdiv($price * $percent, 100);
    }
    return $prices;
}

while (($line = fgets(STDIN)) !== false) {
    $line = trim($line);
    if ($line === "") {
        continue;
    }
    $parts = explode(" ", $line, 2);
    $command = $parts[0];
    $rest = $parts[1] ?? "";
    $tokens = $rest === "" ? [] : explode(" ", $rest);
    if ($command === "uniq") {
        echo json_encode(uniqueSorted(array_map('intval', $tokens))), "\n";
    } elseif ($command === "find") {
        $index = findIndex(array_slice($tokens, 1), $tokens[0] ?? "");
        echo $index === null ? "нет" : $index, "\n";
    } elseif ($command === "words") {
        foreach (countWords($rest) as $word => $count) {
            echo $word, ": ", $count, "\n";
        }
        echo "---\n";
    } elseif ($command === "raise") {
        $percent = (int) array_shift($tokens);
        $prices = [];
        foreach ($tokens as $pair) {
            [$name, $price] = explode("=", $pair);
            $prices[$name] = (int) $price;
        }
        echo json_encode($prices), " -> ", json_encode(raisePrices($prices, $percent)), "\n";
    }
}
