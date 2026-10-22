<?php
declare(strict_types=1);

// Допишите четыре функции. Код ниже читает команды и печатает результат их вызова - менять его не нужно.

/**
 * Медиана списка целых чисел (список не пустой): при чётном количестве - среднее двух средних.
 */
function median(array $numbers): float
{
    sort($numbers);
    $count = count($numbers);
    $middle = intdiv($count, 2);
    if ($count % 2 === 1) {
        return (float) $numbers[$middle];
    }
    return ($numbers[$middle - 1] + $numbers[$middle]) / 2;
}

/**
 * Самое длинное слово (по числу символов). Слова разделены пробелами. Из равных по длине - первое.
 * Для пустого текста - пустая строка.
 */
function longestWord(string $text): string
{
    $best = "";
    foreach (explode(" ", $text) as $word) {
        if (mb_strlen($word) > mb_strlen($best)) {
            $best = $word;
        }
    }
    return $best;
}

/**
 * Каждое слово с заглавной буквы, остальные буквы строчные. Слова разделены одним пробелом.
 */
function titleCase(string $text): string
{
    $result = [];
    foreach (explode(" ", $text) as $word) {
        $result[] = mb_strtoupper(mb_substr($word, 0, 1)) . mb_strtolower(mb_substr($word, 1));
    }
    return implode(" ", $result);
}

/**
 * Слово, которое встречается чаще всех (в нижнем регистре). При равенстве - то, что появилось раньше.
 * Для пустого текста - пустая строка.
 */
function mostCommon(string $text): string
{
    $counts = [];
    foreach (explode(" ", mb_strtolower($text)) as $word) {
        if ($word !== "") {
            $counts[$word] = ($counts[$word] ?? 0) + 1;
        }
    }
    $best = "";
    $bestCount = 0;
    foreach ($counts as $word => $count) {
        if ($count > $bestCount) {
            $best = (string) $word;
            $bestCount = $count;
        }
    }
    return $best;
}

while (($line = fgets(STDIN)) !== false) {
    $parts = explode(" ", trim($line), 2);
    $rest = $parts[1] ?? "";
    if ($parts[0] === "median") {
        echo number_format(median(array_map('intval', explode(" ", $rest))), 1, '.', ''), "\n";
    } elseif ($parts[0] === "longest") {
        echo longestWord($rest), "\n";
    } elseif ($parts[0] === "title") {
        echo titleCase($rest), "\n";
    } elseif ($parts[0] === "top") {
        echo mostCommon($rest), "\n";
    }
}
