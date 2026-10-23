struct Student {
    name: String,
    score: i32,
}

impl Student {
    fn new(name: &str, score: i32) -> Student {
        // TODO: создайте студента с этим именем и баллом
        Student { name: String::new(), score: 0 }
    }

    fn passed(&self) -> bool {
        // TODO: верните true, если балл не меньше 60
        false
    }
}

fn parse_score(text: &str) -> Result<i32, String> {
    // TODO: уберите пробелы по краям и разберите целое число.
    // Не число - Err("не число: <текст>"), вне 0..=100 - Err("вне диапазона: <число>")
    Err(String::from("не реализовано"))
}

fn parse_student(line: &str) -> Result<Student, String> {
    // TODO: разделите строку "имя;балл" по первой точке с запятой.
    // Нет ';' - Err("нет разделителя"), пустое имя после trim - Err("пустое имя"),
    // ошибку балла передайте дальше оператором ?
    Err(String::from("не реализовано"))
}

fn best(students: &Vec<Student>) -> Option<&Student> {
    // TODO: верните студента с наибольшим баллом (при равенстве - первого) или None
    todo!()
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
