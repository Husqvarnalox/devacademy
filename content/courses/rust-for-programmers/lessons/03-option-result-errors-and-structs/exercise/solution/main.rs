struct Student {
    name: String,
    score: i32,
}

impl Student {
    fn new(name: &str, score: i32) -> Student {
        Student { name: String::from(name), score }
    }

    fn passed(&self) -> bool {
        self.score >= 60
    }
}

fn parse_score(text: &str) -> Result<i32, String> {
    let trimmed = text.trim();
    let n: i32 = trimmed.parse().map_err(|_| format!("не число: {trimmed}"))?;
    if n < 0 || n > 100 {
        return Err(format!("вне диапазона: {n}"));
    }
    Ok(n)
}

fn parse_student(line: &str) -> Result<Student, String> {
    let (name_text, score_text) = line
        .split_once(';')
        .ok_or(String::from("нет разделителя"))?;
    let name = name_text.trim();
    if name.is_empty() {
        return Err(String::from("пустое имя"));
    }
    let score = parse_score(score_text)?;
    Ok(Student::new(name, score))
}

fn best(students: &Vec<Student>) -> Option<&Student> {
    let mut result: Option<&Student> = None;
    for s in students {
        match result {
            Some(current) if current.score >= s.score => {}
            _ => result = Some(s),
        }
    }
    result
}

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut students: Vec<Student> = Vec::new();
    let mut bad = 0;
    for line in input.lines() {
        if line.trim().is_empty() {
            continue;
        }
        match parse_student(line) {
            Ok(s) => students.push(s),
            Err(e) => {
                println!("пропущено: {e}");
                bad += 1;
            }
        }
    }
    let mut passed = 0;
    for s in &students {
        if s.passed() {
            passed += 1;
        }
    }
    println!("принято: {}, зачтено: {}, ошибок: {}", students.len(), passed, bad);
    match best(&students) {
        Some(s) => println!("лучший: {} ({})", s.name, s.score),
        None => println!("лучшего нет"),
    }
}
