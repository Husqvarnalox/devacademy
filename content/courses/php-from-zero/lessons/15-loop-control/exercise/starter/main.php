<?php
$n = (int) trim(fgets(STDIN));

$count = 0;
$sum = 0;
for ($i = 0; $i < $n; $i++) {
    $price = (int) trim(fgets(STDIN));
    // TODO: отрицательную цену пропустите (continue), на цене 0 остановите чтение (break),
    // остальные цены учитывайте: увеличьте $count и добавьте цену к $sum.
}

echo "Принято: {$count}\n";
echo "Сумма: {$sum} руб.\n";
