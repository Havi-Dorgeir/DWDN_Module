# Daewden (DWDN) Multiplayer Module
### Модуль фракции Даевден для Mount & Blade II: Bannerlord

Репозиторий содержит исходные коды, конфигурации XML, интерфейсы Gauntlet и ресурсы сетевого модуля **Daewden (DWDN)**.

---

## 📖 Документация и руководства

В репозитории представлена полная техническая и архитектурная документация модуля и движка игры:

1. **[docs/DOCUMENTATION.md](docs/DOCUMENTATION.md)** — практическое руководство по моддингу DWDN:
   * **Архитектурная карта мода**: подробное описание назначения всех папок и файлов.
   * **Пошаговое руководство**: как добавить новый класс и экипировку от XML (`mpcharacters.xml`, `mpclassdivisions.xml`) до C# регистрации (`DWDN_DuchyManager.cs`).
   * **Сетевая синхронизация**: описание работы клиент-серверной архитектуры спавна и применения лодаутов.
   * **Тактическая система Ауры Знаменосца (`DWDN_BannerAuraBehavior.cs`)**: описание балансных бонусов строя в радиусе 12 м, личной защиты Знаменосца и механики «Второго дыхания».
   * **Комплекс визуальных GUI-студий моддинга (`Tools/`)**: описание работы `WeaponCalibrator`, `DuchyLoadoutStudio` и `ModValidator`.
2. **[AGENTS.md](AGENTS.md)** — инженерный кодекс и технический справочник по внутренним системам DWDN (`AgentDrivenProperties`, `CraftingPiece` Z-оффсеты, кастомный Gauntlet UI, защита от крашей `0xC0000005`).
3. **[docs/ENGINE.md](docs/ENGINE.md)** — полная энциклопедия реверс-инжиниринга ядра *Mount & Blade II: Bannerlord (v1.4.8)* (`Native`, `SandBoxCore`, `SandBox`, `Multiplayer`): C++/C# мост `DotNetObject`, двойной пайплайн XML (`project.mbproj` vs `MBObjectManager`), бинарный формат `.tpac` (`LZ4` + `METM`) и `.rdc`, лимит 64 костей скелета и сетевой протокол.
4. **[docs/CHRONICLE.md](docs/CHRONICLE.md)** — полная хроника разработки, архитектурных решений, балансных правок и исправлений модуля.
5. **[docs/PLAN.md](docs/PLAN.md)** — куда идёт мод: цели, этапы и решения автора.
6. **[docs/STATUS.md](docs/STATUS.md)** — что сделано и проверено сейчас, что сломано.
7. **[docs/BACKLOG.md](docs/BACKLOG.md)** — задачи без срока и ссылки на [Issues](https://github.com/Havi-Dorgeir/DWDN_Module/issues).

---

## 🧰 Автономные визуальные инструменты моддинга (`Tools/`)

* **`Tools/WeaponCalibrator/`** — **Авто-Калибратор 3D Оружия**: парсит `.tpac` и `.fbx`, вычисляет точные габариты и стыки (`previous_piece_offset` / `next_piece_offset`), отображает 3D-вьюпорт и автоматически обновляет `crafting_pieces.xml`.
* **`Tools/DuchyLoadoutStudio/`** — **Визуальный Конструктор Герцогств и Лодаутов**: интерактивный редактор классов с 3D-манекеном и кнопкой мгновенного применения в мод (`C# + XML + сборка клиентской и серверной DLL`).
* **`Tools/ModValidator/`** — **Мгновенный Предпусковой Валидатор Мода**: за 0.25 сек проверяет 8 уровней целостности XML, C#, 3D-мешей в `.tpac` и `SHA-256` синхронизацию с выделенным сервером `DaewdenM`.
* **`Tools/MechanicsTester/`** — **Тестовый полигон механик**: 28 тестов без запуска игры (сеть, лодауты, аура знаменосца, артиллерия, ABI, Admin Tools). Запуск: `dotnet run --project Tools\MechanicsTester\MechanicsTester.csproj -c Release -- --cli`.

---

## 🛠 Быстрый старт: Сборка исходного кода

Модуль собирается из двух проектов:

| Что | Проект | Платформа | Куда кладётся |
|---|---|---|---|
| Клиент — `DWDN_Script.dll` | `ModuleData/scripts/DWDN_Script/DWDN_Script.csproj` | .NET Framework 4.7.2 | `bin/Win64_Shipping_Client` |
| Сервер — `DWDN_Script.Server.dll` | `ModuleData/scripts/DWDN_Script.Server/DWDN_Script.Server.csproj` | **.NET 8** | `bin/Win64_Shipping_Server` и `bin/Linux64_Shipping_Server` |

С Bannerlord **v1.5.7** выделенный сервер работает на .NET 8. Серверная DLL,
собранная под .NET Framework, на нём не загружается, поэтому сервер собирается
отдельным проектом и против библиотек **выделенного сервера** (Steam,
«Mount & Blade II: Dedicated Server»), а не клиента.

```powershell
# Клиент (следом сам соберёт и сервер)
dotnet build ModuleData/scripts/DWDN_Script/DWDN_Script.csproj -c Release

# Только сервер
dotnet build ModuleData/scripts/DWDN_Script.Server/DWDN_Script.Server.csproj -c Release
```

Где лежит выделенный сервер, проект ищет сам — рядом с клиентом в той же
библиотеке Steam (`steamapps/common/Mount & Blade II Dedicated Server`). Если он
в другом месте, укажите путь: `-p:BannerlordServerDir="D:\...\Mount & Blade II Dedicated Server"`
или переменной окружения `BANNERLORD_SERVER_DIR`.

---

## 🚀 Как выпустить

1. Поднять `<Version value="vX.Y.Z"/>` в `SubModule.xml` и поставить такой же тег `vX.Y.Z`.
2. Собрать клиент и сервер, прогнать полигон `Tools/MechanicsTester` (текущий результат — в [docs/STATUS.md](docs/STATUS.md)).
3. Собрать архив для игры: `SubModule.xml`, `bin/`, `ModuleData/` без `scripts/`, `GUI/`, `Assets/`, `Shaders/`. Без `AssetSources/`, `Tools/`, документов и `.pdb`.
4. Выложить архив на сайт Даевдена: стол урядника → «Лаунчер: моды и карты». Сайт проверит архив и отдаст каталог лаунчеру.

Автоматизацию выпуска (`release.ps1` + GitHub Releases) описывает [#6](https://github.com/Havi-Dorgeir/DWDN_Module/issues/6). Собирать в GitHub Actions нельзя: библиотеки игры распространять нельзя, а без них проект не собирается, — выпуск собирается локально, GitHub его только хранит.

---

## 📂 Структура репозитория

* `ModuleData/` — XML-манифесты классов (`mpclassdivisions.xml`), персонажей (`mpcharacters.xml`), предметов (`mpitems/`), кастомного оружия (`crafting_pieces.xml`, `crafting_templates.xml`).
* `ModuleData/scripts/DWDN_Script/` — Исходный C#-код мода (клиентский UI на Gauntlet, сетевые сообщения, серверные подсистемы экипировки).
* `GUI/` — XML-разметка и ресурсы кастомного окна выбора классов и лодаутов.
* `Assets/` — Материалы, текстуры и параметры ассетов юнитов.
* `SubModule.xml` — Главный манифест мода для загрузчика TaleWorlds.

---
*Разработано для сообщества Mount & Blade II Bannerlord.*
