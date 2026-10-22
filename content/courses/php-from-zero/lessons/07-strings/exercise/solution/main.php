<?php
$first = "Анна";
$last = "Смирнова";
$full = $first . " " . $last;
echo "Полное имя: " . $full . "\n";
echo "Букв в фамилии: " . mb_strlen($last) . "\n";
echo "Заглавными: " . mb_strtoupper($full) . "\n";
