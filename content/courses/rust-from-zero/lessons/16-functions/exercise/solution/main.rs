fn discount_percent(total: i32) -> i32 {
    if total >= 5000 {
        15
    } else if total >= 2000 {
        10
    } else if total >= 1000 {
        5
    } else {
        0
    }
}

fn final_price(total: i32) -> i32 {
    let percent = discount_percent(total);
    total - total * percent / 100
}

fn free_delivery(price: i32) -> bool {
    price >= 3000
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
