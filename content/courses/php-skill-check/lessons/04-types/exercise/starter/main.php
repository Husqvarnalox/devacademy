<?php
declare(strict_types=1);

// Допишите три функции. Код ниже читает команды и вызывает их - менять его не нужно.

/**
 * Превращает текстовый токен в значение:
 *   "null"            -> null
 *   "true" / "false"  -> bool
 *   целое число       -> int    (проверка filter_var с FILTER_VALIDATE_INT)
 *   иначе число       -> float  (проверка filter_var с FILTER_VALIDATE_FLOAT)
 *   всё остальное     -> string (как есть)
 */
function parseToken(string $token): int|float|bool|string|null
{
    // TODO
    return $token;
}

/**
 * Название типа значения: "null", "bool", "int", "float" или "string".
 */
function typeName(mixed $value): string
{
    // TODO
    return "";
}

/**
 * Сравнение двух значений:
 *   "same"      - значения идентичны (===);
 *   "equal"     - нестрого равны (==), но не идентичны;
 *   "different" - не равны даже нестрого.
 */
function compareValues(mixed $a, mixed $b): string
{
    // TODO
    return "";
}

while (($line = fgets(STDIN)) !== false) {
    $parts = explode(" ", trim($line));
    if ($parts[0] === "parse" && count($parts) === 2) {
        $value = parseToken($parts[1]);
        echo typeName($value), " ", var_export($value, true), "\n";
    } elseif ($parts[0] === "cmp" && count($parts) === 3) {
        echo compareValues(parseToken($parts[1]), parseToken($parts[2])), "\n";
    }
}
