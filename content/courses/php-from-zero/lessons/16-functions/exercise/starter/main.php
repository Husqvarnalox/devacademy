<?php
declare(strict_types=1);

// Допишите тела двух функций. Код ниже читает ввод и вызывает ваши функции - его менять не нужно.

/**
 * Стоимость покупки: цена * количество минус скидка в процентах.
 * Размер скидки в рублях - целая часть от (сумма * процент / 100); используйте intdiv.
 * Если скидка не передана, она равна 0.
 */
function totalPrice(int $price, int $quantity, int $discountPercent = 0): int
{
    // TODO
    return 0;
}

/**
 * Строка чека вида "Хлеб x 3 = 122 руб."
 */
function receiptLine(string $name, int $quantity, int $total): string
{
    // TODO
    return "";
}

$name = trim(fgets(STDIN));
$price = (int) trim(fgets(STDIN));
$quantity = (int) trim(fgets(STDIN));
$discount = (int) trim(fgets(STDIN));

echo receiptLine($name, $quantity, totalPrice($price, $quantity, $discount)), "\n";
echo "Без скидки: ", totalPrice($price, $quantity), " руб.\n";
