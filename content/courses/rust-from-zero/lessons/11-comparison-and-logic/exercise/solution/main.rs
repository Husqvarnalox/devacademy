fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut lines = text.lines();
    let age: i32 = lines.next().unwrap().trim().parse().unwrap();
    let height: i32 = lines.next().unwrap().trim().parse().unwrap();
    let permission = lines.next().unwrap().trim();

    let adult = age >= 18;
    let schoolchild = age >= 7 && age <= 17;
    let tall_enough = height >= 120;
    let has_permission = permission == "да";
    let allowed = tall_enough && (age >= 12 || has_permission);
    let denied = !allowed;

    println!("Взрослый: {adult}");
    println!("Школьник: {schoolchild}");
    println!("Рост подходит: {tall_enough}");
    println!("Разрешение родителей: {has_permission}");
    println!("Допуск на горку: {allowed}");
    println!("Отказ: {denied}");
}
