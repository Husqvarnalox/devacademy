<?php
declare(strict_types=1);

$accepted = 0;
$skipped = 0;
$sum = 0;

// TODO: читайте строки, пока fgets не вернёт false.
// Пустые строки (после trim) пропускайте, не считая.
// Строки, которые не являются целым числом, считайте в $skipped.
// Остальные считайте в $accepted и прибавляйте к $sum.

echo "Принято: ", $accepted, "\n";
echo "Пропущено: ", $skipped, "\n";
echo "Сумма: ", $sum, "\n";
