# GitHub Workflow, ветки и автоматические релизы

## 1. Модель веток

```text
feature/* (или fix/*)
        │
        ▼  Pull Request (ревью + проверки)
   development
        │
        ▼  Pull Request (только @Havi-Dorgeir)
      main  ──────▶  GitHub Actions (.github/workflows/release.yml)
                             │
                             ▼
                  GitHub Release + DWDN_TRY_<version>.zip
```

### Назначение веток
- **`main`** — исключительно стабильная релизная ветка. Право вливать изменения принадлежит только владельцу репозитория (`@Havi-Dorgeir`). Каждый push/merge в `main` автоматически запускает публикацию релиза.
- **`development`** — основная рабочая ветка интеграции. Прямой push запрещён; все задачи попадают сюда через Pull Request из веток `feature/*` или `fix/*`.
- **`feature/<name>` / `fix/<name>`** — краткоживущие ветки для конкретных задач и исправлений.

---

## 2. Автоматический релиз из `main`

Workflow [`.github/workflows/release.yml`](../.github/workflows/release.yml) запускается автоматически при каждом push в `main` (а также может быть запущен вручную через `workflow_dispatch`):

1. Извлекает тег версии из `<Version value="vX.Y.Z"/>` в [`SubModule.xml`](../SubModule.xml).
2. Проверяет, существует ли уже релиз/тег для этой версии (если версия в `SubModule.xml` не была повышена, релиз не дублируется — или при ручном запуске может быть пересобран).
3. Упаковывает чистый релизный архив `DWDN_TRY_<version>.zip` со структурой:
   - `SubModule.xml`
   - `bin/` (только `.dll`, без `.pdb`)
   - `ModuleData/` (без `ModuleData/scripts/`)
   - `GUI/`
   - `Assets/`
   - `Shaders/`
   - *(Исключены: `AssetSources/`, `Tools/`, `docs/`, `.github/`, `.git/`, `.pdb`, `.bat`, `.ps1`, документация.)*
4. Создаёт тег `<version>` и публикует **GitHub Release** с автогенерируемыми release notes и прикреплённым архивом `DWDN_TRY_<version>.zip`.

### Памятка перед слиянием `development` → `main`
1. Соберите `Client` (`.NET 4.7.2`) и `Server` (`.NET 8`) в `Release` и убедитесь, что обновлённые `bin/**/*.dll` закоммичены.
2. Прогоните `Tools/MechanicsTester` (`--cli`).
3. Поднимите `<Version value="vX.Y.Z"/>` в `SubModule.xml`.
4. Влейте `development` в `main` — GitHub Actions автоматически соберёт архив и опубликует релиз.

---

## 3. Настройка защиты веток (Branch Protection / Rulesets)

> **Важно о тарифах GitHub:** на приватных репозиториях тарифа **GitHub Free** серверные Branch Protection Rules и Repository Rulesets заблокированы самой платформой GitHub (`Upgrade to GitHub Pro or make this repository public to enable this feature`).
>
> До включения GitHub Pro или перевода репозитория в Public в репозитории уже работают программные страховки:
> - [`.github/CODEOWNERS`](../.github/CODEOWNERS) — автоматически назначает `@Havi-Dorgeir` ревьюером всех PR.
> - [`.github/workflows/branch-guard.yml`](../.github/workflows/branch-guard.yml) — проверяет PR в `main` (разрешает слияние только от `@Havi-Dorgeir` и предупреждает, если исходная ветка не `development`).

### После включения GitHub Pro или перевода репо в Public
В **Settings → Rules → Rulesets → New branch ruleset** создайте два правила:

1. **Ruleset `protect-development`** (Target branch: `development`):
   - ✅ Restrict deletions
   - ✅ Block force pushes
   - ✅ Require a pull request before merging (запрещает прямой push в `development`)
   - ✅ Require status checks to pass (`Branch & PR Guard`)

2. **Ruleset `protect-main`** (Target branch: `main`):
   - **Bypass list:** добавить только `Repository admin` (`@Havi-Dorgeir`)
   - ✅ Restrict creations / deletions / updates (блокирует push всем, кроме списка bypass)
   - ✅ Block force pushes
   - ✅ Require a pull request before merging + Require review from Code Owners
   - ✅ Require status checks to pass (`Branch & PR Guard`)
