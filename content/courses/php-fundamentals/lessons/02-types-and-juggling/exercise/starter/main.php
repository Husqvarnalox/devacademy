<?php
declare(strict_types=1);

/**
 * Превращает токен в значение PHP по правилам из условия.
 * TODO: сейчас всё считается строкой.
 */
function parseToken(string $token): int|float|string|bool|null
{
    return $token;
}

/** TODO: должна возвращать "true" или "false". */
function show(bool $flag): string
{
    return '';
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
