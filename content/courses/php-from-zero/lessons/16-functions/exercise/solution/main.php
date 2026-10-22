<?php
declare(strict_types=1);

function totalPrice(int $price, int $quantity, int $discountPercent = 0): int
{
    $full = $price * $quantity;
    return $full - intdiv($full * $discountPercent, 100);
}

function receiptLine(string $name, int $quantity, int $total): string
{
    return "{$name} x {$quantity} = {$total} руб.";
}

$name = trim(fgets(STDIN));
$price = (int) trim(fgets(STDIN));
$quantity = (int) trim(fgets(STDIN));
$discount = (int) trim(fgets(STDIN));

echo receiptLine($name, $quantity, totalPrice($price, $quantity, $discount)), "\n";
echo "Без скидки: ", totalPrice($price, $quantity), " руб.\n";
