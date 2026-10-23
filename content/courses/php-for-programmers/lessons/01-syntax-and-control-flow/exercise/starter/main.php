<?php
declare(strict_types=1);

// Допишите четыре функции. Код ниже читает команды и печатает результат их вызова - менять его не нужно.

/**
 * Остаток от деления $a на $b ($b больше нуля), всегда от 0 до $b - 1: floorMod(-7, 3) равно 2.
 */
function floorMod(int $a, int $b): int
{
    // TODO
    return 0;
}

/**
 * Число символов (не байтов) в строке.
 */
function charCount(string $text): int
{
    // TODO
    return 0;
}

/**
 * Целое число из строки или null, если строка не является целым числом в десятичной записи.
 * Значение 0 - корректное число.
 */
function parseInt(string $text): ?int
{
    // TODO
    return null;
}

/**
 * Сумма десятичных цифр числа без учёта знака: digitSum(-123) равно 6.
 */
function digitSum(int $n): int
{
    // TODO
    return 0;
}

while (($line = fgets(STDIN)) !== false) {
    $line = trim($line);
    if ($line === "") {
        continue;
    }
    $parts = explode(" ", $line, 2);
    $command = $parts[0];
    $rest = $parts[1] ?? "";
    if ($command === "mod") {
        [$a, $b] = explode(" ", $rest);
        echo floorMod((int) $a, (int) $b), "\n";
    } elseif ($command === "chars") {
        echo charCount($rest), "\n";
    } elseif ($command === "int") {
        $n = parseInt($rest);
        echo $n === null ? "null" : $n, "\n";
    } elseif ($command === "digits") {
        echo digitSum((int) $rest), "\n";
    }
}
