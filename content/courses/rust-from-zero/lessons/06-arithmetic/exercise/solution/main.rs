fn main() {
    let price = 235;
    let paid = 500;
    let total_minutes = 135;
    let mut bonus = 10;

    let change = paid - price;
    let hours = total_minutes / 60;
    let minutes = total_minutes % 60;
    let three_items = price * 3;
    bonus += 5;
    bonus *= 2;

    println!("Сдача: {change}");
    println!("Длительность: {hours} ч {minutes} мин");
    println!("Три товара: {three_items} руб.");
    println!("Бонусных баллов: {bonus}");
}
