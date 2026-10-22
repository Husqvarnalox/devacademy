fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut lines = text.lines();
    let name = lines.next().unwrap().trim();
    let price: f64 = lines.next().unwrap().trim().parse().unwrap();
    let qty: i32 = lines.next().unwrap().trim().parse().unwrap();

    let total = price * qty as f64;

    println!("{:=^18}", " Чек ");
    println!("{:<8}{:>10}", "Товар:", name);
    println!("{:<8}{:>10.2}", "Цена:", price);
    println!("{:<8}{:>10}", "Кол-во:", qty);
    println!("{:<8}{:>10.2}", "Сумма:", total);
    println!("==================");
}
