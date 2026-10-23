<?php
declare(strict_types=1);

class Item
{
    // TODO: конструктор с продвижением параметров:
    //   public string $name, public int $price, public int $quantity
    // TODO: метод total(): int - цена, умноженная на количество
    // TODO: метод label(): string - строка вида "Молоко x 2" (название, " x ", количество)
}

$sum = 0;
while (($line = fgets(STDIN)) !== false) {
    $line = trim($line);
    if ($line === "") {
        continue;
    }
    $parts = explode(";", $line);
    $item = new Item($parts[0], (int) $parts[1], (int) $parts[2]);
    echo $item->label(), " = ", $item->total(), " руб.\n";
    $sum += $item->total();
}
echo "Итого: ", $sum, " руб.\n";
