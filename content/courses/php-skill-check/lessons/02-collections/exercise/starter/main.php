<?php
declare(strict_types=1);

// Допишите четыре функции. Код ниже читает команды и печатает результат их вызова - менять его не нужно.

/**
 * Медиана списка целых чисел (список не пустой): при чётном количестве - среднее двух средних.
 */
function median(array $numbers): float
{
    // TODO
    return 0.0;
}

/**
 * Самое длинное слово (по числу символов). Слова разделены пробелами. Из равных по длине - первое.
 * Для пустого текста - пустая строка.
 */
function longestWord(string $text): string
{
    // TODO
    return "";
}

/**
 * Каждое слово с заглавной буквы, остальные буквы строчные. Слова разделены одним пробелом.
 */
function titleCase(string $text): string
{
    // TODO
    return "";
}

/**
 * Слово, которое встречается чаще всех (в нижнем регистре). При равенстве - то, что появилось раньше.
 * Для пустого текста - пустая строка.
 */
function mostCommon(string $text): string
{
    // TODO
    return "";
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
