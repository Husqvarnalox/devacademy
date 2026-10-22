trait Pay {
    fn monthly(&self) -> i64;
    fn kind(&self) -> String {
        "staff".to_string()
    }
}

struct Hourly {
    hours: i64,
    rate: i64,
}

struct Salaried {
    amount: i64,
}

// Hourly: monthly = hours * rate, kind = "hourly".
impl Pay for Hourly {
    fn monthly(&self) -> i64 {
        // TODO
        0
    }
}

// Salaried: monthly = amount; kind не переопределяйте.
impl Pay for Salaried {
    fn monthly(&self) -> i64 {
        // TODO
        0
    }
}

// Сумма monthly всех элементов.
fn total_pay<T: Pay>(items: &[T]) -> i64 {
    // TODO
    0
}

// Элемент с наибольшей monthly (при равенстве - первый) или None.
fn top<T: Pay>(items: &[T]) -> Option<&T> {
    // TODO
    None
}

// Строка "kind: monthly".
fn report(item: &dyn Pay) -> String {
    // TODO
    String::new()
}

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut hourly: Vec<Hourly> = Vec::new();
    let mut salaried: Vec<Salaried> = Vec::new();
    let mut all: Vec<Box<dyn Pay>> = Vec::new();
    for line in input.lines() {
        let words: Vec<&str> = line.split_whitespace().collect();
        match words.as_slice() {
            ["h", hours, rate] => {
                let hours: i64 = hours.parse().unwrap();
                let rate: i64 = rate.parse().unwrap();
                hourly.push(Hourly { hours, rate });
                all.push(Box::new(Hourly { hours, rate }));
            }
            ["s", amount] => {
                let amount: i64 = amount.parse().unwrap();
                salaried.push(Salaried { amount });
                all.push(Box::new(Salaried { amount }));
            }
            _ => {}
        }
    }
    println!("hourly total={}", total_pay(&hourly));
    println!("salaried total={}", total_pay(&salaried));
    match top(&hourly) {
        Some(item) => println!("top hourly: {}", report(item)),
        None => println!("top hourly: none"),
    }
    match top(&salaried) {
        Some(item) => println!("top salaried: {}", report(item)),
        None => println!("top salaried: none"),
    }
    for item in &all {
        println!("{}", report(item.as_ref()));
    }
}
