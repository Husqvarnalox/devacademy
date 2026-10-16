<?php
declare(strict_types=1);

// Журнал хранится в файле рабочего каталога; имя с pid исключает конфликты параллельных запусков.
$file = 'journal-' . getmypid() . '.txt';

/** @return list<string> */
function entries(string $file): array
{
    // TODO: если файла нет - пустой список; иначе строки файла без переводов строк и без пустых строк
    return [];
}

function append(string $file, string $text): void
{
    // TODO: допишите $text и перевод строки в конец файла (FILE_APPEND | LOCK_EX)
}

/** @param list<string> $items */
function rewrite(string $file, array $items): void
{
    file_put_contents($file, implode('', array_map(fn (string $s): string => $s . "\n", $items)), LOCK_EX);
}

if (is_file($file)) {
    unlink($file);
}

while (($line = fgets(STDIN)) !== false) {
    $line = trim($line);
    if ($line === '') {
        continue;
    }
    $parts = preg_split('/\s+/', $line, 2);
    $cmd = strtoupper($parts[0]);
    $arg = $parts[1] ?? '';

    switch ($cmd) {
        case 'ADD':
            if ($arg === '') {
                echo "error: empty text\n";
                break;
            }
            append($file, $arg);
            echo 'added #', count(entries($file)), "\n";
            break;
        case 'LIST':
            $items = entries($file);
            if ($items === []) {
                echo "(empty)\n";
            }
            foreach ($items as $i => $text) {
                echo $i + 1, ': ', $text, ' (', mb_strlen($text), " chars)\n";
            }
            break;
        case 'DEL':
            $items = entries($file);
            if (!preg_match('/^[1-9][0-9]*$/', $arg) || (int) $arg > count($items)) {
                echo "error: no entry $arg\n";
                break;
            }
            $n = (int) $arg;
            [$removed] = array_splice($items, $n - 1, 1);
            rewrite($file, $items);
            echo "deleted #$n: $removed\n";
            break;
        case 'FIND':
            $found = 0;
            foreach (entries($file) as $i => $text) {
                if ($arg !== '' && mb_stripos($text, $arg) !== false) {
                    echo $i + 1, ': ', $text, "\n";
                    $found++;
                }
            }
            if ($found === 0) {
                echo "no matches\n";
            }
            break;
        case 'SIZE':
            echo 'bytes=', is_file($file) ? filesize($file) : 0, ' entries=', count(entries($file)), "\n";
            break;
        default:
            echo "error: unknown command $cmd\n";
    }
}

if (is_file($file)) {
    unlink($file);
}
