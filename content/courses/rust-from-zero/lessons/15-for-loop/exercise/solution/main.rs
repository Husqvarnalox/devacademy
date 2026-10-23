fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut count = 0;
    let mut total = 0;
    let mut biggest = 0;

    for line in text.lines() {
        if line.trim() == "" {
            continue;
        }
        let price: i32 = line.trim().parse().unwrap();
        count += 1;
        total += price;
        if count == 1 || price > biggest {
            biggest = price;
        }
        println!("{count}. {price}");
    }

    println!("Позиций: {count}");
    println!("Итого: {total}");
    if count == 0 {
        println!("Самая дорогая: нет");
    } else {
        println!("Самая дорогая: {biggest}");
    }
}
