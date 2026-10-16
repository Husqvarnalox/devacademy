use std::collections::{BTreeSet, HashMap};
use std::io::{self, Read};

/// Ключ анаграммы: буквы слова в нижнем регистре, отсортированные.
fn key(word: &str) -> String {
    todo!("to_lowercase, в Vec<char>, sort, обратно в String")
}

/// Группы анаграмм (по два слова и более), без повторов слов.
/// Слова внутри группы - по алфавиту; группы: больше слов раньше, затем по первому слову.
fn groups(words: &[&str]) -> Vec<Vec<String>> {
    let _ = (HashMap::<String, BTreeSet<String>>::new(), words);
    todo!("HashMap<ключ, BTreeSet<слово>> через entry().or_default()")
}

fn main() {
    let mut input = String::new();
    io::stdin().read_to_string(&mut input).unwrap();
    let words: Vec<&str> = input.split_whitespace().collect();
    let gs = groups(&words);
    for g in &gs {
        println!("{}", g.join(" "));
    }
    println!("groups={}", gs.len());
}
