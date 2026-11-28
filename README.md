# Daewden (DWDN) Multiplayer Module

Сетевой модуль фракции **Даевден (Daewden)** для *Mount & Blade II: Bannerlord* (клиент + выделенный сервер `.NET 8`, кастомный Gauntlet UI герцогств, полевая артиллерия, тактическая аура знаменосца, Admin Tools и набор визуальных инструментов моддинга).

---

## Быстрый старт

### 1. Сборка исходного кода

| Сборка | Проект | Платформа | Выход |
|---|---|---|---|
| Клиент (`DWDN_Script.dll`) | `ModuleData/scripts/DWDN_Script/DWDN_Script.csproj` | `.NET Framework 4.7.2` | `bin/Win64_Shipping_Client/` |
| Сервер (`DWDN_Script.Server.dll`) | `ModuleData/scripts/DWDN_Script.Server/DWDN_Script.Server.csproj` | `.NET 8.0` | `bin/Win64_Shipping_Server/` и `bin/Linux64_Shipping_Server/` |

```powershell
# Сборка клиента (автоматически собирает и серверную DLL)
dotnet build ModuleData/scripts/DWDN_Script/DWDN_Script.csproj -c Release

# Сборка только выделенного сервера (.NET 8, Bannerlord v1.5.7+)
dotnet build ModuleData/scripts/DWDN_Script.Server/DWDN_Script.Server.csproj -c Release
```

Путь к библиотекам выделенного сервера определяется автоматически из соседней папки Steam (`Mount & Blade II Dedicated Server`). При нестандартном расположении передайте `-p:BannerlordServerDir="D:\...\Mount & Blade II Dedicated Server"` или задайте переменную окружения `BANNERLORD_SERVER_DIR`.

### 2. Проверка и синхронизация

```powershell
# Автономный прогон тестов механик (без запуска игры)
dotnet run --project Tools/MechanicsTester/MechanicsTester.csproj -c Release -- --cli

# Синхронизация DLL, XML и GUI с локальным Dedicated Server
.\sync_all.ps1
```

---

## Модель веток и автоматические релизы

```text
feature/* или fix/*  ──(PR)──▶  development  ──(PR, только @Havi-Dorgeir)──▶  main  ──(GitHub Actions)──▶  GitHub Release (.zip)
```

- **`development`** — интеграционная ветка разработки (изменения вносятся через Pull Request).
- **`main`** — стабильная релизная ветка (слияние только владельцем репозитория `@Havi-Dorgeir`).
- **Авторелиз:** каждый push/merge в `main` запускает [`.github/workflows/release.yml`](.github/workflows/release.yml), который считывает `<Version value="vX.Y.Z"/>` из [`SubModule.xml`](SubModule.xml), собирает чистый игровой архив `DWDN_TRY_<version>.zip` (без `AssetSources/`, `Tools/`, `docs/`, исходников `scripts/` и `.pdb`) и автоматически публикует GitHub Release.

Подробные правила работы с ветками и настройкой защиты описаны в [`docs/GITHUB_WORKFLOW.md`](docs/GITHUB_WORKFLOW.md).

---

## Документация

Вся детальная документация структурирована и вынесена в отдельные файлы:

| Файл | Назначение |
|---|---|
| [`AGENTS.md`](AGENTS.md) | Точка входа для ИИ-агентов: базовая архитектура, скиллы `universal-modder` / `NativeHook`, правила работы — и полный кодекс архитектора по внутренним системам DWDN (огнестрел, Gauntlet UI, Z-оффсеты, артиллерия, Admin Tools, защита от крашей), англ. |
| [`docs/DOCUMENTATION.md`](docs/DOCUMENTATION.md) | Практическое руководство: добавление классов, экипировки, герцогств, сетевая синхронизация, аура знаменосца. |
| [`docs/ENGINE.md`](docs/ENGINE.md) | Энциклопедия реверс-инжиниринга ядра Bannerlord (`DotNetObject`, `.tpac`/`.rdc`, XML-пайплайн, сетевой протокол). |
| [`docs/CHRONICLE.md`](docs/CHRONICLE.md) | Хроника разработки, архитектурных решений, багфиксов и баланса. |
| [`docs/GITHUB_WORKFLOW.md`](docs/GITHUB_WORKFLOW.md) | Регламент веток `development` / `main`, настройки Branch Protection / Rulesets и авторелизов. |
| [`docs/PLAN.md`](docs/PLAN.md) | Куда идёт мод: цели, этапы и решения автора. |
| [`docs/STATUS.md`](docs/STATUS.md) | Что сделано и проверено сейчас, что сломано. |
| [`docs/BACKLOG.md`](docs/BACKLOG.md) | Задачи без срока и ссылки на [Issues](https://github.com/Havi-Dorgeir/DWDN_Module/issues). |
| [`docs/agents/`](docs/agents/) | Специализированные глубокие разборы подсистем для ИИ-агентов (огнестрел, Gauntlet UI и 3D-превью, лодауты и спавн, крафтовое оружие, реверс-инжиниринг, игровые системы и тулзы, траблшутинг, анти-скрапкод и сеть) — индекс в `AGENTS.md`. |

---

## Инструменты моддинга (`Tools/`)

Все лаунчеры `.bat` расположены внутри `Tools/` и работают по относительным путям:

- **`Tools/MechanicsTester/`** — автономный полигон юнит- и интеграционных тестов механик DWDN (GUI + `--cli`).
- **`Tools/ModValidator/`** — предпусковой валидатор XML, C#, 3D-мешей `.tpac` и SHA-256 синхронизации с сервером.
- **`Tools/DuchyLoadoutStudio/`** — визуальный конструктор герцогств, классов и лодаутов с 3D-манекеном.
- **`Tools/WeaponCalibrator/`** — 3D-калибратор стыков составного оружия (`crafting_pieces.xml`).

---

## Структура репозитория

- `SubModule.xml` — манифест модуля TaleWorlds.
- `bin/` — собранные клиентские (`.NET 4.7.2`) и серверные (`.NET 8`) сборки.
- `ModuleData/` — игровые XML-конфигурации и исходный код `scripts/`.
- `GUI/` — Gauntlet XML-префабы и графические ресурсы интерфейса.
- `Assets/` / `AssetSources/` — запечённые `.tpac` и исходные 3D/текстурные файлы.
- `Shaders/` — скомпилированный кэш шейдеров модуля.
- `Tools/` — утилиты разработки и тестирования.
- `docs/` — техническая и архитектурная документация.
- `.github/` — CI-проверки, `CODEOWNERS` и автоматическая публикация релизов.
