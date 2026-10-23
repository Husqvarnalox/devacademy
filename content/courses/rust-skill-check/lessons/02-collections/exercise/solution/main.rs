use std::collections::HashMap;

fn split_pair(line: &str) -> Option<(String, i32)> {
    let (name_text, value_text) = line.split_once(';')?;
    let name = name_text.trim();
    if name.is_empty() {
        return None;
    }
    let value: i32 = value_text.trim().parse().ok()?;
    Some((String::from(name), value))
}

fn totals_by_category(categories: &Vec<String>, amounts: &Vec<i32>) -> HashMap<String, i32> {
    let mut totals = HashMap::new();
    for i in 0..categories.len() {
        *totals.entry(categories[i].clone()).or_insert(0) += amounts[i];
    }
    totals
}

fn biggest(totals: &HashMap<String, i32>) -> String {
    let mut names: Vec<&String> = totals.keys().collect();
    names.sort();
    let mut best: Option<&String> = None;
    for name in names {
        match best {
            Some(current) if totals[current] >= totals[name] => {}
            _ => best = Some(name),
        }
    }
    match best {
        Some(name) => name.clone(),
        None => String::from("-"),
    }
}

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut categories: Vec<String> = Vec::new();
    let mut amounts: Vec<i32> = Vec::new();
    let mut skipped = 0;
    for line in input.lines() {
        match split_pair(line) {
            Some((name, value)) => {
                categories.push(name);
                amounts.push(value);
            }
            None => skipped += 1,
        }
    }
    let totals = totals_by_category(&categories, &amounts);
    let mut names: Vec<&String> = totals.keys().collect();
    names.sort();
    for name in names {
        println!("{}: {}", name, totals[name]);
    }
    println!("top: {}", biggest(&totals));
    println!("skipped: {skipped}");
}
