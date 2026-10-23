<?php
declare(strict_types=1);

// Допишите функции parseRecord и summary. Код ниже читает ввод и вызывает их - менять его не нужно.

/**
 * Разбирает строку "имя оценка" (поля разделены одним пробелом) и возвращает [имя, оценка].
 * При ошибке бросает InvalidArgumentException с сообщением:
 *   "ожидалось 2 поля, получено N"          - если полей не два;
 *   "неверная оценка: <текст>"              - если оценка не целое число;
 *   "оценка вне диапазона 0-100: <число>"   - если число меньше 0 или больше 100.
 *
 * @return array{0: string, 1: int}
 */
function parseRecord(string $line): array
{
    // TODO
    throw new InvalidArgumentException("не реализовано");
}

/**
 * Итоговая строка по списку оценок: "n=3 avg=84.33 max=95" (среднее с двумя знаками после точки).
 * Для пустого списка - "нет данных".
 */
function summary(array $scores): string
{
    // TODO
    return "";
}

$scores = [];
$lineNumber = 0;
while (($line = fgets(STDIN)) !== false) {
    $lineNumber++;
    $line = trim($line);
    if ($line === "") {
        continue;
    }
    try {
        [$name, $score] = parseRecord($line);
        $scores[] = $score;
        echo "{$name}: {$score}\n";
    } catch (InvalidArgumentException $e) {
        echo "Строка {$lineNumber}: ", $e->getMessage(), "\n";
    }
}
echo summary($scores), "\n";
