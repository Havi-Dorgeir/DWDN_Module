# Daewden (DWDN) Multiplayer Module
### Модуль фракции Даевден для Mount & Blade II: Bannerlord

Репозиторий содержит исходные коды, конфигурации XML, интерфейсы Gauntlet и ресурсы сетевого модуля **Daewden (DWDN)**.

---

## 📖 Документация и руководства

Подробное руководство по самостоятельному моддингу находится в файле **[DOCUMENTATION.md](DOCUMENTATION.md)**:
* **Архитектурная карта мода**: подробное описание назначения всех папок и файлов.
* **Пошаговое руководство: Как добавить новый класс самому**: сквозной туториал от XML (`mpcharacters.xml`, `mpclassdivisions.xml`) до C# регистрации (`DWDN_DuchyManager.cs`).
* **Пошаговое руководство: Как добавить классу новую вещь в кастомном UI**: добавление шлемов, доспехов, оружия и щитов в меню выбора экипировки.
* **Сетевая синхронизация**: описание работы клиент-серверной архитектуры спавна и применения лодаутов.
* **Решение типовых проблем**: таблица частых ошибок и способов их исправления.

---

## 🛠 Быстрый старт: Сборка исходного кода

Проект C# расположен по пути:
`ModuleData/scripts/DWDN_Script/DWDN_Script.csproj` (Target Framework: `.NET Framework 4.7.2`).

Сборка через .NET CLI:
```powershell
# Клиентская библиотека (DWDN_Script.dll)
dotnet build ModuleData/scripts/DWDN_Script/DWDN_Script.csproj -c Release

# Серверная библиотека для Dedicated Server (DWDN_Script.Server.dll)
dotnet build ModuleData/scripts/DWDN_Script/DWDN_Script.csproj -c Server
```

---

## 📂 Структура репозитория

* `ModuleData/` — XML-манифесты классов (`mpclassdivisions.xml`), персонажей (`mpcharacters.xml`), предметов (`mpitems/`), кастомного оружия (`crafting_pieces.xml`, `crafting_templates.xml`).
* `ModuleData/scripts/DWDN_Script/` — Исходный C#-код мода (клиентский UI на Gauntlet, сетевые сообщения, серверные подсистемы экипировки).
* `GUI/` — XML-разметка и ресурсы кастомного окна выбора классов и лодаутов.
* `Assets/` — Материалы, текстуры и параметры ассетов юнитов.
* `SubModule.xml` — Главный манифест мода для загрузчика TaleWorlds.

---
*Разработано для сообщества Mount & Blade II Bannerlord.*
