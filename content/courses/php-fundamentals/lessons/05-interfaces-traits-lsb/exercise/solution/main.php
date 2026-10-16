<?php
declare(strict_types=1);

interface Priced
{
    public function gross(): int;
}

/** Счётчик созданных объектов: у каждого класса, использующего трейт, он свой. */
trait Counted
{
    private static int $made = 0;

    public static function made(): int
    {
        return self::$made;
    }

    protected static function markMade(): void
    {
        self::$made++;
    }
}

abstract class Product implements Priced
{
    /** Налог в процентах; потомки переопределяют. */
    protected const RATE = 0;

    final public function __construct(
        public readonly string $title,
        public readonly int $net,
    ) {
        static::markMade();
    }

    abstract public static function kind(): string;

    abstract protected static function markMade(): void;

    /** Разбирает «<копейки> <название>» и создаёт объект вызвавшего класса. */
    public static function fromParts(string $cents, string $title): ?static
    {
        if (!preg_match('/^[0-9]+$/', $cents) || $title === '') {
            return null;
        }
        return new static($title, (int) $cents);
    }

    public function gross(): int
    {
        return $this->net + intdiv($this->net * static::RATE + 50, 100);
    }
}

final class Book extends Product
{
    use Counted;
    protected const RATE = 7;
    public static function kind(): string { return 'book'; }
}

final class Ebook extends Product
{
    use Counted;
    protected const RATE = 19;
    public static function kind(): string { return 'ebook'; }
}

final class Audio extends Product
{
    use Counted;
    protected const RATE = 12;
    public static function kind(): string { return 'audio'; }
}

final class Game extends Product
{
    use Counted;
    protected const RATE = 21;
    public static function kind(): string { return 'game'; }
}

function money(int $cents): string
{
    return sprintf('%d.%02d', intdiv($cents, 100), $cents % 100);
}

/** @var array<string, class-string<Product>> $registry */
$registry = [];
foreach ([Book::class, Ebook::class, Audio::class, Game::class] as $class) {
    $registry[$class::kind()] = $class;
}

$total = 0;
while (($line = fgets(STDIN)) !== false) {
    $line = trim($line);
    if ($line === '') {
        continue;
    }
    $parts = preg_split('/\s+/', $line, 3);
    $class = $registry[strtolower($parts[0])] ?? null;
    $item = $class !== null && count($parts) === 3 ? $class::fromParts($parts[1], $parts[2]) : null;
    if ($item === null) {
        echo "invalid: $line\n";
        continue;
    }
    $total += $item->gross();
    echo $class::kind(), ' ', $item->title, ': net=', money($item->net), ' gross=', money($item->gross()), "\n";
}

$counts = [];
foreach ($registry as $kind => $class) {
    $counts[] = $kind . '=' . $class::made();
}
echo 'counts: ', implode(' ', $counts), "\n";
echo 'total gross=', money($total), "\n";
