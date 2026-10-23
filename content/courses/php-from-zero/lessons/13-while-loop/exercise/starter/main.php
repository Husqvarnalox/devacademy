<?php
$goal = (int) trim(fgets(STDIN));
$monthly = (int) trim(fgets(STDIN));
$saved = (int) trim(fgets(STDIN));

$months = 0;
// TODO: пока $saved меньше $goal, откладывайте $monthly и считайте месяцы.

echo "Месяцев: {$months}\n";
echo "Накоплено: {$saved} руб.\n";
