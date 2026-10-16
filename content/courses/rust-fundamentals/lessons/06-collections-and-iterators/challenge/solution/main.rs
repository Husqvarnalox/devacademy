use std::collections::{BTreeSet, HashMap};
use std::io::{self, Read};

/// Ключ анаграммы: буквы слова в нижнем регистре, отсортированные.
fn key(word: &str) -> String {
    let mut chars: Vec<char> = word.chars().flat_map(|c| c.to_lowercase()).collect();
    chars.sort_unstable();
    chars.into_iter().collect()
}

/// Группы анаграмм (по два слова и более), без повторов слов.
fn groups(words: &[&str]) -> Vec<Vec<String>> {
    let mut map: HashMap<String, BTreeSet<String>> = HashMap::new();
    for w in words {
        map.entry(key(w)).or_default().insert(w.to_lowercase());
    }
    let mut out: Vec<Vec<String>> = map
        .into_values()
        .filter(|g| g.len() >= 2)
        .map(|g| g.into_iter().collect())
        .collect();
    out.sort_by(|a, b| b.len().cmp(&a.len()).then(a[0].cmp(&b[0])));
    out
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
