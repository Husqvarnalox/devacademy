<?php
declare(strict_types=1);

/** @var array<string, string> $data */
$data = [];
/** @var list<array<string, string>> $stack снимки состояния на момент каждого BEGIN */
$stack = [];

while (($line = fgets(STDIN)) !== false) {
    $parts = preg_split('/\s+/', trim($line), -1, PREG_SPLIT_NO_EMPTY);
    if ($parts === [] ) {
        continue;
    }
    $cmd = strtoupper($parts[0]);

    switch ($cmd) {
        case 'SET':
            $data[$parts[1]] = $parts[2];
            break;
        case 'GET':
            echo $data[$parts[1]] ?? 'NULL', "\n";
            break;
        case 'DEL':
            unset($data[$parts[1]]);
            break;
        case 'COUNT':
            echo count(array_keys($data, $parts[1], true)), "\n";
            break;
        case 'DUMP':
            $copy = $data;
            ksort($copy, SORT_STRING);
            $pairs = [];
            foreach ($copy as $k => $v) {
                $pairs[] = "$k=$v";
            }
            echo '{', implode(', ', $pairs), "}\n";
            break;
        case 'BEGIN':
            $stack[] = $data;          // массив - значение: снимок независим
            break;
        case 'ROLLBACK':
            if ($stack === []) {
                echo "NO TRANSACTION\n";
            } else {
                $data = array_pop($stack);
            }
            break;
        case 'COMMIT':
            if ($stack === []) {
                echo "NO TRANSACTION\n";
            } else {
                array_pop($stack);     // изменения остаются, снимок больше не нужен
            }
            break;
    }
}
