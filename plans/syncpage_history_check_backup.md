# План: История изменений, проверки, SyncPageWindow

## Phase 1 — База данных и модель

### 1.1 ALTER TABLE `[Копия EWSs]`
Добавить недостающие колонки из новой схемы EWSs:
- `EWS_FireUnit_COD`, `EWS_Status_COD`, `EWS_AdressObject_COD`, `EWS_HouseNumber`
- `EWS_AdressNote`, `EWS_PipeType_COD`, `EWS_PKDiameter_COD`, `EWS_Value_COD`
- `EWS_PrLeft`, `EWS_PrRight`, `EWS_PrStright`, `EWS_Priviazka_GeoX`, `EWS_Priviazka_GeoY`
- `Record_User_COD`, `EWS_Type_COD`

Плюс две новые колонки:
- `ChangeDate` (TEXT)
- `ChangeDescription` (TEXT)

### 1.2 Обновить `КопияEwss.cs`
Добавить C#-свойства для всех недостающих колонок + `ChangeDate`, `ChangeDescription`

### 1.3 Метод `InsertEwssHistory(Ewss oldEwss, string changeDescription)`
- Читает старые значения из `Ewss`
- Вставляет копию в `[Копия EWSs]` с `ChangeDate=DateTime.Now`
- Вызывать ДО `UpdateEwss`

### 1.4 Метод `InsertEwssCheck(Ewss ewss)`
```sql
INSERT INTO EWSs_Check (Check_ID, Check_Date, Check_EWS_COD,
    Check_CheckType_COD, Check_Staff_COD, Check_Status_COD,
    Record_Created, Record_User_COD)
VALUES (@id, @now, @ewsId, '1', @user, @status, @now, @user)
```
- Всегда вызывается при сохранении через `AddMarkerWindow`

## Phase 2 — AddMarkerWindow + MainWindow

### 2.1 AddMarkerWindow.xaml
Добавить поле "Причина изменения" (`ChangeReasonTextBox`) после "Примечание"

### 2.2 AddMarkerWindow.xaml.cs
- Публичное свойство `ChangeReason`

### 2.3 MainWindow.xaml.cs — EditMarkerInfo()
После `ShowDialog()`:
1. `_ewsService.InsertEwssHistory(existing, editWindow.ChangeReason)`
2. `_ewsService.InsertEwssCheck(updated)`
3. `_ewsService.UpdateEwss(updated)`

**CompleteMarkerMove**: НЕ копировать

## Phase 3 — SyncPageWindow (полная переработка)

### 3.1 SyncPageWindow.xaml
TabControl с двумя вкладками + секция резервного копирования:
- **Вкладка 1 "История изменений"**: DataGrid (КопияEWSS) + фильтр №ПГ, дата от/до
- **Вкладка 2 "История проверок"**: DataGrid (EWSs_Check) + фильтр №ПГ, дата от/до
- **Секция бэкапа**: [Создать резервную копию] [Восстановить из резервной копии]

### 3.2 Методы пагинации
- `GetCopyEwssPaged(offset, limit, searchPg?, dateFrom?, dateTo?)`
- `GetEwssChecksPaged(offset, limit, searchPg?, dateFrom?, dateTo?)`

### 3.3 SyncPageWindow.xaml.cs
- Первые 50 строк при загрузке, подгрузка по скроллу
- Debounce-фильтрация (300ms)
- BackupService для кнопок бэкапа

## Файлы для изменения (7 файлов)

| Файл | Изменения |
|------|-----------|
| `Models\КопияEwss.cs` | +15 новых свойств + `ChangeDate`, `ChangeDescription` |
| `Helpers\EwsMapDataService.cs` | +5 новых методов + ALTER TABLE логика |
| `UserInput\AddMarkerWindow.xaml` | + поле "Причина изменения" |
| `UserInput\AddMarkerWindow.xaml.cs` | + свойство `ChangeReason` |
| `MainWindow.xaml.cs` | `EditMarkerInfo`: + InsertEwssHistory + InsertEwssCheck |
| `SyncPage\SyncPageWindow.xaml` | Полная замена |
| `SyncPage\SyncPageWindow.xaml.cs` | Полная замена |

## Что НЕ меняется
- `MainWindow.xaml` (кнопки бэкапа остаются)
- `BindingEditDialog`, `BindingMarker`, `HydrantContextMenu`
- `BackupService`
- `CompleteMarkerMove` в MainWindow
