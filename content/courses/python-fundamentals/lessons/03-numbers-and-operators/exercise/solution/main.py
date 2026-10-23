eggs = int(input())
size = int(input())
price = int(input())

boxes = eggs // size
leftover = eggs % size
cost = boxes * price
fraction = eggs / size

print("Полных коробок:", boxes)
print("Осталось яиц:", leftover)
print("Стоимость:", cost)
print("Коробок (дробно):", fraction)
