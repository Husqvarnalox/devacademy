<?php
declare(strict_types=1);

function line(string $word, int $count): string
{
    return $word . " " . $count;
}

$text = stream_get_contents(STDIN);
$counts = [];

// TODO: разбейте $text на слова и накопите счётчики в $counts (слово => количество)

// TODO: отсортируйте по убыванию количества, сохранив ключи

foreach ($counts as $word => $count) {
    echo line($word, $count), PHP_EOL;
}
