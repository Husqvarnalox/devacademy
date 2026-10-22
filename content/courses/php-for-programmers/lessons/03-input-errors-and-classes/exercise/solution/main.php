<?php
declare(strict_types=1);

// Допишите класс Item и функцию parseItem. Код ниже читает ввод и вызывает их - менять его не нужно.

class Item
{
    public function __construct(public string $name, public int $quantity, public int $price)
    {
    }

    public function total(): int
    {
        return $this->quantity * $this->price;
    }
}

/**
 * Разбирает строку "название;количество;цена" и возвращает Item.
 * При ошибке бросает InvalidArgumentException с сообщением:
 *   "ожидалось 3 поля, получено N" - если полей не три;
 *   "пустое название"              - если название (после trim) пустое;
 *   "неверное количество: <текст>" - если количество не целое число от 0 и больше;
 *   "неверная цена: <текст>"       - если цена не целое число больше 0.
 * Поля обрезаются через trim; в сообщении текст - уже обрезанный. Проверки идут в указанном порядке.
 */
function parseItem(string $line): Item
{
    $fields = explode(";", $line);
    if (count($fields) !== 3) {
        throw new InvalidArgumentException("ожидалось 3 поля, получено " . count($fields));
    }
    [$name, $quantityText, $priceText] = array_map('trim', $fields);
    if ($name === "") {
        throw new InvalidArgumentException("пустое название");
    }
    $quantity = filter_var($quantityText, FILTER_VALIDATE_INT);
    if ($quantity === false || $quantity < 0) {
        throw new InvalidArgumentException("неверное количество: {$quantityText}");
    }
    $price = filter_var($priceText, FILTER_VALIDATE_INT);
    if ($price === false || $price <= 0) {
        throw new InvalidArgumentException("неверная цена: {$priceText}");
    }
    return new Item($name, $quantity, $price);
}

$items = [];
$errors = 0;
$lineNumber = 0;
while (($line = fgets(STDIN)) !== false) {
    $lineNumber++;
    $line = trim($line);
    if ($line === "") {
        continue;
    }
    try {
        $item = parseItem($line);
        $items[] = $item;
        echo $item->name, ": ", $item->quantity, " x ", $item->price, " = ", $item->total(), "\n";
    } catch (InvalidArgumentException $e) {
        $errors++;
        echo "Строка {$lineNumber}: ", $e->getMessage(), "\n";
    }
}
$sum = 0;
foreach ($items as $item) {
    $sum += $item->total();
}
echo "Итого: {$sum}, позиций: ", count($items), ", ошибок: {$errors}\n";
