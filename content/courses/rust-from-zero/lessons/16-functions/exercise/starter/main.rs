fn discount_percent(total: i32) -> i32 {
    // TODO: верните процент скидки по сумме покупки
    0
}

fn final_price(total: i32) -> i32 {
    // TODO: верните сумму к оплате (используйте discount_percent)
    total
}

fn free_delivery(price: i32) -> bool {
    // TODO: верните true, если сумма к оплате не меньше 3000
    false
}

fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let total: i32 = text.trim().parse().unwrap();

    let percent = discount_percent(total);
    let price = final_price(total);
    println!("Скидка: {percent}%");
    println!("К оплате: {price}");
    if free_delivery(price) {
        println!("Доставка: бесплатно");
    } else {
        println!("Доставка: платно");
    }
}
