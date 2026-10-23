fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut lines = text.lines();
    let goal: i32 = lines.next().unwrap().trim().parse().unwrap();
    let deposit: i32 = lines.next().unwrap().trim().parse().unwrap();

    let months = 0;
    let saved = 0;
    // TODO: сделайте months и saved изменяемыми и допишите цикл while.
    let _ = (goal, deposit);

    println!("Месяцев: {months}");
    println!("Накоплено: {saved}");
}
