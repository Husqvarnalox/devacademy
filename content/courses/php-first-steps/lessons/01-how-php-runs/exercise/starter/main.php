<?php
$text = stream_get_contents(STDIN);
$tokens = preg_split('/\s+/', trim($text));
$n = (int) $tokens[0];
// TODO: превратите следующие $n токенов в числа
$nums = [];

// TODO: вычислите сумму, минимум, максимум и среднее
printf("sum=%d\nmin=%d\nmax=%d\navg=%.2F\n", 0, 0, 0, 0);
