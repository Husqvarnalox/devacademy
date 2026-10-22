<?php
$price = (int) trim(fgets(STDIN));
$maxCount = (int) trim(fgets(STDIN));

for ($count = 1; $count <= $maxCount; $count++) {
    $total = $price * $count;
    echo "{$count} шт. - {$total} руб.\n";
}
echo "Итого позиций: {$maxCount}\n";
