fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut lines = text.lines();
    let name = lines.next().unwrap().trim();
    let city = lines.next().unwrap().trim();
    let parcel = lines.next().unwrap().trim();
    println!("Получатель: {name}");
    println!("Город: {}", city.to_uppercase());
    println!("Посылка: {parcel}");
    println!("Букв в описании: {}", parcel.chars().count());
}
