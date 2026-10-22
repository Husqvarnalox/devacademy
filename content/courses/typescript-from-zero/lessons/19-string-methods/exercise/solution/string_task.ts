export function capitalize(word: string): string {
  if (word === "") {
    return "";
  }
  return word.slice(0, 1).toUpperCase() + word.slice(1).toLowerCase();
}

export function initials(fullName: string): string {
  const words: string[] = fullName.trim().split(" ").filter((w) => w !== "");
  let result: string = "";
  for (const w of words) {
    result = result + w.slice(0, 1).toUpperCase();
  }
  return result;
}

export function slugify(title: string): string {
  return title.trim().toLowerCase().replaceAll(" ", "-");
}

export function countWords(text: string): number {
  return text.trim().split(" ").filter((w) => w !== "").length;
}
