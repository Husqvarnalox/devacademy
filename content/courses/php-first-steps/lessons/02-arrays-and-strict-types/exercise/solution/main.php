<?php
declare(strict_types=1);

function line(string $word, int $count): string
{
    return $word . " " . $count;
}

$text = stream_get_contents(STDIN);
$counts = [];

foreach (preg_split('/\s+/', $text, -1, PREG_SPLIT_NO_EMPTY) as $w) {
    $counts[$w] = ($counts[$w] ?? 0) + 1;
}

arsort($counts);

foreach ($counts as $word => $count) {
    echo line((string) $word, $count), PHP_EOL;
}
