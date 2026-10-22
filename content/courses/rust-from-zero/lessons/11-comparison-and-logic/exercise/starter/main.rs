fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut lines = text.lines();
    let age: i32 = lines.next().unwrap().trim().parse().unwrap();
    let height: i32 = lines.next().unwrap().trim().parse().unwrap();
    let permission = lines.next().unwrap().trim();

    // TODO: замените false на выражения, которые дают true или false.
    // Взрослый: возраст не меньше 18
    let adult = false;
    // Школьник: возраст от 7 до 17 включительно (используйте &&)
    let schoolchild = false;
    // Рост подходит: рост не меньше 120
    let tall_enough = false;
    // Разрешение родителей есть: permission равно "да"
    let has_permission = false;
    // Допуск на горку: рост подходит И (возраст не меньше 12 ИЛИ есть разрешение)
    let allowed = false;
    // Отказ: допуска нет (используйте !)
    let denied = false;

    println!("Взрослый: {adult}");
    println!("Школьник: {schoolchild}");
    println!("Рост подходит: {tall_enough}");
    println!("Разрешение родителей: {has_permission}");
    println!("Допуск на горку: {allowed}");
    println!("Отказ: {denied}");
}
