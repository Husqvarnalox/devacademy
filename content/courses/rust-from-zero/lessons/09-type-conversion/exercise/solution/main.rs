fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut lines = text.lines();
    let price = lines.next().unwrap().trim();
    let grams = lines.next().unwrap().trim();

    // Затенение: то же имя, но теперь это число, а не текст.
    let price: f64 = price.parse().unwrap();
    let grams: i32 = grams.parse().unwrap();

    let cost = price * grams as f64 / 1000.0;

    println!("Цена за кг: {price}");
    println!("Вес, г: {grams}");
    println!("Стоимость: {cost}");
    println!("Целых рублей: {}", cost as i32);
    println!("Цифр в записи веса: {}", grams.to_string().len());
}
