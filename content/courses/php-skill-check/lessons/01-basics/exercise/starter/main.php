<?php
declare(strict_types=1);

// Допишите четыре функции. Код ниже читает команды и печатает результат их вызова - менять его не нужно.

/**
 * "FizzBuzz", если $n делится на 3 и на 5; "Fizz" - только на 3; "Buzz" - только на 5; иначе число, записанное строкой.
 */
function fizzBuzz(int $n): string
{
    // TODO
    return "";
}

/**
 * true для високосного года: делится на 4, но не на 100, кроме годов, которые делятся на 400.
 */
function isLeapYear(int $year): bool
{
    // TODO
    return false;
}

/**
 * Наименьшее простое число, строго большее $n ($n может быть отрицательным).
 */
function nextPrime(int $n): int
{
    // TODO
    return 0;
}

/**
 * Наибольший общий делитель двух неотрицательных чисел: gcd(0, 5) равно 5.
 */
function gcd(int $a, int $b): int
{
    // TODO
    return 0;
}

while (($line = fgets(STDIN)) !== false) {
    $parts = explode(" ", trim($line));
    if ($parts[0] === "fizz") {
        echo fizzBuzz((int) $parts[1]), "\n";
    } elseif ($parts[0] === "leap") {
        echo isLeapYear((int) $parts[1]) ? "да" : "нет", "\n";
    } elseif ($parts[0] === "prime") {
        echo nextPrime((int) $parts[1]), "\n";
    } elseif ($parts[0] === "gcd") {
        echo gcd((int) $parts[1], (int) $parts[2]), "\n";
    }
}
