<?php
declare(strict_types=1);

class LedgerException extends RuntimeException {}
final class AccountExists extends LedgerException {}
final class UnknownAccount extends LedgerException {}
final class InsufficientFunds extends LedgerException {}

final class Ledger
{
    /** @var array<string, int> */
    private array $balances = [];

    public function open(string $name): void
    {
        if (isset($this->balances[$name])) {
            throw new AccountExists("account $name already exists");
        }
        $this->balances[$name] = 0;
    }

    public function balance(string $name): int
    {
        return $this->balances[$name] ?? throw new UnknownAccount("no such account $name");
    }

    public function deposit(string $name, int $amount): int
    {
        $this->balance($name);
        return $this->balances[$name] += $amount;
    }

    public function withdraw(string $name, int $amount): int
    {
        $balance = $this->balance($name);
        // TODO: если $amount больше $balance, бросьте InsufficientFunds
        //       с сообщением "insufficient funds: balance=<b>, requested=<r>"
        return $this->balances[$name] -= $amount;
    }

    /** Перевод атомарен: при любой ошибке балансы остаются прежними. */
    public function transfer(string $from, string $to, int $amount): string
    {
        if ($from === $to) {
            throw new InvalidArgumentException('cannot transfer to the same account');
        }
        $snapshot = $this->balances;
        try {
            $this->balance($from);
            $this->balance($to);
            $a = $this->withdraw($from, $amount);
            $b = $this->deposit($to, $amount);
            return "$from=$a $to=$b";
        } catch (LedgerException $e) {
            // TODO: верните балансы из $snapshot, чтобы перевод был атомарным
            throw $e;
        }
    }

    /** @return array<string, int> */
    public function all(): array
    {
        $copy = $this->balances;
        ksort($copy, SORT_STRING);
        return $copy;
    }
}

function parseAmount(string $raw): int
{
    // TODO: сумма - целое из одних цифр и не меньше 1, иначе
    //       InvalidArgumentException("invalid amount <raw>")
    return (int) $raw;
}

$ledger = new Ledger();
$handled = 0;
$errors = 0;

while (($line = fgets(STDIN)) !== false) {
    $line = trim($line);
    if ($line === '') {
        continue;
    }
    $words = preg_split('/\s+/', $line);
    $cmd = strtoupper(array_shift($words));
    try {
        [$arity, $run] = match ($cmd) {
            'OPEN'     => [1, fn (string $n) => $ledger->open($n) ?? "opened $n"],
            'DEPOSIT'  => [2, fn (string $n, string $a) => "$n balance=" . $ledger->deposit($n, parseAmount($a))],
            'WITHDRAW' => [2, fn (string $n, string $a) => "$n balance=" . $ledger->withdraw($n, parseAmount($a))],
            'TRANSFER' => [3, fn (string $f, string $t, string $a) => $ledger->transfer($f, $t, parseAmount($a))],
        };
        if (count($words) !== $arity) {
            throw new ArgumentCountError("$cmd takes $arity argument(s)");
        }
        $result = $run(...$words);
        echo "ok $result\n";
    } catch (UnhandledMatchError) {
        $errors++;
        echo "error UnknownCommand: $cmd\n";
    } catch (LedgerException | InvalidArgumentException | ArgumentCountError $e) {
        $errors++;
        echo 'error ', get_class($e), ': ', $e->getMessage(), "\n";
    } finally {
        // TODO: считайте каждую обработанную команду здесь, чтобы это работало при любом исходе
    }
}

echo "handled=$handled errors=$errors\n";
foreach ($ledger->all() as $name => $balance) {
    echo "$name=$balance\n";
}
