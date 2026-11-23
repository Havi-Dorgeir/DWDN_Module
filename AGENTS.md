# AGENTS.md — Инженерный индекс DWDN для ИИ-агентов

> Компактная точка входа для работы с модулем **Daewden (DWDN)** (*Mount & Blade II: Bannerlord*).
> Глубокие спецификации, реверс-инжиниринг и хроника вынесены в [`docs/`](docs/) — обращайтесь к ним по мере необходимости, не перегружая контекст.

---

## 1. Базовая архитектура модуля

Модуль состоит из двух целевых сборок, разделяющих общий код (`Common/`) и платформенные подсистемы (`Client/` и `Server/`):

| Слой | Проект | Платформа | Выходной путь |
|---|---|---|---|
| **Клиент** | `ModuleData/scripts/DWDN_Script/DWDN_Script.csproj` | `.NET Framework 4.7.2` | `bin/Win64_Shipping_Client/DWDN_Script.dll` |
| **Сервер** | `ModuleData/scripts/DWDN_Script.Server/DWDN_Script.Server.csproj` | `.NET 8.0` (Bannerlord v1.5.7+) | `bin/Win64_Shipping_Server/` и `bin/Linux64_Shipping_Server/` |

### Карта директорий
- `SubModule.xml` — главный манифест модуля, версия (`<Version value="vX.Y.Z"/>`), регистрация клиентской и серверной DLL, список XML.
- `ModuleData/` — игровые XML (`mpclassdivisions.xml`, `mpcharacters.xml`, `mpcultures.xml`, `crafting_pieces.xml`, `crafting_templates.xml`, `weapon_descriptions.xml`, `artillery_config.xml`, `mpitems/*.xml`).
- `ModuleData/scripts/DWDN_Script/` — исходный C#-код:
  - `Common/` — герцогства, классы и сетевые сообщения (`DWDN_DuchyManager`, `NetworkMessages`).
  - `Client/` — Gauntlet UI выбора классов, 3D-превью, HUD артиллерии, админ-панель.
  - `Server/` — серверная логика спавна, лодаутов, ауры знаменосца, админ-сервиса.
  - Корневые `DWDN_*Behavior.cs` — общие/клиент-серверные `MissionBehavior` (артиллерия, огнестрел, ввод).
- `GUI/` — Gauntlet XML-префабы (`Prefabs/`) и спрайты (`Brushes/`, `SpriteData/`).
- `Assets/` — запечённые ресурсы игры (`.tpac`); `AssetSources/` — исходные `.fbx`/`.png` (в релизный архив не входят).
- `Tools/` — автономные утилиты (`.NET 8`): `MechanicsTester`, `ModValidator`, `WeaponCalibrator`, `DuchyLoadoutStudio`.

### Базовые команды сборки и проверки
```powershell
# Сборка клиента (автоматически подтягивает сервер)
dotnet build ModuleData/scripts/DWDN_Script/DWDN_Script.csproj -c Release

# Сборка только выделенного сервера (.NET 8)
dotnet build ModuleData/scripts/DWDN_Script.Server/DWDN_Script.Server.csproj -c Release

# Предпусковой CLI-прогон тестов механик (без запуска игры)
dotnet run --project Tools/MechanicsTester/MechanicsTester.csproj -c Release -- --cli

# Синхронизация с локальным Dedicated Server
.\sync_all.ps1
```

---

## 2. Внешние скиллы и базы знаний для моддинга

Перед сложными задачами по движку, нативному коду, ассетам или декомпиляции обязательно опирайтесь на следующие проверенные скиллы и репозитории:

### Universal Modder (`rehan-remade/universal-modder`)
- **Bannerlord Engine Reference** — специфика движка TaleWorlds, модулей и XML/C# пайплайна:
  [engines/bannerlord.md](https://github.com/rehan-remade/universal-modder/blob/main/.agents/skills/mod-any-game/references/engines/bannerlord.md)
- **Mod Any Game (Core Skill)** — базовый протокол исследования и разработки модов:
  [mod-any-game/SKILL.md](https://github.com/rehan-remade/universal-modder/blob/main/.agents/skills/mod-any-game/SKILL.md)
- **Safety & Stability Rules** — защита от крашей, битых сохранений и деградации:
  [mod-any-game/references/safety.md](https://github.com/rehan-remade/universal-modder/blob/main/.agents/skills/mod-any-game/references/safety.md)
- **Reverse Engineering** — декомпиляция, анализ структур памяти и нативных вызовов:
  [reverse-engineering/SKILL.md](https://github.com/rehan-remade/universal-modder/blob/main/.agents/skills/reverse-engineering/SKILL.md)
- **Game Recon** — разведка бинарников, логов и runtime-состояния:
  [game-recon/SKILL.md](https://github.com/rehan-remade/universal-modder/blob/main/.agents/skills/game-recon/SKILL.md)
- **Asset Pipeline** — работа с 3D-моделями, текстурами, материалами и контейнерами ассетов:
  [asset-pipeline/SKILL.md](https://github.com/rehan-remade/universal-modder/blob/main/.agents/skills/asset-pipeline/SKILL.md)
- **Publish Mod** — упаковка, версионирование и выпуск релизов:
  [publish-mod/SKILL.md](https://github.com/rehan-remade/universal-modder/blob/main/.agents/skills/publish-mod/SKILL.md)
- **Share Field Notes** — фиксация технических находок для последующих ИИ-сессий:
  [share-field-notes/SKILL.md](https://github.com/rehan-remade/universal-modder/blob/main/.agents/skills/share-field-notes/SKILL.md)

### Нативные хуки (C++ / Interop)
- **NativeHook (`ShortJake/NativeHook`)** — референс по перехвату нативных функций и работе с C++/C# мостом:
  [github.com/ShortJake/NativeHook](https://github.com/ShortJake/NativeHook)

---

## 3. Обязательные правила работы

1. **План перед действием:** сначала опишите план изменений, затем приступайте к правкам.
2. **Минимальные изменения:** не меняйте работающий код вне рамок задачи; не создавайте временные копии XML/SubModule в репозитории.
3. **Проверка перед коммитом:** после правок кода собирайте обе DLL (`Client` + `Server`) и прогоняйте `Tools/MechanicsTester`.
4. **Конспект для других ИИ:** изучите последние записи в [`docs/CHRONICLE.md`](docs/CHRONICLE.md) перед началом и кратко зафиксируйте свои значимые находки/решения.
5. **Поток веток:** вся разработка ведётся в `feature/*` / `fix/*` → PR в `development` → слияние в `main` (только владелец `@Havi-Dorgeir`), что автоматически создаёт релиз.

---

## 4. Карта документации (`docs/`)

| Документ | Когда читать |
|---|---|
| [`docs/AI_MODDING_GUIDE.md`](docs/AI_MODDING_GUIDE.md) | Глубокие внутренние спецификации DWDN: огнестрел, Gauntlet UI, пайплайн лодаутов, Z-оффсеты `CraftingPiece`, защита от крашей `0xC0000005`, сетевое сжатие, артиллерия, Admin Tools. |
| [`docs/DOCUMENTATION.md`](docs/DOCUMENTATION.md) | Практическое руководство по моддингу DWDN: добавление классов, экипировки, герцогств, аура знаменосца, GUI-студии в `Tools/`. |
| [`docs/ENGINE.md`](docs/ENGINE.md) | Энциклопедия реверс-инжиниринга ядра Bannerlord: `DotNetObject`, `project.mbproj` vs `MBObjectManager`, форматы `.tpac`/`.rdc`, скелет, сетевой протокол. |
| [`docs/CHRONICLE.md`](docs/CHRONICLE.md) | Полная хроника разработки, решённых багов, балансных правок и конспект предыдущих ИИ-сессий. |
| [`docs/GITHUB_WORKFLOW.md`](docs/GITHUB_WORKFLOW.md) | Модель веток (`development` / `main`), правила PR, настройка Branch Protection / Rulesets и автоматический релизный пайплайн. |
