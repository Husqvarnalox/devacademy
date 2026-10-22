<?php
$grade = (int) trim(fgets(STDIN));
$attendance = (int) trim(fgets(STDIN));

// Замените false на выражения-сравнения. Условия - в тексте задания.
$passedExam = false;
$admitted = false;
$credit = false;
$retake = false;
$perfect = false;

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
