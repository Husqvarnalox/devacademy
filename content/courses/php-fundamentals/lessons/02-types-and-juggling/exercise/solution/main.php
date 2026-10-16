<?php
declare(strict_types=1);

/** Превращает токен в значение PHP по правилам из условия. */
function parseToken(string $token): int|float|string|bool|null
{
    if ($token === 'null') {
        return null;
    }
    if ($token === 'true') {
        return true;
    }
    if ($token === 'false') {
        return false;
    }
    if (strlen($token) >= 2 && $token[0] === '"' && str_ends_with($token, '"')) {
        return substr($token, 1, -1);   // токен в кавычках - всегда строка
    }
    if (preg_match('/^-?[0-9]+$/', $token) === 1) {
        return (int) $token;
    }
    if (is_numeric($token)) {
        return (float) $token;
    }
    return $token;
}

function show(bool $flag): string
{
    return $flag ? 'true' : 'false';
}

$n = (int) trim((string) fgets(STDIN));
for ($i = 0; $i < $n; $i++) {
    [$left, $right] = preg_split('/\s+/', trim((string) fgets(STDIN)));
    $a = parseToken($left);
    $b = parseToken($right);
    printf(
        "%s %s loose=%s strict=%s cmp=%d\n",
        get_debug_type($a),
        get_debug_type($b),
        show($a == $b),
        show($a === $b),
        $a <=> $b
    );
}
