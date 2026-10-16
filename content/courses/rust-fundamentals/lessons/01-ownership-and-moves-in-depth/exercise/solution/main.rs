use std::io::Read;

struct Guard(String);

impl Drop for Guard {
    fn drop(&mut self) {
        println!("drop {}", self.0);
    }
}

fn inspect(g: &Guard) -> usize {
    println!("inspect {}", g.0);
    g.0.chars().count()
}

fn consume(g: Guard) -> usize {
    let n = g.0.chars().count();
    println!("consume {}", g.0);
    n
}

fn main() {
    let mut input = String::new();
    std::io::stdin().read_to_string(&mut input).unwrap();
    let mut it = input.split_whitespace();
    let n: usize = it.next().unwrap().parse().unwrap();
    let mut guards: Vec<Guard> = it.take(n).map(|s| Guard(s.to_string())).collect();

    let last = guards.pop().unwrap();
    let first = guards.remove(0);

    let seen = inspect(&first);
    let taken = consume(first);

    println!("seen={} taken={}", seen, taken);
    println!("last={}", last.0);
    println!("rest={}", guards.len());
}
