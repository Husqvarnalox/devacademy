<?php
$total = 135;
$hours = intdiv($total, 60);
$minutes = $total % 60;
echo "Всего минут: {$total}\n";
echo "Часов: {$hours}\n";
echo "Минут: {$minutes}\n";
