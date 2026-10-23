<?php
declare(strict_types=1);

// Допишите четыре функции. Код ниже читает команды и печатает результат их вызова - менять его не нужно.

/**
 * "FizzBuzz", если $n делится на 3 и на 5; "Fizz" - только на 3; "Buzz" - только на 5; иначе число, записанное строкой.
 */
function fizzBuzz(int $n): string
{
    if ($n % 15 === 0) {
        return "FizzBuzz";
    }
    if ($n % 3 === 0) {
        return "Fizz";
    }
    if ($n % 5 === 0) {
        return "Buzz";
    }
    return (string) $n;
}

/**
 * true для високосного года: делится на 4, но не на 100, кроме годов, которые делятся на 400.
 */
function isLeapYear(int $year): bool
{
    return ($year % 4 === 0 && $year % 100 !== 0) || $year % 400 === 0;
}

/**
 * Наименьшее простое число, строго большее $n ($n может быть отрицательным).
 */
function nextPrime(int $n): int
{
    $candidate = $n + 1;
    while (true) {
        $isPrime = $candidate >= 2;
        for ($d = 2; $d * $d <= $candidate; $d++) {
            if ($candidate % $d === 0) {
                $isPrime = false;
                break;
            }
        }
        if ($isPrime) {
            return $candidate;
        }
        $candidate++;
    }
}

/**
 * Наибольший общий делитель двух неотрицательных чисел: gcd(0, 5) равно 5.
 */
function gcd(int $a, int $b): int
{
    while ($b !== 0) {
        $rest = $a % $b;
        $a = $b;
        $b = $rest;
    }
    return $a;
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
