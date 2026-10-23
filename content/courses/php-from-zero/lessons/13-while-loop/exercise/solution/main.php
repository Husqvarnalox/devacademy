<?php
$goal = (int) trim(fgets(STDIN));
$monthly = (int) trim(fgets(STDIN));
$saved = (int) trim(fgets(STDIN));

$months = 0;
while ($saved < $goal) {
    $saved += $monthly;
    $months++;
}

echo "Месяцев: {$months}\n";
echo "Накоплено: {$saved} руб.\n";
