<?php
declare(strict_types=1);

function parseAge(string $text): int
{
    $age = filter_var($text, FILTER_VALIDATE_INT);
    if ($age === false) {
        throw new InvalidArgumentException("не целое число: " . $text);
    }
    if ($age < 0 || $age > 120) {
        throw new InvalidArgumentException("возраст вне диапазона 0-120: " . $age);
    }
    return $age;
}

while (($line = fgets(STDIN)) !== false) {
    $line = trim($line);
    if ($line === "") {
        continue;
    }
    try {
        $age = parseAge($line);
        echo "OK: ", $age, "\n";
    } catch (InvalidArgumentException $e) {
        echo "Ошибка: ", $e->getMessage(), "\n";
    }
}
