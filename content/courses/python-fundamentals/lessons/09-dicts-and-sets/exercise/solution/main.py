import sys

PUNCTUATION = ".,!?;:\"'()"

counts = {}
for raw in sys.stdin.read().split():
    word = raw.lower().strip(PUNCTUATION)
    if word:
        counts[word] = counts.get(word, 0) + 1

for word, number in sorted(counts.items(), key=lambda pair: (-pair[1], pair[0])):
    print(word, number)
