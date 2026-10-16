# Таблица employees(id, name, dept, salary, manager_id).
# dept, salary и manager_id могут быть NULL.
# Замените заглушки SQL-запросами. Каждая константа - ОДИН запрос SELECT.

# Два сотрудника с наименьшей известной зарплатой: столбцы name, salary.
# Порядок: salary по возрастанию, затем name по возрастанию.
Q_LOWEST_PAID = "SELECT 1"

# Имена сотрудников без руководителя (manager_id не задан), по алфавиту.
Q_NO_MANAGER = "SELECT 1"

# Вторая страница списка при размере страницы 2: столбцы id, name; порядок по id.
Q_PAGE2 = "SELECT 1"

# id сотрудников, чей отдел НЕ 'Sales'. Сотрудники с неизвестным отделом (NULL)
# тоже подходят. Порядок по id. Столбец один: id.
Q_NOT_SALES = "SELECT 1"

# Столбцы name и annual, где annual = salary * 12, а неизвестная зарплата считается 0.
# Порядок по name.
Q_ANNUAL = "SELECT 1"

# Различные известные отделы (без NULL) по алфавиту. Столбец один: dept.
Q_DEPTS = "SELECT 1"
