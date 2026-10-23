<?php
declare(strict_types=1);

$accepted = 0;
$skipped = 0;
$sum = 0;

while (($line = fgets(STDIN)) !== false) {
    $line = trim($line);
    if ($line === "") {
        continue;
    }
    $number = filter_var($line, FILTER_VALIDATE_INT);
    if ($number === false) {
        $skipped++;
        continue;
    }
    $accepted++;
    $sum += $number;
}

echo "Принято: ", $accepted, "\n";
echo "Пропущено: ", $skipped, "\n";
echo "Сумма: ", $sum, "\n";
