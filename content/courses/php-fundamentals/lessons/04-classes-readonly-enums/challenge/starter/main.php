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
        // TODO: PAY из NEW; SHIP из PAID; DELIVER из SHIPPED; CANCEL из NEW или PAID
        return null;
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
        // TODO: верните новый Order с состоянием $to и историей, дополненной $to
        return $this;
    }
}

/** @param array<int|string, Order> $orders */
function handle(array &$orders, string $word, string $id): string
{
    // TODO: верните одну строку ответа ("ok ..." или "error ...")
    return '';
}

/**
 * @param array<int|string, Order> $orders
 * @return list<string> строки "history ..." по id побайтно, затем "summary: ..."
 */
function report(array $orders): array
{
    // TODO
    return [];
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
