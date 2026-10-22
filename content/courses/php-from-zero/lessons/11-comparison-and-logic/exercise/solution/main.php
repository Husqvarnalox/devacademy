<?php
$grade = (int) trim(fgets(STDIN));
$attendance = (int) trim(fgets(STDIN));

$passedExam = $grade >= 60;
$admitted = $attendance >= 75;
$credit = $passedExam && $admitted;
$retake = !$credit;
$perfect = $grade === 100;

echo "Сдал экзамен: ";
var_dump($passedExam);
echo "Допущен: ";
var_dump($admitted);
echo "Зачёт: ";
var_dump($credit);
echo "Пересдача: ";
var_dump($retake);
echo "Максимум: ";
var_dump($perfect);
