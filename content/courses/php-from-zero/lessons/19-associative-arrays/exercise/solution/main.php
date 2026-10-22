<?php
declare(strict_types=1);

$n = (int) trim(fgets(STDIN));
$counts = [];
for ($i = 0; $i < $n; $i++) {
    $word = trim(fgets(STDIN));
    $counts[$word] = ($counts[$word] ?? 0) + 1;
}

ksort($counts);
foreach ($counts as $word => $count) {
    echo "{$word}: {$count}\n";
}
echo "Видов: ", count($counts), "\n";

arsort($counts);
foreach ($counts as $word => $count) {
    echo "Чаще всего: {$word} ({$count})\n";
    break;
}
