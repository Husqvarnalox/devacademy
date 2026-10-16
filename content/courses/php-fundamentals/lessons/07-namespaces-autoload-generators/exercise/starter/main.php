<?php
declare(strict_types=1);

/** Приводит префикс к виду «Vendor\Pkg\» (пустой префикс - корневой). */
function normalizePrefix(string $prefix): string
{
    $prefix = trim($prefix, '\\');
    return $prefix === '' ? '' : $prefix . '\\';
}

/**
 * Лениво отдаёт пути-кандидаты для класса по правилам PSR-4:
 * сначала самый длинный подходящий префикс, при равной длине - порядок объявления,
 * внутри префикса - порядок каталогов.
 *
 * @param list<array{0: string, 1: string}> $maps пары [префикс, каталог]
 * @return Generator<int, string>
 */
function candidates(array $maps, string $class): Generator
{
    // TODO: 1) отсортируйте $maps по длине префикса по убыванию (usort стабилен);
    //       2) для каждого префикса, с которого начинается $class (str_starts_with),
    //          отдайте через yield путь: каталог без хвостового '/' + '/' + остаток имени
    //          с '\\' заменёнными на '/' + '.php'
    yield from [];
}

$maps = [];
$files = [];
$loaded = [];

while (($line = fgets(STDIN)) !== false) {
    $line = trim($line);
    if ($line === '') {
        continue;
    }
    $words = preg_split('/\s+/', $line);
    $cmd = strtolower($words[0]);
    $argc = count($words) - 1;

    if ($cmd === 'map' && $argc === 2) {
        $maps[] = [normalizePrefix($words[1]), $words[2]];
    } elseif ($cmd === 'file' && $argc === 1) {
        $files[$words[1]] = true;
    } elseif ($cmd === 'load' && $argc === 1) {
        $class = ltrim($words[1], '\\');
        if (isset($loaded[$class])) {
            echo "$class -> cached (checked 0)\n";
            continue;
        }
        $checked = 0;
        $found = null;
        foreach (candidates($maps, $class) as $path) {
            $checked++;
            if (isset($files[$path])) {
                $found = $path;
                break;
            }
        }
        if ($found !== null) {
            $loaded[$class] = true;
        }
        echo $class, ' -> ', $found ?? 'not found', " (checked $checked)\n";
    } else {
        echo "invalid: $line\n";
    }
}
