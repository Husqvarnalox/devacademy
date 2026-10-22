<?php
$n = (int) trim(fgets(STDIN));

$count = 0;
$sum = 0;
for ($i = 0; $i < $n; $i++) {
    $price = (int) trim(fgets(STDIN));
    if ($price < 0) {
        continue;
    }
    if ($price === 0) {
        break;
    }
    $count++;
    $sum += $price;
}

echo "Принято: {$count}\n";
echo "Сумма: {$sum} руб.\n";
