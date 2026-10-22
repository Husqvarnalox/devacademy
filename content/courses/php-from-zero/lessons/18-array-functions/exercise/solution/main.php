<?php
declare(strict_types=1);

$n = (int) trim(fgets(STDIN));
$grades = [];
for ($i = 0; $i < $n; $i++) {
    $grades[] = (int) trim(fgets(STDIN));
}

sort($grades);
echo "Оценки по возрастанию: ", implode(", ", $grades), "\n";
echo "Сумма: ", array_sum($grades), "\n";
printf("Среднее: %.2F\n", array_sum($grades) / count($grades));

rsort($grades);
echo "Лучшие три: ", implode(", ", array_slice($grades, 0, 3)), "\n";
if (in_array(2, $grades, true)) {
    echo "Есть двойка: да\n";
} else {
    echo "Есть двойка: нет\n";
}
