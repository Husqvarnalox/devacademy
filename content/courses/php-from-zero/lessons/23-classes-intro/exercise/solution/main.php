<?php
declare(strict_types=1);

class Item
{
    public function __construct(
        public string $name,
        public int $price,
        public int $quantity,
    ) {
    }

    public function total(): int
    {
        return $this->price * $this->quantity;
    }

    public function label(): string
    {
        return "{$this->name} x {$this->quantity}";
    }
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
