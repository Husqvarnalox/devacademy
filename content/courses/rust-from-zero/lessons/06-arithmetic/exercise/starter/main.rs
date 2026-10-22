fn main() {
    let price = 235;
    let paid = 500;
    let total_minutes = 135;
    let mut bonus = 10;

    // TODO: сдача - сколько остаётся после оплаты (paid минус price)
    let change = 0;
    // TODO: часы - целая часть от деления total_minutes на 60
    let hours = 0;
    // TODO: минуты - остаток от деления total_minutes на 60
    let minutes = 0;
    // TODO: стоимость трёх таких товаров
    let three_items = 0;
    // TODO: прибавьте к bonus число 5, а затем умножьте bonus на 2 (операции += и *=)

    println!("Сдача: {change}");
    println!("Длительность: {hours} ч {minutes} мин");
    println!("Три товара: {three_items} руб.");
    println!("Бонусных баллов: {bonus}");
}
