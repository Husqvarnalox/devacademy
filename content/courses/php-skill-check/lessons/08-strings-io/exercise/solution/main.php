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
    $result = "";
    foreach (preg_split('/\s+/', trim($fullName), -1, PREG_SPLIT_NO_EMPTY) as $word) {
        $result .= mb_strtoupper(mb_substr($word, 0, 1));
    }
    return $result;
}

/**
 * Обрезает строку до $max символов; если она длиннее, заканчивает её символом "…" (итоговая длина ровно $max).
 */
function truncate(string $text, int $max): string
{
    if (mb_strlen($text) <= $max) {
        return $text;
    }
    return mb_substr($text, 0, $max - 1) . "…";
}

/**
 * Выравнивает по ширине $width символов пробелами: справа ($alignRight = false) или слева.
 * Если строка длиннее ширины, возвращает её без изменений.
 */
function pad(string $text, int $width, bool $alignRight = false): string
{
    return mb_str_pad($text, $width, " ", $alignRight ? STR_PAD_LEFT : STR_PAD_RIGHT);
}

/**
 * Деньги из копеек: 123456 -> "1 234,56" (пробел - разделитель тысяч, запятая - дробная часть).
 */
function money(int $cents): string
{
    return number_format($cents / 100, 2, ",", " ");
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
