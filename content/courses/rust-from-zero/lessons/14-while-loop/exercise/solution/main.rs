fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut lines = text.lines();
    let goal: i32 = lines.next().unwrap().trim().parse().unwrap();
    let deposit: i32 = lines.next().unwrap().trim().parse().unwrap();

    let mut months = 0;
    let mut saved = 0;
    while saved < goal {
        months += 1;
        saved += deposit;
        if months % 6 == 0 {
            saved += 100;
        }
    }

    println!("Месяцев: {months}");
    println!("Накоплено: {saved}");
}
