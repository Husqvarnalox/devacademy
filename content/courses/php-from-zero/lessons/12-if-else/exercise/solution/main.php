<?php
$age = (int) trim(fgets(STDIN));

if ($age < 7) {
    $price = 0;
} elseif ($age < 18) {
    $price = 150;
} elseif ($age < 65) {
    $price = 300;
} else {
    $price = 200;
}

echo "Билет: {$price} руб.\n";
