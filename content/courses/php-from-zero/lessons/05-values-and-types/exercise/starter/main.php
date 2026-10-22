<?php
// Исправьте значения так, чтобы у каждой переменной был нужный тип.
$count = "12";      // нужно целое число 12
$price = 99;        // нужно дробное число 99.5
$title = "Чай";     // текст - так и оставьте
$inStock = "true";  // нужно логическое значение «истина»
$discount = "";     // нужно «пусто» (null)

echo "count: ", get_debug_type($count), "\n";
echo "price: ", get_debug_type($price), "\n";
echo "title: ", get_debug_type($title), "\n";
echo "inStock: ", get_debug_type($inStock), "\n";
echo "discount: ", get_debug_type($discount), "\n";
