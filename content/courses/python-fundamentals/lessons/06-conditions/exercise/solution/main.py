age = int(input())
day = input()

if age < 0 or age > 120:
    print("Некорректный возраст")
elif age <= 5:
    print("Бесплатно")
else:
    if age <= 17:
        price = 200
    elif age <= 64:
        price = 350
    else:
        price = 150
    if day == "суббота" or day == "воскресенье":
        price = price + 50
    print(f"Цена: {price} руб.")
