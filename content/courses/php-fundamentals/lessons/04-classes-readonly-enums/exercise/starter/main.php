<?php
declare(strict_types=1);

/** Неизменяемая сумма в копейках. */
final readonly class Money
{
    public function __construct(public int $cents)
    {
    }

    public static function zero(): self
    {
        return new self(0);
    }

    public function plus(self $other): self
    {
        // TODO: верните новую сумму (объект неизменяем)
        return $this;
    }

    public function times(int $factor): self
    {
        // TODO: умножьте на количество
        return $this;
    }

    /** Доля в процентах, дробная часть копейки отбрасывается. */
    public function percent(int $percent): self
    {
        // TODO: intdiv($this->cents * $percent, 100)
        return $this;
    }

    public function __toString(): string
    {
        // TODO: формат "12.34" (рубли.копейки с двумя знаками)
        return (string) $this->cents;
    }
}

enum Size: string
{
    case S = 's';
    case M = 'm';
    case L = 'l';

    public function percent(): int
    {
        // TODO: S - 100, M - 125, L - 150 (процентов от базовой цены)
        return 100;
    }
}

$total = Money::zero();
$items = 0;
while (($line = fgets(STDIN)) !== false) {
    $line = trim($line);
    if ($line === '') {
        continue;
    }
    $parts = preg_split('/\s+/', $line);
    $size = count($parts) === 4 ? Size::tryFrom(strtolower($parts[1])) : null;
    $valid = $size !== null
        && preg_match('/^[1-9][0-9]*$/', $parts[2]) === 1
        && preg_match('/^[0-9]+$/', $parts[3]) === 1;
    if (!$valid) {
        echo "invalid: $line\n";
        continue;
    }
    [$name, , $qty, $price] = $parts;
    $unit = (new Money((int) $price))->percent($size->percent());
    $sum = $unit->times((int) $qty);
    $total = $total->plus($sum);
    $items += (int) $qty;
    echo "$name {$size->name} $qty x $unit = $sum\n";
}
echo "items=$items total=$total\n";
