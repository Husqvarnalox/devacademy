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
        return new self($this->cents + $other->cents);
    }

    public function times(int $factor): self
    {
        return new self($this->cents * $factor);
    }

    /** Доля в процентах, дробная часть копейки отбрасывается. */
    public function percent(int $percent): self
    {
        return new self(intdiv($this->cents * $percent, 100));
    }

    public function __toString(): string
    {
        return sprintf('%d.%02d', intdiv($this->cents, 100), $this->cents % 100);
    }
}

enum Size: string
{
    case S = 's';
    case M = 'm';
    case L = 'l';

    public function percent(): int
    {
        return match ($this) {
            self::S => 100,
            self::M => 125,
            self::L => 150,
        };
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
