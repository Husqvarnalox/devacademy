import sys

counts = {}
for word in sys.stdin.read().split():
    # TODO: приведите слово к нижнему регистру и уберите знаки .,!?;:"'() по краям; пустые слова пропустите
    counts[word] = counts.get(word, 0) + 1

# TODO: выводите слова в порядке убывания частоты, при равенстве - по алфавиту
for word, number in counts.items():
    print(word, number)
