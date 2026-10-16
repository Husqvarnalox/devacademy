<?php
declare(strict_types=1);

enum Event: string
{
    case Create = 'CREATE';
    case Pay = 'PAY';
    case Ship = 'SHIP';
    case Deliver = 'DELIVER';
    case Cancel = 'CANCEL';
}

enum State: string
{
    case New = 'new';
    case Paid = 'paid';
    case Shipped = 'shipped';
    case Delivered = 'delivered';
    case Cancelled = 'cancelled';

    /** Следующее состояние или null, если событие здесь недопустимо. */
    public function on(Event $event): ?self
    {
        return match ($event) {
            Event::Pay => $this === self::New ? self::Paid : null,
            Event::Ship => $this === self::Paid ? self::Shipped : null,
            Event::Deliver => $this === self::Shipped ? self::Delivered : null,
            Event::Cancel => $this === self::New || $this === self::Paid ? self::Cancelled : null,
            Event::Create => null,
        };
    }

    public function label(): string
    {
        return strtoupper($this->name);
    }
}

final readonly class Order
{
    /** @param list<State> $history */
    public function __construct(public string $id, public State $state, public array $history)
    {
    }

    public static function create(string $id): self
    {
        return new self($id, State::New, [State::New]);
    }

    public function advance(State $to): self
    {
        return new self($this->id, $to, [...$this->history, $to]);
    }
}

/** @param array<int|string, Order> $orders */
function handle(array &$orders, string $word, string $id): string
{
    $event = Event::tryFrom(strtoupper($word));
    if ($event === null) {
        return "error $id: unknown event $word";
    }
    if ($event === Event::Create) {
        if (isset($orders[$id])) {
            return "error $id: already exists";
        }
        $orders[$id] = Order::create($id);
        return "ok $id NEW";
    }
    if (!isset($orders[$id])) {
        return "error $id: no such order";
    }
    $order = $orders[$id];
    $next = $order->state->on($event);
    if ($next === null) {
        return "error $id: cannot {$event->value} from {$order->state->label()}";
    }
    $orders[$id] = $order->advance($next);
    return "ok $id {$next->label()}";
}

/**
 * @param array<int|string, Order> $orders
 * @return list<string>
 */
function report(array $orders): array
{
    $list = array_values($orders);
    usort($list, fn(Order $a, Order $b) => strcmp($a->id, $b->id));
    $lines = array_map(
        fn(Order $o) => "history {$o->id}: " . implode(' > ', array_map(fn(State $s) => $s->label(), $o->history)),
        $list
    );
    $counts = array_count_values(array_map(fn(Order $o) => $o->state->name, $list));
    $parts = array_map(fn(State $s) => $s->label() . '=' . ($counts[$s->name] ?? 0), State::cases());
    $lines[] = 'summary: ' . implode(' ', $parts);
    return $lines;
}

$orders = [];
while (($line = fgets(STDIN)) !== false) {
    $parts = preg_split('/\s+/', trim($line), -1, PREG_SPLIT_NO_EMPTY);
    if (count($parts) !== 2) {
        continue;
    }
    echo handle($orders, $parts[0], $parts[1]), "\n";
}
echo implode("\n", report($orders)), "\n";
