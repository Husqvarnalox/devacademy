<?php
declare(strict_types=1);

$n = (int) trim(fgets(STDIN));
$grades = [];
for ($i = 0; $i < $n; $i++) {
    $grades[] = (int) trim(fgets(STDIN));
}

// TODO: напечатайте пять строк (формат - в тексте задания), используя готовые функции для массивов.
