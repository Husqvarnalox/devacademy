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

impl Pay for Hourly {
    fn monthly(&self) -> i64 {
        self.hours * self.rate
    }
    fn kind(&self) -> String {
        "hourly".to_string()
    }
}

impl Pay for Salaried {
    fn monthly(&self) -> i64 {
        self.amount
    }
}

fn total_pay<T: Pay>(items: &[T]) -> i64 {
    let mut sum = 0;
    for item in items {
        sum += item.monthly();
    }
    sum
}

fn top<T: Pay>(items: &[T]) -> Option<&T> {
    let mut best: Option<&T> = None;
    for item in items {
        match best {
            Some(current) if current.monthly() >= item.monthly() => {}
            _ => best = Some(item),
        }
    }
    best
}

fn report(item: &dyn Pay) -> String {
    format!("{}: {}", item.kind(), item.monthly())
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
