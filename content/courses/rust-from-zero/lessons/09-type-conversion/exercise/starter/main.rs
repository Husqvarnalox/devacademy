fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut lines = text.lines();
    let price = lines.next().unwrap().trim();
    let grams = lines.next().unwrap().trim();

    // TODO 1: превратите price из текста в число f64, оставив то же имя:
    // let price: f64 = price.parse().unwrap();

    // TODO 2: превратите grams из текста в число i32 так же.

    // TODO 3: посчитайте стоимость в рублях: цена за килограмм * граммы / 1000.0
    // Граммы нужно привести к f64 записью grams as f64
    let cost = 0.0;

    println!("Цена за кг: {price}");
    println!("Вес, г: {grams}");
    println!("Стоимость: {cost}");
    // TODO 4: целая часть стоимости: cost as i32
    println!("Целых рублей: {}", 0);
    // TODO 5: число цифр в записи веса: grams.to_string().len()
    println!("Цифр в записи веса: {}", 0);
}
