<?php
$a = (float) trim(fgets(STDIN));
$b = (float) trim(fgets(STDIN));
$sum = $a + $b;
echo "Сумма: {$sum}\n";
echo "Целая часть: " . (int) $sum . "\n";
