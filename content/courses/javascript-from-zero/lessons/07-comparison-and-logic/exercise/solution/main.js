const age = 17;
const hasTicket = true;
const city = "Казань";

// Замените каждое false на выражение, которое отвечает на вопрос в комментарии.
console.log(age >= 18); // Гостю не меньше 18 лет?
console.log(hasTicket && age >= 18); // Есть билет и гостю не меньше 18 лет?
console.log(hasTicket || age >= 18); // Есть билет или гостю не меньше 18 лет?
console.log(!hasTicket); // Билета нет?
console.log(age === 17); // Возраст ровно 17?
console.log(city !== "Москва"); // Город не равен "Москва"?
