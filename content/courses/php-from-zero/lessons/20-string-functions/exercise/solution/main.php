<?php
declare(strict_types=1);

$line = trim(fgets(STDIN));

$words = [];
foreach (explode(" ", $line) as $part) {
    if ($part === "") {
        continue;
    }
    $words[] = $part;
}

$result = mb_ucfirst($words[0]);
for ($i = 1; $i < count($words); $i++) {
    $result .= " " . mb_strtoupper(mb_substr($words[$i], 0, 1)) . ".";
}

echo $result, "\n";
echo "Слов: ", count($words), "\n";
