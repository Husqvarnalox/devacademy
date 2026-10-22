<?php
declare(strict_types=1);

// Допишите четыре функции. Код ниже читает записи и печатает таблицу - менять его не нужно.
// Запись: "полное имя;сумма в копейках".

/**
 * Инициалы: первая буква каждого слова в верхнем регистре, без точек ("иван петров" -> "ИП").
 * Слова разделены любым числом пробелов; для пустой строки - пустая строка.
 */
function initials(string $fullName): string
{
    // TODO
    return "";
}

/**
 * Обрезает строку до $max символов; если она длиннее, заканчивает её символом "…" (итоговая длина ровно $max).
 */
function truncate(string $text, int $max): string
{
    // TODO
    return $text;
}

/**
 * Выравнивает по ширине $width символов пробелами: справа ($alignRight = false) или слева.
 * Если строка длиннее ширины, возвращает её без изменений.
 */
function pad(string $text, int $width, bool $alignRight = false): string
{
    // TODO
    return $text;
}

/**
 * Деньги из копеек: 123456 -> "1 234,56" (пробел - разделитель тысяч, запятая - дробная часть).
 */
function money(int $cents): string
{
    // TODO
    return (string) $cents;
}

$total = 0;
$count = 0;
while (($line = fgets(STDIN)) !== false) {
    $line = trim($line);
    if ($line === "") {
        continue;
    }
    $parts = explode(";", $line);
    $cents = count($parts) === 2 ? filter_var($parts[1], FILTER_VALIDATE_INT) : false;
    if ($cents === false || $cents < 0) {
        echo "пропущено\n";
        continue;
    }
    echo pad(initials($parts[0]), 4), pad(truncate($parts[0], 12), 13), pad(money($cents), 12, true), "\n";
    $total += $cents;
    $count++;
}
echo "всего: ", money($total), " (", $count, ")\n";
