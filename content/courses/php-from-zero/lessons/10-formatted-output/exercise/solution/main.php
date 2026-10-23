<?php
$price = (float) trim(fgets(STDIN));
$count = (int) trim(fgets(STDIN));
$total = $price * $count;
printf("Цена:       %8.2F\n", $price);
printf("Количество: %8d\n", $count);
printf("Итого:      %8.2F\n", $total);
