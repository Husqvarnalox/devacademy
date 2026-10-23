<?php
declare(strict_types=1);

// Допишите класс CalcException и функции evaluate и kind. Код ниже читает выражения - менять его не нужно.

/**
 * Ошибка вычисления. Наследуйте от Exception.
 */
final class CalcException extends Exception
{
}

/**
 * Вычисляет "a op b" для целых a и b и операций + - * / % (/ и % - целочисленные: intdiv и %).
 * Бросает CalcException с сообщением:
 *   "не число: X"             - операнд не целое число (проверка через filter_var);
 *   "неизвестная операция: X" - операция не из списка;
 *   "деление на ноль"         - при / и % с нулём справа; причиной (previous) должно быть
 *                               исходное DivisionByZeroError.
 * Используйте match.
 */
function evaluate(string $a, string $op, string $b): int
{
    $left = filter_var($a, FILTER_VALIDATE_INT);
    if ($left === false) {
        throw new CalcException("не число: {$a}");
    }
    $right = filter_var($b, FILTER_VALIDATE_INT);
    if ($right === false) {
        throw new CalcException("не число: {$b}");
    }
    try {
        return match ($op) {
            "+" => $left + $right,
            "-" => $left - $right,
            "*" => $left * $right,
            "/" => intdiv($left, $right),
            "%" => $left % $right,
            default => throw new CalcException("неизвестная операция: {$op}"),
        };
    } catch (DivisionByZeroError $e) {
        throw new CalcException("деление на ноль", 0, $e);
    }
}

/**
 * Описание результата: "отрицательное", "ноль" или "положительное". Используйте match (true).
 */
function kind(int $n): string
{
    return match (true) {
        $n < 0 => "отрицательное",
        $n === 0 => "ноль",
        default => "положительное",
    };
}

$processed = 0;
$failed = 0;
while (($line = fgets(STDIN)) !== false) {
    $line = trim($line);
    if ($line === "") {
        continue;
    }
    try {
        $tokens = explode(" ", $line);
        if (count($tokens) !== 3) {
            throw new CalcException("формат: a op b");
        }
        $result = evaluate($tokens[0], $tokens[1], $tokens[2]);
        echo "{$line} = {$result} (", kind($result), ")\n";
    } catch (CalcException $e) {
        $failed++;
        echo "ошибка: ", $e->getMessage(), "\n";
        if ($e->getPrevious() !== null) {
            echo "причина: ", get_class($e->getPrevious()), "\n";
        }
    } finally {
        $processed++;
    }
}
echo "обработано: {$processed}, ошибок: {$failed}\n";
