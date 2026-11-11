# Daewden (DWDN) Multiplayer Module
### Модуль фракции Даевден для Mount & Blade II: Bannerlord

Репозиторий содержит исходные коды, конфигурации XML, интерфейсы Gauntlet и ресурсы сетевого модуля **Daewden (DWDN)**.

---

## 📖 Документация и руководства

В репозитории представлена полная техническая и архитектурная документация модуля и движка игры:

1. **[DOCUMENTATION.md](DOCUMENTATION.md)** — практическое руководство по моддингу DWDN:
   * **Архитектурная карта мода**: подробное описание назначения всех папок и файлов.
   * **Пошаговое руководство**: как добавить новый класс и экипировку от XML (`mpcharacters.xml`, `mpclassdivisions.xml`) до C# регистрации (`DWDN_DuchyManager.cs`).
   * **Сетевая синхронизация**: описание работы клиент-серверной архитектуры спавна и применения лодаутов.
   * **Тактическая система Ауры Знаменосца (`DWDN_BannerAuraBehavior.cs`)**: описание балансных бонусов строя в радиусе 12 м, личной защиты Знаменосца и механики «Второго дыхания».
   * **Комплекс визуальных GUI-студий моддинга (`Tools/`)**: описание работы `WeaponCalibrator`, `DuchyLoadoutStudio` и `ModValidator`.
2. **[AI_MODDING_GUIDE.md](AI_MODDING_GUIDE.md)** — инженерный кодекс и технический справочник по внутренним системам DWDN (`AgentDrivenProperties`, `CraftingPiece` Z-оффсеты, кастомный Gauntlet UI, защита от крашей `0xC0000005`).
3. **[BANNERLORD_COMPLETE_REVERSE_ENGINEERING.md](BANNERLORD_COMPLETE_REVERSE_ENGINEERING.md)** — полная энциклопедия реверс-инжиниринга ядра *Mount & Blade II: Bannerlord (v1.4.8)* (`Native`, `SandBoxCore`, `SandBox`, `Multiplayer`): C++/C# мост `DotNetObject`, двойной пайплайн XML (`project.mbproj` vs `MBObjectManager`), бинарный формат `.tpac` (`LZ4` + `METM`) и `.rdc`, лимит 64 костей скелета и сетевой протокол.
4. **[notes.md](notes.md)** — полная хроника разработки, архитектурных решений, балансных правок и исправлений модуля.

---

## 🧰 Автономные визуальные инструменты моддинга (`Tools/`)

* **`Tools/WeaponCalibrator/`** — **Авто-Калибратор 3D Оружия**: парсит `.tpac` и `.fbx`, вычисляет точные габариты и стыки (`previous_piece_offset` / `next_piece_offset`), отображает 3D-вьюпорт и автоматически обновляет `crafting_pieces.xml`.
* **`Tools/DuchyLoadoutStudio/`** — **Визуальный Конструктор Герцогств и Лодаутов**: интерактивный редактор классов с 3D-манекеном и кнопкой мгновенного применения в мод (`C# + XML + сборка клиентской и серверной DLL`).
* **`Tools/ModValidator/`** — **Мгновенный Предпусковой Валидатор Мода**: за 0.25 сек проверяет 8 уровней целостности XML, C#, 3D-мешей в `.tpac` и `SHA-256` синхронизацию с выделенным сервером `DaewdenM`.

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
