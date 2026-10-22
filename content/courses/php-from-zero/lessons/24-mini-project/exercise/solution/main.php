<?php
declare(strict_types=1);

class Purchase
{
    public function __construct(
        public string $category,
        public string $name,
        public int $price,
        public int $quantity,
    ) {
    }

    public function total(): int
    {
        return $this->price * $this->quantity;
    }
}

function parsePurchase(string $line): Purchase
{
    $parts = explode(";", $line);
    if (count($parts) !== 4) {
        throw new InvalidArgumentException("нужно 4 поля, найдено " . count($parts));
    }
    $category = trim($parts[0]);
    $name = trim($parts[1]);
    if ($category === "" || $name === "") {
        throw new InvalidArgumentException("пустая категория или название");
    }
    $price = filter_var(trim($parts[2]), FILTER_VALIDATE_INT);
    $quantity = filter_var(trim($parts[3]), FILTER_VALIDATE_INT);
    if ($price === false || $quantity === false || $price <= 0 || $quantity <= 0) {
        throw new InvalidArgumentException("цена и количество должны быть положительными целыми");
    }
    return new Purchase($category, $name, $price, $quantity);
}

$counts = [];
$sums = [];
$overall = 0;
$skipped = 0;
$bestName = "";
$bestTotal = 0;

while (($line = fgets(STDIN)) !== false) {
    $line = trim($line);
    if ($line === "") {
        continue;
    }
    try {
        $purchase = parsePurchase($line);
    } catch (InvalidArgumentException $e) {
        $skipped++;
        continue;
    }
    $total = $purchase->total();
    $counts[$purchase->category] = ($counts[$purchase->category] ?? 0) + 1;
    $sums[$purchase->category] = ($sums[$purchase->category] ?? 0) + $total;
    $overall += $total;
    if ($bestName === "" || $total > $bestTotal) {
        $bestName = $purchase->name;
        $bestTotal = $total;
    }
}

ksort($sums);
foreach ($sums as $category => $sum) {
    echo $category, ": ", $counts[$category], " поз., ", $sum, " руб.\n";
}
echo "Всего: ", $overall, " руб.\n";
if ($bestName !== "") {
    echo "Самая дорогая позиция: ", $bestName, " (", $bestTotal, " руб.)\n";
}
echo "Пропущено строк: ", $skipped, "\n";
