# Соглашения об именовании

1. **C# классы:** PascalCase, без венгерской нотации.
   - Пример: `EwsMapDataService`, `BindingEditDialog`, `SyncPageWindow`

2. **Поля и свойства:** CamelCase для public свойств, `_camelCase` для private полей.
   - Пример: `EwsStatusCod`, `_ewsService`, `_selectedEwss`

3. **Методы:** PascalCase (глагол + существительное).
   - Пример: `GetAllEwss()`, `InsertEwssHistory()`, `UpdateEwssBinding()`

4. **Файлы:** Имя файла = имя класса.
   - Пример: `Ewss.cs` → class `Ewss`, `BackupService.cs` → class `BackupService`

5. **Таблицы БД:** Имена в UPPER_SNAKE_CASE или свободные (с пробелами).
   - Пример: `EWSs`, `EWSs_Check`, `Копия EWSs`

6. **SQL-параметры:** Префикс `@`, snake_case.
   - Пример: `@ewsId`, `@changeDate`, `@status`

7. **XAML-элементы:** PascalCase + суффикс типа.
   - Пример: `ChangeReasonTextBox`, `StatusComboBox`, `SaveButton`

8. **Russian в коде:** Допускается только в моделях (поля БД с русскими названиями) и UI-строках. В именах классов/методов/переменных — только английский.
