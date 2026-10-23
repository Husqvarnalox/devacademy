<?php
declare(strict_types=1);

function parseAge(string $text): int
{
    // TODO: превратите $text в целое число через filter_var.
    // Если это не целое число, бросьте InvalidArgumentException("не целое число: <текст>").
    // Если число меньше 0 или больше 120, бросьте InvalidArgumentException("возраст вне диапазона 0-120: <число>").
    // Иначе верните число.
    return 0;
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
