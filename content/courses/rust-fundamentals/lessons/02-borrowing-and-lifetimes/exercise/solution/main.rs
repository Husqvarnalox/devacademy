use std::io::Read;

struct Words<'a> {
    items: Vec<&'a str>,
}

fn pick<'a>(a: &'a str, b: &'a str) -> &'a str {
    if b.chars().count() > a.chars().count() { b } else { a }
}

impl<'a> Words<'a> {
    fn new(text: &'a str) -> Self {
        Words { items: text.split_whitespace().collect() }
    }

    fn first(&self) -> &'a str {
        self.items[0]
    }

    fn last(&self) -> &'a str {
        self.items[self.items.len() - 1]
    }

    fn longest(&self) -> &'a str {
        let mut best = self.items[0];
        for w in &self.items {
            best = pick(best, w);
        }
        best
    }
}

fn main() {
    let mut text = String::new();
    std::io::stdin().read_to_string(&mut text).unwrap();
    let words = Words::new(&text);

    println!("count={}", words.items.len());
    println!("first={}", words.first());
    println!("last={}", words.last());
    println!("longest={}", words.longest());
}
