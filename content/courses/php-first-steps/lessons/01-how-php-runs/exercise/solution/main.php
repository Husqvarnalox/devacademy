<?php
$text = stream_get_contents(STDIN);
$tokens = preg_split('/\s+/', trim($text));
$n = (int) $tokens[0];
$nums = array_map('intval', array_slice($tokens, 1, $n));

$sum = array_sum($nums);
printf("sum=%d\nmin=%d\nmax=%d\navg=%.2F\n", $sum, min($nums), max($nums), $sum / count($nums));
