fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut lines = text.lines();
    let name = lines.next().unwrap().trim();
    let price: f64 = lines.next().unwrap().trim().parse().unwrap();
    let qty: i32 = lines.next().unwrap().trim().parse().unwrap();

    let total = price * qty as f64;

    // TODO: сделайте так, чтобы вывод точно совпал с образцом из условия.
    // Заголовок: слово " Чек " по центру, ширина 18, заполнитель =  ->  {:=^18}
    println!("Чек");
    // Подписи слева, поле шириной 8: {:<8}. Значения справа, поле шириной 10: {:>10}.
    // Для цены и суммы нужны два знака после запятой: {:>10.2}
    println!("Товар: {}", name);
    println!("Цена: {}", price);
    println!("Кол-во: {}", qty);
    println!("Сумма: {}", total);
    println!("==================");
}
