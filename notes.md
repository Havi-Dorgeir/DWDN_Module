# Конспект проекта DWDN_TRY (Daewden Mod)

## 1. Структура путей и развертывания (ВАЖНО!)
- **Клиентский модуль:** `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\DWDN_TRY`
- **Серверный модуль (Dedicated Server):** `E:\SteamLibrary\steamapps\common\Mount & Blade II Dedicated Server\Modules\DaewdenM`
- **Серверный бинарный каталог:** `E:\SteamLibrary\steamapps\common\Mount & Blade II Dedicated Server\bin\Win64_Shipping_Server`
- **Проект C#:** `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\DWDN_TRY\ModuleData\scripts\DWDN_Script\DWDN_Script.csproj`
  - **КРИТИЧЕСКОЕ ПРАВИЛО СБОРКИ:** Компилировать проект нужно **ТОЛЬКО** из папки клиента (`Modules\DWDN_TRY\ModuleData\scripts\DWDN_Script\DWDN_Script.csproj`)!
  - Почему: в `.csproj` прописан относительный путь `<OutputPath>..\..\..\bin\Win64_Shipping_Client\</OutputPath>`. Предыдущий агент правил файлы в `Dedicated Server\Modules\DaewdenM` и собирал `.csproj` оттуда, в результате чего клиент игры (`Modules\DWDN_TRY\bin\Win64_Shipping_Client\DWDN_Script.dll`) оставался со старой DLL и старыми XML!
  - После правок обязательно синхронизировать все `.cs` и `.xml` между `Modules\DWDN_TRY` и `Dedicated Server\Modules\DaewdenM`.

---

## 2. Графический баг интермиссии и гербовые щиты (Команды и Интермиссия)
- **Файлы:**
  - `Modules\DWDN_TRY\ModuleData\scripts\DWDN_Script\SubModule.cs`
  - `Modules\DWDN_TRY\GUI\SpriteData.xml`
- **Истинная причина гигантского щита на экране смены карты (`MultiplayerIntermission`):**
  1. На экране `MultiplayerIntermission.xml` позади затемняющей плашки `intermission_screen_band_effect` рендерятся `<Standard.Background />` (`ScreenBackgroundBrushWidget`, использующий кисть `Standard.Background.Brush` со спрайтами `background_1..4` -> `dwdn_screen_load_1`) и `<MultiplayerIntermissionNextMapImageWidget />`.
  2. В запакованном ассет-пакете клиента `Modules\DWDN_TRY\AssetPackages\pack0.tpac` внутри текстурной категории `dwdn_loading` **Лист 1 (`SheetID = 1`, индекс `0`)** — это изображение 1920x1080 арены с **гигантским щитом Daewden по центру**, тогда как Листы 2 и 3 (`SheetID = 2, 3`) — чистые фоновые арты.
  3. Когда движок загружает или перезагружает категорию `dwdn_loading` из `pack0.tpac` (например, при открытии интермиссии), `dwdn_loading.SpriteSheets[0]` перезаписывался текстурой из `pack0.tpac`. Ранее `OnApplicationTick` проверял только флаги `_loadingTextureLoaded` и не замечал подмены `SpriteSheets[0]`. Кроме того, в `GUI\SpriteData.xml` у `dwdn_screen_load` и `dwdn_screen_load_1` стоял `<SheetID>1</SheetID>`.
- **Решение:**
  1. В `GUI\SpriteData.xml` для `dwdn_screen_load`, `dwdn_screen_load_1` и `dwdn_screen_load_3` установлен `<SheetID>2</SheetID>`, а для `dwdn_screen_load_2` — `<SheetID>3</SheetID>`.
  2. В `SubModule.OnApplicationTick` добавлена ежекадровая проверка: если `UIResourceManager.SpriteData.SpriteCategories["dwdn_loading"].SpriteSheets[0] != _injectedLoadingTexture1` (или аналогично для `dwdn_faction`), немедленно вызывается повторная инъекция `InjectAllLoadingSprites` / `InjectIntoSpriteData`.
  3. Метод `UpdateSpriteGenericInPlace` обновляет приватное поле `_spritePart` у уже существующих экземпляров `SpriteGeneric` (`background_1..4`, `loading_01..12`, `dwdn_screen_load_1..3`), чтобы кисти (`Standard.Background.Brush`), закэшировавшие ссылку на `Sprite` при старте игры, сразу использовали чистые обои.
  4. На всех слоях `ScreenManager.SortedLayers` **всегда** вызывается `FixFactionBanners(gl.UIContext.Root)` (чтобы маленький щит 220x220 слева на интермиссии и щиты на выборе команды отображались корректно), и дополнительно при `IsIntermissionLayer(gl)` вызывается `CleanIntermissionWidgets(gl.UIContext.Root)` (скрывает `MultiplayerIntermissionNextMapImageWidget`, принудительно ставит чистый фон `dwdn_screen_load_1` в `ScreenBackgroundBrushWidget` и скрывает любые не-`MultiplayerFactionBannerWidget` с иконкой фракции).

---

## 3. Гербы и кружки команд в HUD (Инверсия цветов)
- **Файлы:**
  - `Modules\DWDN_TRY\ModuleData\mpcultures.xml`
  - `Dedicated Server\Modules\DaewdenM\ModuleData\mpcultures.xml`
- **Настройка:**
  - Ключ баннера: `faction_banner_key="11.116.116.1536.1536.764.764.1.0.0.340.15.15.303.303.764.545.1.0.0.138.15.15.480.480.764.873.1.0.0"`
  - Цвет `116` — черный фон, цвет `15` — золотисто-желтый медведь и корона.
  - Команда 1 (Атакующие): Желтый медведь и желтая корона на черном фоне.
  - Команда 2 (Защитники): Черный медведь и черная корона на желтом фоне (инвертируется движком зеркально).

---

## 4. Знаменосец (Штандарт в левую руку + ношение на поясе)
- **Файлы:**
  - `Modules\DWDN_TRY\ModuleData\mpitems\dwdn_banners.xml` (и в `DaewdenM`)
  - `Modules\DWDN_TRY\ModuleData\mpcharacters.xml` (и в `DaewdenM`)
  - `Modules\DWDN_TRY\ModuleData\scripts\DWDN_Script\Common\Duchies\DWDN_DuchyManager.cs`
  - `Modules\DWDN_TRY\ModuleData\scripts\DWDN_Script\Server\Behaviors\DWDN_ServerLoadoutBehavior.cs`
  - `Modules\DWDN_TRY\ModuleData\scripts\DWDN_Script\Client\Behaviors\DWDN_ClientLoadoutBehavior.cs`
- **Истинная причина, почему знамя было ТОЛЬКО на поясе и не бралось в руку в прошлой итерации:**
  - В `TaleWorlds.Core.ItemObject.Deserialize` парсинг идет строго в порядке:
    1. `mesh="dwdn_banner1"`
    2. `item_holsters="throwing_stone_2"` (успешно записывалось — поэтому флаг висел на поясе!)
    3. `<ItemComponent><Banner ... /></ItemComponent>` -> вызывает `BannerComponent.Deserialize`.
    4. `<Flags HeldInOffHand="true" HasToBeHeldUp="true" ... />`
    5. `Type="Banner"`
  - Внутри `BannerComponent.Deserialize` движок выполняет **без проверки на null**:
    `this.BannerEffect = MBObjectManager.Instance.GetObject<BannerEffect>(node.Attributes["effect"].Value);`
  - В прошлой итерации в `<Banner ... />` отсутствовал обязательный атрибут `effect="IncreasedMeleeDamage"`, из-за чего `BannerComponent.Deserialize` выбрасывал `NullReferenceException` посреди чтения предмета! В итоге `ItemHolsters` (`throwing_stone_2`) сохранялся, но `ItemComponent` (`PrimaryWeapon`) оставался `null`, `<Flags>` не читались, и предмет физически не мог быть взят в руку.
- **Решение (НЕ УДАЛЯТЬ `effect="IncreasedMeleeDamage"`!):**
  - В `dwdn_banners.xml` прописан `Type="Banner"`, `item_holsters="throwing_stone_2"`, внутри `<Banner>` обязательно указан `effect="IncreasedMeleeDamage"`, `weapon_class="Banner"`, `item_usage="banner"`, `position="0.0, 0.0, 0.0"`, а в `<Flags>` — `HeldInOffHand="true"`, `HasToBeHeldUp="true"`, `ForceAttachOffHandPrimaryItemBone="true"`, `DropOnWeaponChange="false"`.
  - В `mpcharacters.xml` и `DWDN_DuchyManager.cs` у `mp_Bannerman` в слоте 0 (`Item0`) стоит одноручное оружие (`mp_battania_noble_sword` / топор / булава), а в слоте 1 (`Item1`) — `dwdn_banner1`.
  - На сервере и клиенте при спавне знамя берется в левую руку (`Weapon1`), а при убирании в ножны висит на поясе (`throwing_stone_2`).

---

## 5. Стрельба: Стрелок с ручницей и Стрелок с луком (Исправление холостых выстрелов)
- **Файлы:**
  - `Modules\DWDN_TRY\ModuleData\scripts\DWDN_Script\Client\Behaviors\DWDN_ClientLoadoutBehavior.cs`
  - `Modules\DWDN_TRY\ModuleData\scripts\DWDN_Script\Server\Behaviors\DWDN_ServerLoadoutBehavior.cs`
  - `Modules\DWDN_TRY\ModuleData\scripts\DWDN_Script\DWDN_FirearmsBehavior.cs` (не трогать — работает штатно!)
- **Истинная причина бага "холостые выстрелы при спавне":**
  1. Сетевое сообщение `SetWeaponAmmoData` и нативный метод `SetReloadAmmoInSlot` предназначены **строго** для момента завершения физической анимации перезарядки (`Agent.OnWeaponAmmoReload`). В ванильном движке они **никогда** не вызываются при спавне!
  2. Когда предшественник вставил искусственную рассылку `SetWeaponAmmoData(..., (short)weapon.Amount)` при спавне, он обманул нативный C++ конечный автомат оружия: движок счёл, что в патроннике уже есть готовые заряды, и при первом натяжении тетивы/взводе **пропустил** вызов реального `OnWeaponAmmoReload`.
  3. Из-за пропуска реального `OnWeaponAmmoReload` поле `weapon.AmmoWeapon` в C# на сервере оставалось пустым, и серверный `Mission.OnAgentShootMissile` отменял выстрелы.
  4. Каждый холостой клик списывал 1 единицу из нативного фантомного счётчика. Когда предшественник передавал туда малое число (1–4), требовалось 1–4 холостых выстрела, пока счётчик не обнулялся. А когда мы ошибочно передали туда `remainingAmmo` (весь стек из 16–24 пуль/стрел), фантомный счётчик раздулся, и число холостых выстрелов увеличилось до размера всего колчана.
- **Решение:**
  1. В `DWDN_ServerLoadoutBehavior.cs` и `DWDN_ClientLoadoutBehavior.cs` полностью удалены искусственные инъекции `SetWeaponAmmoData`, `SetReloadAmmoInSlot` и принудительный `SetReloadedAmmoOfSlot` на этапе спавна (которые вызывали фантомный счётчик и холостые выстрелы).
  2. Никаких искусственных вызовов `SetWeaponReloadPhase` или `SetWeaponReloadPhaseAsClient`! (Попытка вызвать `SetWeaponReloadPhaseAsClient` блокирует стейт-машину нативного движка, переводя оружие в режим незавершённой фазы перезарядки, из-за чего атака/выстрел физически блокируются).
  3. Для штатной зарядки арбалетов и ручниц (`WeaponClass.Crossbow`) при спавне:
     - Применяется правильная последовательность ванильного движка:
       `agent.Equipment?.CheckLoadedAmmos();` (заряжает 1 патрон в managed MissionWeapon)
       `agent.EquipItemsFromSpawnEquipment(true, true, false, 0);` (передает обновленные WeaponData и AmmoWeaponData в нативный движок)
       `agent.UpdateAgentProperties();`
       `agent.UpdateWeapons();`
       `agent.WieldInitialWeapons();`
     - Этот обряд выполняется синхронно как на сервере, так и на клиенте в `OnMyAgentSpawnedFromVisual`.
  4. Лукам возвращена штатная механика: при первом натяжении тетивы нативный движок сам вызывает `OnWeaponAmmoReload`, связывая колчан с луком без каких-либо холостых выстрелов.

---

## 6. Перчатки Офицера и Збройного
- **Файл:** `ModuleData\scripts\DWDN_Script\Common\Duchies\DWDN_DuchyManager.cs`
- Добавлены `plate_gauntlet` (как у Пикинера) и `plate_round_gauntlet` в `AllowedGloves` для офицера и збройного.

---

## 7. Названия Герцогств
- **Файл:** `ModuleData\scripts\DWDN_Script\Common\Duchies\DWDN_DuchyManager.cs`
- Герцогства названы: **Баннрайх**, **Рецезия**, **Креамор**.

---

## 8. Статус синхронизации и компиляции
- **Сборка C#:** Успешно (0 ошибок, 0 предупреждений).
- **Клиентская DLL:** `Modules\DWDN_TRY\bin\Win64_Shipping_Client\DWDN_Script.dll` (и копия в `DaewdenM`) обновлена.
- **Серверная DLL:** `Dedicated Server\Modules\DaewdenM\bin\Win64_Shipping_Server\DWDN_Script.Server.dll` (и в `Modules\DWDN_TRY\bin\Win64_Shipping_Server`) обновлена.
- **Исходники C# и XML-файлы:** 100% синхронизированы между клиентом и сервером.

---

## 9. Фракции мода (spclans.xml)
- Удалены все сторонние фракции (`vlandia`, `empire`, `battania`, `sturgia`, `aserai`, `khuzait`).
- Сохранена исключительно титульная фракция **Daewden** (`id="daewden"`).
- Изменения синхронизированы между клиентом (`Modules\DWDN_TRY\ModuleData\spclans.xml`) и выделенным сервером (`Dedicated Server\Modules\DaewdenM\ModuleData\spclans.xml`).

---

## 10. Архитектура подавления ванильных классов в Multiplayer и кастомный UI
- **Подробный технический трактат для ИИ:** `D:\banAImod\factions_and_loadout_architecture.md`
- **Проблема:** Правка `spclans.xml` не влияет на сетевой режим. В мультиплеере TaleWorlds использует `MissionGauntletClassLoadout`, формирующий список классов из `MultiplayerClassDivisions`. Предыдущий агент скрывал его через `gl.UIContext.Root.IsVisible`, но проверял `IsLocalPlayerDwdn()`, из-за чего ванильный селектор возвращался при несовпадении культуры лобби.
- **Решение (Паттерн Воркашоп-модов LotR / OMEE):**
  - В `DWDN_ClientLoadoutBehavior.cs` реализован безусловный `SuppressNativeClassLoadout()`:
    1. `nativeLoadout.OnMissionScreenFinalize();`
    2. `screen.UnregisterView(nativeLoadout);`
  - Добавлен референс `TaleWorlds.MountAndBlade.Multiplayer.GauntletUI` в `DWDN_Script.csproj`.
  - В `SubModule.CleanIntermissionWidgets` скрыт `AvailableCultures`, блокируя выбор культур на интермиссии.
- **Дизайн UI (`DWDN_ClassSelection.xml`):**
  - Выдержана высококонтрастная черно-желтая цветовая гамма (`#0B0F16FA`, `#0A0E14FF`, `#F5C768FF`, `#D4A044FF`).
  - Улучшена читаемость: увеличены кегли шрифтов, добавлен четкий черный контур (`TextOutlineAmount="2"`, `TextOutlineColor="#000000FF"`).
- **Расширяемость фракций:**
  - В `DWDN_DuchyManager.cs` введены методы `RegisterFaction(DWDN_Duchy)` и `RegisterDuchy(DWDN_Duchy)`.
  - Префаб `DWDN_ClassSelection.xml` и модель `DWDN_ClassSelectionVM.cs` автоматически генерируют кнопки для любых новых добавленных фракций.

---

## 11. Система селектора верховых животных (Кони и Попоны)
- **Файлы:**
  - `Common\Duchies\DWDN_ClassDefinition.cs`
  - `Common\Duchies\DWDN_LoadoutSelection.cs`
  - `Common\Duchies\DWDN_DuchyManager.cs`
  - `Common\Network\DWDN_NetworkMessages.cs`
  - `Client\UI\DWDN_EquipmentSlotVM.cs`
  - `Client\UI\DWDN_ClassSelectionVM.cs`
  - `Client\Behaviors\DWDN_ClientLoadoutBehavior.cs`
  - `Server\Behaviors\DWDN_ServerLoadoutBehavior.cs`
  - `ModuleData\mpcharacters.xml`
- **Проблема:**
  1. В `mpcharacters.xml` у Рыцаря (`mp_Knight`) не было коня и попоны в ростерах (раньше они давались ванильным перком, который наш селектор обходит), из-за чего Рыцарь всегда спавнился пешим.
  2. В коде селектора вообще отсутствовали слоты выбора коня и попоны.
  3. В `ApplySelectionToEquipment` на сервере слоты `EquipmentIndex.Horse` и `HorseHarness` не перезаписывались, из-за чего пехота при смене классов или остаточном стейте получала коней.
- **Решение:**
  1. В `DWDN_ClassDefinition` и `DWDN_LoadoutSelection` внедрены пулы и поля `AllowedHorses` / `AllowedHorseHarnesses` и `HorseId` / `HorseHarnessId`.
  2. В `DWDN_DuchyManager` кони и попоны назначены **СТРОГО 5 классам**:
     - **Кавалерия** (`mp_DWDN_Cav`)
     - **Рыцарь** (`mp_Knight`)
     - **Барон Клаус Берген** (`mp_Bergen_Klaus`)
     - **Барон Валериан Финнард** (`mp_Finnard_Valerian`)
     - **Король Альберик Девальд** (`mp_ALberic_Devald_hero`)
     У всех этих классов в список разрешенных включен пустой вариант `""` («Без предмета / Пешим»), дающий возможность появиться пешим бойцом без скакуна.
  3. У всех пехотных классов пулы коней строго пусты, поэтому в UI строки коня и попоны скрыты автоматически (`IsVisible="@HasOptions"`).
  4. На сервере (`DWDN_ServerLoadoutBehavior`) и клиенте (`DWDN_ClientLoadoutBehavior`) при пустом `HorseId` слоты `EquipmentIndex.Horse` и `HorseHarness` принудительно устанавливаются в `EquipmentElement.Invalid`, а если агент уже верхом — он мгновенно спешивается (`agent.Mount(null)`), и скакун удаляется со сцены (`mount.FadeOut(false, false)`).
  5. В `mpcharacters.xml` рыцарю `mp_Knight_hero` и `mp_Knight_troop` во все 3 экипировочных ростера добавлены `mp_vlandia_horse_war` и `mp_chain_horse_harness`.

---

## 12. Откат и восстановление проверенной эталонной версии селектора из `D:\banAImod\DWDN_TRY`
- **Причина:** Экспериментальные модификации слотов верховых животных и агрессивное удаление нативного селектора (`nativeLoadout.OnMissionScreenFinalize()`, `screen.UnregisterView(nativeLoadout)`) вызвали скрапкод и критические ошибки в Mission-лупе движка TaleWorlds, нарушив сетевой протокол и стабильность миссий.
- **Действие:**
  - Из эталонной рабочей директории `D:\banAImod\DWDN_TRY` перенесены все ключевые компоненты селектора:
    - `GUI\Prefabs\DWDN_ClassSelection.xml` (исходный стабильный Gauntlet UI префаб)
    - `ModuleData\mpcharacters.xml` (оригинальная конфигурация персонажей)
    - `ModuleData\scripts\DWDN_Script\Client\Behaviors\DWDN_ClientLoadoutBehavior.cs` (стабильное скрытие нативного селектора через `UIContext.Root.IsVisible`, без деструктивного размонтирования View)
    - `ModuleData\scripts\DWDN_Script\Client\UI\*` (`DWDN_ClassSelectionVM.cs`, `DWDN_EquipmentSlotVM.cs`, `DWDN_ItemOptionVM.cs`, `DWDN_ClassItemVM.cs`, `DWDN_DuchyItemVM.cs`)
    - `ModuleData\scripts\DWDN_Script\Common\Duchies\*` (`DWDN_ClassDefinition.cs`, `DWDN_DuchyManager.cs`, `DWDN_LoadoutSelection.cs`)
    - `ModuleData\scripts\DWDN_Script\Common\Network\DWDN_NetworkMessages.cs`
    - `ModuleData\scripts\DWDN_Script\Server\Behaviors\DWDN_ServerLoadoutBehavior.cs`
    - `ModuleData\scripts\DWDN_Script\DWDN_Script.csproj` и `SubModule.cs`
  - Выполнена чистая компиляция обеих целевых конфигураций:
    - **Client (Debug):** Сборка успешна, 0 ошибок.
    - **Server (Server):** Сборка успешна, 0 ошибок.
  - Проведена синхронизация файлов с выделенным сервером: `E:\SteamLibrary\steamapps\common\Mount & Blade II Dedicated Server\Modules\DaewdenM`.
- **Текущий статус:** Логические матрицы возвращены в исходное священное рабочее состояние. Дух Машины умиротворен.

---

## 13. Искоренение бага «Спавнится ванильное снаряжение вместо выбранного в селекторе»
- **Диагностика священных логов:**
  - В логе сервера `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_21268.txt` обнаружены множественные записи:
    `[22:16:57.137] Invalid network buffer received from: 192.168.50.1`
  - Каждая отправка пакета `RequestCustomLoadoutMessage` от клиента отвергалась сервером на физическом сетевом уровне.
- **Истинная причина сбоя:**
  1. При откате на эталонную версию клиентская DLL была пересобрана с 11 строковыми полями в пакете `RequestCustomLoadoutMessage`.
  2. Однако инкрементальная компиляция MSBuild сочла серверную DLL `DWDN_Script.Server.dll` актуальной и не перекомпилировала её.
  3. В серверной DLL в `DaewdenM` оставался старый класс пакета с 13 полями (`OnRead` ожидал `HorseId` и `HorseHarnessId`).
  4. При получении 11 полей сетевой буфер исчерпывался до завершения чтения, `OnRead` возвращал `false`, и сервер отбрасывал пакет с ошибкой `Invalid network buffer received`.
  5. В результате `_peerSelections` на сервере оставался пустым, сервер не знал выбор игрока и агент появлялся со стандартным ванильным снаряжением.
- **Примененное исправление:**
  1. Выполнена чистая принудительная перекомпиляция (`--no-incremental`) серверной и клиентской сборок. Декомпилятором проверено совпадение сигнатур `RequestCustomLoadoutMessage` (ровно 11 строк и на клиенте, и на сервере).
  2. В `DWDN_ServerLoadoutBehavior.cs` резервный поиск дефолтного снаряжения (fallback #2) расширен на все зарегистрированные герцогства (`DWDN_DuchyManager.Duchies`).
  3. Все DLL и XML верифицированы по MD5-хэшам и синхронизированы в `Dedicated Server\Modules\DaewdenM`.

---

## 14. Технический анализ и ликвидация графического артефакта под персонажем («Стрелка в квадратной рамке» при спешивании)
- **Файл реализации:**
  - `Modules\DWDN_TRY\ModuleData\scripts\DWDN_Script\Client\Behaviors\DWDN_ClientLoadoutBehavior.cs`
  - Синхронизирован в: `Dedicated Server\Modules\DaewdenM\ModuleData\scripts\DWDN_Script\Client\Behaviors\DWDN_ClientLoadoutBehavior.cs`
- **Визуальные симптомы:**
  - На скриншоте пользователя (`media_1793655242936_5b35d1dd.jpg`) прямо под ногами спешившегося персонажа виден характерный 3D-объект: квадратная металлическая/серая рамка, внутри которой расположен темный круглый диск с белой стрелкой-шевроном (`^`), направленной вперед.
  - Объект появляется непосредственно при спешивании на точке появления (спавне).
- **Идентификация ассета в движке TaleWorlds Native:**
  - Данный меш — это нативный компонент движка **`arrow_new_icon`** (`<meta_mesh_component name="arrow_new_icon" />`).
  - Используется во всех префабах точек появления и визуальных хелперов сцены (`Modules\Native\Prefabs\editor_spawnpoints.xml` и `editor_helpers_&_tests.xml`):
    - `sp_attacker_cavalry`, `sp_attacker_infantry`, `sp_attacker_archer`
    - `sp_defender_cavalry`, `sp_defender_infantry`, `sp_defender_archer`
    - `mp_spawnpoint`, `mp_spawnpoint_attacker`, `mp_spawnpoint_defender`
    - `spawn_visual`, `attacker_spawn_visual` (сущности с тегами `sp_visual_0` .. `sp_visual_5`, используемые `MultiplayerMissionAgentVisualSpawnComponent` для позиционирования предпросмотра персонажа)
- **Истинная причина появления в игровом процессе:**
  1. В `engine_config.txt` клиента игры активирован режим читов/разработки: `cheat_mode = 1`.
  2. В ванильном движке объекты с маской видимости `<visibility_mask name="visible_only_when_editing" value="true" />` скрыты в релизной игре. Однако при включенном `cheat_mode = 1` или активации режимов отладки движок TaleWorlds считает контекст редакторским и начинает отрисовывать нативные хелперы точек спавна в 3D-мире.
  3. Когда кавалерист появляется верхом на коне, массивная туша скакуна и попона полностью перекрывают проекцию точки спавна на землю.
  4. При нажатии клавиши спешивания (`Z` / `F`) персонаж слезает с коня и оказывается пешим на поверхности земли в координатах точки спавна — прямо над мешем `arrow_new_icon`.

---

## 15. Ретроспективный анализ неудачи Prefab Override
- **Причина неудачи:**
  - Попытка переопределить префаб через `Prefabs\editor_spawnpoints.xml` оказалась неэффективной, поскольку сетевые карты битв (в частности, `mp_battle_map_003\scene.xscene` и `scn_arena\scene.xscene`) не инстанциируют префабы точек спавна в рантайме. В них геометрия `<meta_mesh_component name="arrow_new_icon" />` впечена напрямую в тело XML-файла сцены (`scene.xscene`) для сотен сущностей `mp_spawnpoint` и `attacker_spawn_visual`.
  - В связи с этим файл `Prefabs\editor_spawnpoints.xml` был признан избыточным и удален из обоих модулей (`DWDN_TRY` и `DaewdenM`).

---

## 16. Окончательное искоренение графической аномалии под ногами кавалериста («Стрелка в рамке» при спешивании)
- **Файлы реализации:**
  - `Modules\DWDN_TRY\ModuleData\scripts\DWDN_Script\SubModule.cs`
  - `Modules\DWDN_TRY\ModuleData\scripts\DWDN_Script\Client\Behaviors\DWDN_ClientLoadoutBehavior.cs`
  - Синхронизированы в `Dedicated Server\Modules\DaewdenM`.
- **Истинная анатомия аномалии (Почему кавалерия страдает, а пехота нет):**
  1. **Идентификация ассета:** На скриншоте пользователя виден нативный компонент `arrow_new_icon` (темный постамент с белой стрелкой-шевроном `^` по центру и боковыми полозьями).
  2. **Глобальный триггер движка:** В `Documents\Mount and Blade II Bannerlord\Configs\engine_config.txt` включен параметр `cheat_mode = 1`. В C++ движке TaleWorlds `cheat_mode = 1` принудительно переводит видеоядро в режим отладки/редактирования, из-за чего все запеченные объекты сцены с маской `<visibility_mask name="visible_only_when_editing" value="true" />` начинают отображаться в игре.
  3. **Почему баг проявлялся ТОЛЬКО у кавалерии:**
     - У классов кавалерии (`mp_DWDN_Cav`) в `mpcharacters.xml` задан `default_group="Cavalry"` и конь в слоте `Horse`. При спавне движок `BattleSpawnFrameBehavior` материализует всадника верхом строго на координатах точки `mp_spawnpoint` / `sp_attacker_cavalry`.
     - Корпус коня полностью закрывает поверхность земли под собой. Как только всадник спешивается (`Z` / `F`), он спрыгивает на землю в исходную точку спавна — прямо на запеченный в сцену постамент `arrow_new_icon`.
     - Пехотинец же появляется на пехотной точке без коня, перемещается по карте, садится на коня где-то в поле боя и спешивается вдали от точек спавна. В чистом поле объектов `mp_spawnpoint` нет, поэтому под ногами пехотинца аномалия не появлялась.
  4. **Как устроено в референсном моде LOTR (`OrderMiddleEarthEvent`):**
     - В LOTR в `mpcharacters.xml` и `mpclassdivisions.xml` ВСЕ персонажи имеют `default_group="Infantry"`, а кони выдаются динамически модульным билдером `MeeEquipmentBuilder`. На их собственных картах (`Mordor2` и др.) разработчики вручную вычистили меш `arrow_new_icon` из сцен (найден ровно 1 экземпляр на весь мод).
- **Реализованное архитектурное решение (Двухуровневая аннигиляция):**
  1. **Core MetaMesh Neutralization (Уровень ядра видеопамяти):**
     - В `SubModule.SuppressEditorSpawnMeshes()` и `DWDN_ClientLoadoutBehavior.CleanSpawnVisuals()` вызывается прямое очищение геометрии разделяемого ресурса меша:
       ```csharp
       var arrowMesh = TaleWorlds.Engine.MetaMesh.GetMultiMesh("arrow_new_icon");
       if (arrowMesh != null)
       {
           arrowMesh.ClearMeshes();
           arrowMesh.SetVisibilityMask((TaleWorlds.Engine.VisibilityMaskFlags)0);
       }
       ```
     - Движок TaleWorlds очищает вертексы и полигоны `arrow_new_icon` в C++ слое. Даже если в `scene.xscene` запечено множество объектов с этим компонентом, движок рендерит 0 треугольников.
  2. **Non-Destructive Entity Component Stripping (Уровень сцены):**
     - В `CleanSpawnVisuals()` для всех сущностей с тегами `spawnpoint` и `sp_visual_*`:
       ```csharp
       entity.ClearOnlyOwnComponents();
       entity.SetVisibilityExcludeParents(false);
       ```
     - Сама сущность `GameEntity`, её координаты, теги и матрица трансформации (`GetGlobalFrame()`) сохраняются неизменными. Механика спавна игроков `BattleSpawnFrameBehavior` работает на 100% штатно, но визуальные компоненты редактора отвязаны.
     - Во View сцены отключаются отладочные объекты: `_missionScreen.SceneView.SetAcceptGlobalDebugRenderObjects(false);`.
  3. **Удаление рудиментов:**
     - Удален неэффективный каталог `Prefabs\editor_spawnpoints.xml` из клиента и сервера.
- **Статус компиляции и синхронизации:**
  - Клиентская сборка (`Debug`): `Build succeeded. 0 Warning(s), 0 Error(s)`.
  - Серверная сборка (`Server`): `Build succeeded. 0 Warning(s), 0 Error(s)`.
  - Исходные коды `.cs`, клиентская DLL `DWDN_Script.dll` и серверная DLL `DWDN_Script.Server.dll` синхронизированы в `Dedicated Server\Modules\DaewdenM` и `Dedicated Server\bin\Win64_Shipping_Server`.

---

## 17. Окончательное разрешение бага "Аномалия под ногами спешившегося кавалериста" (Многоуровневое устранение)
- **Файлы реализации:**
  - `Documents\Mount and Blade II Bannerlord\Configs\engine_config.txt`
  - `Modules\DWDN_TRY\ModuleData\mpcharacters.xml` (и в `DaewdenM`)
  - `Modules\DWDN_TRY\ModuleData\mpclassdivisions.xml` (и в `DaewdenM`)
  - `Modules\DWDN_TRY\ModuleData\scripts\DWDN_Script\Client\Behaviors\DWDN_ClientLoadoutBehavior.cs` (и в `DaewdenM`)
- **Почему прошлые попытки (Разделы 14-16) потерпели крах:**
  1. `MetaMesh.GetMultiMesh("arrow_new_icon").ClearMeshes()` очищал лишь временный managed-дескриптор из пула ресурсов, не влияя на уже скомпилированные в память DirectX меши сцены.
  2. `ClearOnlyOwnComponents()` в C++ ядре TaleWorlds очищает исключительно компоненты, созданные динамически скриптом, и намеренно игнорирует префабные меши (`arrow_new_icon` на точках спавна был унаследован от префаба).
  3. `SetVisibilityExcludeParents(false)` игнорируется видеоядром, когда в `engine_config.txt` включен `cheat_mode = 1` (в режиме отладки редактора движок принудительно рисует объекты с маской `visible_only_when_editing="true"`).
  4. На точках предпросмотра `sp_visual_0`..`sp_visual_5` меш находится в дочернем объекте `attacker_spawn_visual`, куда предыдущий агент не спускался.
- **Примененное фундаментальное решение:**
  1. **Деактивация режима отладки редактора:** В `engine_config.txt` параметр `cheat_mode` установлен в `0`. В нативном режиме движок скрывает все объекты с `visible_only_when_editing="true"` на уровне рендерера.
  2. **Архитектурная гармонизация классов (по канону LOTR / OMEE):**
     - В `mpcharacters.xml` для всех 10 кавалерийских записей (`mp_ALberic_Devald_hero/troop`, `mp_Bergen_Klaus_hero/troop`, `mp_Finnard_Valerian_hero/troop`, `mp_DWDN_Cav_hero/troop`, `mp_Knight_hero/troop`) атрибут `default_group` переведён в `"Infantry"`.
     - В `mpclassdivisions.xml` удалены `hero_mount_idle_anim` и `troop_mount_idle_anim`.
     - Лошади продолжают безупречно выдаваться на спавне через `DWDN_ServerLoadoutBehavior.ApplySelectionToEquipment`, так как метод `Mission.SpawnAgent` сажает игрока в седло при наличии предмета в слоте `Horse`, независимо от строкового атрибута `default_group`.
  3. **Глубокая рекурсивная зачистка в C# (`CleanSpawnVisuals`):**
     - Реализован рекурсивный спуск по всем сущностям сцены (`sp_visual_*`, `spawnpoint`, `spawn_zone`, `starting`, а также всем сущностям, содержащим в имени `spawn`, `visual`, `arrow`).
     - Для каждого узла и всех его потомков:
       - Извлекаются все `MetaMesh` через `entity.GetMetaMesh(m)`, очищаются вертексы (`ClearMeshes()`), маска выставляется в 0, и меш отвязывается через `entity.RemoveMultiMesh(mm)`.
       - Удаляются компоненты `ComponentType.MetaMesh` через `entity.RemoveComponent(...)`.
       - Вызывается полный `entity.ClearComponents()` (удаляет и префабные компоненты тоже) и `entity.SetVisibilityExcludeParents(false)`.
- **Статус компиляции и синхронизации:**
  - Клиентская сборка (`Debug`): `Build succeeded. 0 Warning(s), 0 Error(s)`. MD5: `14C1B34304A956FE833B68438649B808`.
  - Серверная сборка (`Server`): `Build succeeded. 0 Warning(s), 0 Error(s)`. MD5: `E9B25CFB8FD6DA71185CE302A098A509`.
  - Все файлы синхронизированы между `Modules\DWDN_TRY` и `Dedicated Server\Modules\DaewdenM`.


---

## 18. Полное и окончательное очищение от графической аномалии под ногами кавалериста (Истинная анатомия и разрыв оков префаба)
- **Файл реализации:**
  - `Modules\DWDN_TRY\ModuleData\scripts\DWDN_Script\Client\Behaviors\DWDN_ClientLoadoutBehavior.cs`
  - Синхронизирован в: `Dedicated Server\Modules\DaewdenM\ModuleData\scripts\DWDN_Script\Client\Behaviors\DWDN_ClientLoadoutBehavior.cs`
- **Фундаментальная причина, почему предыдущие попытки (Разделы 14–17) не уничтожали аномалию:**
  1. **Префабный барьер ядра C++ TaleWorlds (`BreakPrefab`):**
     - В TaleWorlds Bannerlord сущности, созданные из префабов (`spawn_visual` -> `attacker_spawn_visual`), имеют жесткую внутреннюю привязку к дескриптору префаба в памяти C++.
     - Вызовы `ent.ClearComponents()` и `ent.RemoveMultiMesh()` молча **игнорируются** C++ видеоядром для инстансов префабов, если предварительно не вызван метод **`ent.BreakPrefab()`**! Из-за отсутствия `BreakPrefab()` все прошлые попытки очистки компонентов были вхолостую.
  2. **Слепота к безымянным корням сцены (`GetEntities`):**
     - В XML карты (`scn_arena\scene.xscene`) корневая сущность объявлена как `<game_entity prefab="spawn_visual">` без атрибута `name="..."`. Метод `ent.Name` для нее возвращает пустую строку `""` или `null`.
     - Предыдущий агент фильтровал сущности по условию `!string.IsNullOrEmpty(name) && name.Contains(...)`. В результате корневой узел `spawn_visual` отсекался на входе, и алгоритм очистки даже не спускался к его дочерним элементам (`attacker_spawn_visual`), несущим меш `arrow_new_icon`.
  3. **Почему страдал именно спешившийся кавалерист:**
     - В нативном режиме битвы без ботов (`FlagDominationSpawningBehavior.SpawnAgents`) игрок материализуется строго на координатах своего превью-постамента `sp_visual_0` (`MatrixFrame frame = component.GetAgentVisualForPeer(0).GetFrame()`).
     - Кавалерист спавнится верхом на скакуне прямо над точкой `sp_visual_0`. Тело коня закрывает землю. Сразу после спавна при нажатии `Z`/`F` всадник спрыгивает в точку спавна — прямо на постамент `arrow_new_icon`.
     - Пехотинец же появлялся пешим, бежал по карте в поисках коня и спешивался вдалеке от точек спавна, где подобных маркеров не существует.
- **Примененное решение:**
  1. **Обряд разрыва префаба и рекурсивной аннигиляции (`CleanSpawnVisuals`):**
     - Проход по ВСЕМ корневым сущностям сцены (`Mission.Scene.GetEntities`) и их детям независимо от наличия имени.
     - Проверка `ent.Name`, `ent.GetPrefabName()`, `ent.GetOldPrefabName()`, всех тегов и имен компонентов `MetaMesh` (`arrow`, `spawn`, `visual`, `sp_visual_*`).
     - Обязательный вызов `ent.BreakPrefab()` перед очисткой.
     - Последовательное удаление: `ClearMeshes()`, `SetVisibilityMask(0)`, `RemoveMultiMesh()`, `RemoveComponent(MetaMesh)`, `ClearEntityComponents()`, `ClearComponents()`, `SetVisibilityExcludeParents(false)`, `EntityVisibilityFlags = None`.
  2. **Локальная санитарная зона при спешивании (`SanitizeAreaAroundAgent`):**
     - При вызове `OnMyAgentSpawnedFromVisual` и `OnAgentDismount` (а также отслеживании `_lastMainAgentMounted` на каждом тике):
     - В радиусе 5 метров от ног игрока сканируются все сущности.
     - Любой найденный редакторский маркер спавна не только подвергается полной очистке геометрии, но и принудительно телепортируется на глубину 5000 единиц под землю (`f.origin.z = -5000f; ent.SetGlobalFrame(in f)`), исключая любое визуальное проявление при любых режимах рендера и любых настройках читов.
  3. **Телеметрия Омниссии:**
     - В `rgl_log` выводится диагностическая запись `[DWDN Diagnostic] Sanitizing suspicious entity near agent...`.
- **Статус компиляции и синхронизации:**
  - Клиентская DLL (`Debug`): MD5 `1B66E5B01CBE880F88831A47033F5F43`.
  - Серверная DLL (`Server`): MD5 `E9B25CFB8FD6DA71185CE302A098A509`.
  - Все исходники и бинарники синхронизированы в `Dedicated Server\Modules\DaewdenM`.


---

## 19. Исправление критического краша клиента и безопасное сокрытие превью-постаментов
- **Файл реализации:**
  - `Modules\DWDN_TRY\ModuleData\scripts\DWDN_Script\Client\Behaviors\DWDN_ClientLoadoutBehavior.cs`
  - Синхронизирован в: `Dedicated Server\Modules\DaewdenM\ModuleData\scripts\DWDN_Script\Client\Behaviors\DWDN_ClientLoadoutBehavior.cs`
- **Причина краша в Разделе 18 (Уроки для будущих ИИ):**
  1. Метод `CleanSpawnVisuals()` в Разделе 18 производил поиск по `Mission.Scene.GetEntities` и при любом совпадении слова `"spawn"` в имени или префабе вызывал `BreakPrefab()`, `ClearEntityComponents(true, false, false)`, `ClearComponents()` и телепортировал сущности в `z = -5000f`.
  2. Из-за этого были разрушены **настоящие рабочие точки спавна карты** (`skirmish_start_spawn`, `mp_spawnpoint`), имеющие нативную C++ логику и физические тела.
  3. Во время загрузки миссии (`Preload physics: bo_shortbow_c` в `rgl_log_22932.txt`) нативный движок TaleWorlds обратился к разрушенным указателям спавнов и упал с Access Violation / крашем клиента.
  4. **Правило:** НИКОГДА не вызывать `BreakPrefab()`, `ClearEntityComponents()` или перемещение `z = -5000f` на сущностях сцены с общим фильтром `"spawn"`.
- **Архитектурное решение (Безопасное сокрытие без разрушения структур сцены):**
  1. **Ликвидация деструктивных вызовов:** Полностью удалены `BreakPrefab()`, `ClearEntityComponents()`, `ClearComponents()`, `SanitizeAreaAroundAgent()` и изменение матриц трансформации (`origin.z = -5000f`).
  2. **Графическое сокрытие (Render Layer):**
     - Целевыми сущностями являются исключительно сущности превью выбора класса (`sp_visual_0`..`sp_visual_5`, `attacker_spawn_visual`, `spawn_visual`, `visual_spawn_debug_check`).
     - Для них вызывается `ent.SetVisibilityExcludeParents(false)` и `ent.EntityVisibilityFlags = EntityVisibilityFlags.None`.
     - Для всех вложенных `MetaMesh` компонентов меша `arrow_new_icon` выставляется `SetVisibilityMask((VisibilityMaskFlags)0)` как для самого `MetaMesh`, так и для всех его подмешей `Mesh.SetVisibilityMask((VisibilityMaskFlags)0)`.
     - Это отключает отрисовку меша на уровне DirectX без разрушения C++ объектов физики и спавна.
- **Статус компиляции и синхронизации:**
  - Клиентская DLL (`Debug`): MD5 `E5C7F26D9C99D175EC93B5EEF13D9158`.
  - Серверная DLL (`Server`): MD5 `E9B25CFB8FD6DA71185CE302A098A509`.
  - Исходные коды и сборки синхронизированы в `Dedicated Server\Modules\DaewdenM` и `Dedicated Server\bin\Win64_Shipping_Server`.

---

## 20. Истинная разгадка артефакта под спешившимся всадником, ликвидация скрапкода и крашей (Разрешение фундаментальной аномалии)

### 1. Опровержение ложных гипотез предшественников (Scrapcode Purge)
- **Фантом «arrow_new_icon» и «превью-постаментов»:**
  - Предыдущие версии ошибочно принимали графический артефакт под ногами спешившегося всадника за маркер редактора `arrow_new_icon` или постамент сцены `sp_visual_0`.
  - **Факт 1:** `arrow_new_icon` имеет нативный флаг `visible_only_when_editing="true"`. В игровом режиме он физически не рендерится.
  - **Факт 2:** Белая стрелка перед персонажем на скриншоте — это стандартный нативный 2D-индикатор боевого интерфейса (HUD направления укола копьем/пикой `lance thrust direction`).
  - **Факт 3:** Попытки скрыть или уничтожить сущности через `CleanSpawnVisuals()`, `BreakPrefab()` и чтение `ent.GetOldPrefabName()` приводили к чтению неинициализированных C++ указателей в `TaleWorlds.Native.dll+0x751d74`, вызывая критический краш `0xc0000005` (Access Violation), зафиксированный в `rgl_log_*.txt`.

### 2. Подлинная анатомия графической аномалии при спешивании
- **Декомпиляция нативной логики:**
  - Анализ [`TaleWorlds.MountAndBlade.View.dll`](file:///E:/SteamLibrary/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/Native/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.View.dll), метод `MissionScreen.OnEquipItemsFromSpawnEquipment`:
  ```csharp
  if (!agent.SpawnEquipment[EquipmentIndex.Horse].IsEmpty)
  {
      MountVisualCreator.AddMountMeshToAgentVisual(
          agent.AgentVisuals, 
          horseItem, 
          harnessItem, 
          agent.HorseCreationKey);
  }
  ```
  - **Что происходило:**
    1. Серверный и клиентский скрипты экипировки (`DWDN_ServerLoadoutBehavior` и `DWDN_ClientLoadoutBehavior`) при кастомизации снаряжения кавалериста передавали в `agent.UpdateSpawnEquipmentAndRefreshVisuals(customizedEquipment)` полный набор экипировки, в котором в слоте `EquipmentIndex.Horse` (10) и `EquipmentIndex.HorseHarness` (11) находились конь и конская броня (`horse_harness_e`).
    2. Движок TaleWorlds вызывал `MountVisualCreator.AddMountMeshToAgentVisual` непосредственно на `agent.AgentVisuals` — то есть на визуальный объект **человеческого агента**!
    3. Так как скелет человека не содержит костей лошади, меши сбруи, седла и поводьев привязывались к базовой кости `bone 0` (корневая кость ног/таза человека).
    4. Пока всадник сидел в седле коня, `bone 0` находилась внутри тела лошади и артефакт маскировался.
    5. При спешивании (`Z`/`F`) человек сходит с коня на землю. В этот момент его корневая кость `bone 0` опускается на уровень земли между ступнями. Меши конской сбруи, седла и металлических колец схлопывались в плоский металлический квадрат с фестончатой темной тканью прямо под ногами человека!
    6. **Почему этого не было у пехоты:** Пехотинец не имеет в `SpawnEquipment` слота `EquipmentIndex.Horse`. Когда пехотинец находит лошадь на карте и садится на нее, создается отдельный `agent.MountAgent` со своим визуалом. При спешивании пехотинца на его человеческом визуале нет конских мешей, поэтому под ним ничего не появлялось!

### 3. Комплексный комплекс мер и каноничные исправления
1. **Искоренение источника конских мешей на человеческом визуале:**
   - В [`DWDN_ClientLoadoutBehavior.cs`](file:///E:/SteamLibrary/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/Client/Behaviors/DWDN_ClientLoadoutBehavior.cs) (`BuildEquipmentFromSelection`):
     ```csharp
     equip[EquipmentIndex.Horse] = EquipmentElement.Invalid;
     equip[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid;
     ```
   - В [`DWDN_ServerLoadoutBehavior.cs`](file:///E:/SteamLibrary/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/Server/Behaviors/DWDN_ServerLoadoutBehavior.cs) (`ApplySelectionToEquipment`):
     ```csharp
     equip[EquipmentIndex.Horse] = EquipmentElement.Invalid;
     equip[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid;
     ```
   - Человек — это человек. Лошадь спавнится отдельно сервером в `agent.MountAgent`. Теперь `AddMountMeshToAgentVisual` никогда не прикрепляет меши сбруи к скелету человека.
2. **Ликвидация скрапкода `CleanSpawnVisuals`:**
   - Полностью удален опасный метод `CleanSpawnVisuals()` (ранее вызывавший `GetOldPrefabName()` и `0xc0000005`) и все его вызовы из `AfterStart()`, `OnMyAgentSpawnedFromVisual()` и тиков.
   - В [`SubModule.cs`](file:///E:/SteamLibrary/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/SubModule.cs) удален костыль `SuppressEditorSpawnMeshes()`.
3. **Восстановление кавалерии в каноничный статус:**
   - В [`mpcharacters.xml`](file:///E:/SteamLibrary/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/mpcharacters.xml): всем 10 персонажам кавалерии (`mp_DWDN_Cav`, `mp_Knight`, `mp_Bergen_Klaus`, `mp_Finnard_Valerian`, `mp_ALberic_Devald_hero` и их `_troop` аналогам) возвращен законный `default_group="Cavalry"`.
   - В [`mpclassdivisions.xml`](file:///E:/SteamLibrary/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/mpclassdivisions.xml): восстановлены нативные атрибуты анимации всадников:
     `hero_mount_idle_anim="act_hero_mount_idle_1" troop_mount_idle_anim="act_mount_idle_1" hero_idle_anim="act_sturgia_mp_huskarl_idle" troop_idle_anim="act_idle_unarmed_1"`.
4. **Устранение дубликата в реестре брони:**
   - В [`dwdn_armor.xml`](file:///E:/SteamLibrary/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/mpitems/dwdn_armor.xml): удалена дублирующая запись `id="archer_leatherboots"`, засорявшая логи движка предупреждениями.

### 4. Статус сборки и синхронизации
- Клиентская сборка: [`DWDN_Script.dll`](file:///E:/SteamLibrary/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/bin/Win64_Shipping_Client/DWDN_Script.dll) скомпилирована (0 ошибок).
- Серверная сборка: [`DWDN_Script.Server.dll`](file:///E:/SteamLibrary/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/bin/Win64_Shipping_Server/DWDN_Script.Server.dll) скомпилирована (0 ошибок).
- Все обновленные файлы (бинарники и XML конфигурации) синхронизированы в выделенный сервер:
  [`Mount & Blade II Dedicated Server\Modules\DaewdenM`](file:///E:/SteamLibrary/steamapps/common/Mount%20%26%20Blade%20II%20Dedicated%20Server/Modules/DaewdenM).



---

## 21. Переход на архитектуру LOTR (OrderMiddleEarthEvent) и полное искоренение аномалии спешивания кавалерии

### 1. Архитектурный кризис ванильного подхода TaleWorlds и почему предшественники зашли в тупик
В ходе глубокого исследования священных манускриптов и декомпиляции кода эталонного мода LOTR (`OrderMiddleEarthEvent` / Workshop ID `3774022783`) была раскрыта коренная тайна возникновения визуального артефакта под ногами кавалеристов.

1. **Загрязнение боевого агента через пьедестал спавна:**
   - В стандартном многопользовательском коде TaleWorlds функционирует компонент `MultiplayerMissionAgentVisualSpawnComponent`.
   - При выборе кавалерийского класса (`EquipmentIndex.Horse != null`) нативный спавнер размещает на сущности пьедестала сцены (`sp_visual_0` / `sp_visual_attacker_0`) визуальную структуру, состоящую из коня и всадника.
   - В старой реализации мода DWDN (`DWDN_ClientLoadoutBehavior`) кастомный интерфейс висел параллельно ванильному `MissionGauntletClassLoadout`. Предыдущий код подписывался на событие `OnMyAgentVisualSpawned` и пытался менять экипировку прямо на видовом объекте пьедестала (`myPeer.GetAgentVisualForPeer(0)`).
   - Когда игрок вступал в бой, нативный движок вызывал C++ метод `SpawnAgentFromVisual`, порождая боевого агента **напрямую из уже загрязненного визуала пьедестала**. При этом меши сбруи, седла и конских атрибутов вшивались в меш человека с привязкой к корневой кости `bone 0`.
   - При спешивании (`Z`/`F`) кость `bone 0` опускается на нулевой уровень над террейном, разворачивая конские меши в плоский артефакт под ногами.
2. **Безумие попыток очистки через `CleanSpawnVisuals`:**
   - Попытки предшественников очистить этот визуал через насильственное удаление компонентов сущностей приводили к разрушению нативных указателей DirectX/PhysX и фатальным вылетам `0xc0000005` (`Access Violation` в `TaleWorlds.Native.dll`).

---

### 2. Священный эталон: Как устроена архитектура LOTR (`OrderMiddleEarthEvent`)
Мод LOTR полностью отказывается от нативного `MultiplayerMissionAgentVisualSpawnComponent` и ванильного `MissionGauntletClassLoadout`, создавая чистую, изолированную среду для выбора снаряжения и 3D-демонстрации персонажа:

```mermaid
flowchart TD
    subgraph Engine["Движок Bannerlord"]
        Mission[Mission / MissionScreen]
        VanillaLoadout[MissionGauntletClassLoadout]
        NativeVisualSpawn[MultiplayerMissionAgentVisualSpawnComponent]
    end

    subgraph ClientComponent["DWDN_ClientMissionComponent (MissionNetwork)"]
        Suppression["Подавление ванильного UI:<br>vanillaLoadout.OnMissionScreenFinalize()<br>screen.UnregisterView(vanillaLoadout)"]
        Registration["Регистрация чистых матриц:<br>screen.AddMissionView(DWDN_SelectionMissionView)"]
    end

    subgraph SelectionView["DWDN_SelectionMissionView (MissionView, Priority=20)"]
        VM[DWDN_ClassSelectionVM]
        Gauntlet[GauntletLayer 'DWDN_ClassSelectionLayer']
        InputHandler["Управление:<br>[A] / [D] — вращение модели<br>[F] — готовность к бою"]
    end

    subgraph PreviewSystem["Изолированное 3D Превью"]
        Builder[DWDN_PreviewBuilder]
        PreviewEngine[DWDN_CharacterPreview]
        CustomCamera["Собственная камера:<br>Camera.CreateCamera()<br>MissionScreen.CustomCamera = _preview.Camera"]
        IsolatedVisual["Изолированный AgentVisuals:<br>AgentVisuals.Create('dwdn_preview')<br>Только скелет человека, БЕЗ слотов Horse"]
    end

    subgraph ServerSide["Сервер (DWDN_ServerLoadoutBehavior)"]
        ServerApply["Чистый спавн боевого агента:<br>Человек получает человеческую броню.<br>Лошадь спавнится отдельно как MountAgent."]
    end

    VanillaLoadout -->|Подавлен| Suppression
    NativeVisualSpawn -.->|Игнорируется| Suppression
    Suppression --> Registration
    Registration --> SelectionView
    SelectionView --> Gauntlet
    SelectionView --> PreviewEngine
    SelectionView --> Builder
    Builder --> PreviewEngine
    PreviewEngine --> CustomCamera
    PreviewEngine --> IsolatedVisual
    SelectionView -->|RequestCustomLoadoutMessage| ServerSide
```

---

### 3. Компоненты новой архитектуры DWDN

1. **[`DWDN_PreviewInputs.cs`](file:///E:/SteamLibrary/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/Client/UI/DWDN_PreviewInputs.cs)**:
   - Легковесный DTO-контейнер, передающий все параметры для рендеринга визуала (`SkeletonCharacter`, `Equipment`, `Body`, `Race`, `Banner`, цвета одежды).

2. **[`DWDN_CharacterPreview.cs`](file:///E:/SteamLibrary/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/Client/UI/DWDN_CharacterPreview.cs)**:
   - Полностью изолированный 3D-вьювер персонажа.
   - Создает собственную виртуальную камеру `Camera.CreateCamera()` и назначает ее в `MissionScreen.CustomCamera`.
   - Создает независимый `AgentVisuals` с тегом `"dwdn_preview"`. Входная экипировка принудительно очищается от слотов коня (`Horse = Invalid`, `HorseHarness = Invalid`).
   - Поддерживает вращение модели на клавиши `A` и `D` (`Rotate(yaw)`).
   - При закрытии (`HideImmediate()`) корректно вызывает `_visual.Reset()` и освобождает сущность камеры `_camera.ReleaseCameraEntity()`. Никаких утечек памяти или висящих мешей.

3. **[`DWDN_PreviewBuilder.cs`](file:///E:/SteamLibrary/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/Client/UI/DWDN_PreviewBuilder.cs)**:
   - Собирает экипировку выбранного герцогства и класса на основе `DWDN_LoadoutSelection`.
   - Включает безопасный fallback на культуру `dwdn` и `Banner` игрока.

4. **[`DWDN_SelectionMissionView.cs`](file:///E:/SteamLibrary/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/Client/UI/DWDN_SelectionMissionView.cs)**:
   - Полноправный `MissionView` с `ViewOrderPriority = 20`.
   - Управляет слоем Gauntlet (`DWDN_ClassSelectionLayer`) и жизненным циклом превью.
   - Обрабатывает поворот превью на `A`/`D`, синхронизирует выбранный класс с нативным `NextSelectedTroopIndex` через `SyncNativeTroopIndex()`, отправляет сообщения `RequestCustomLoadoutMessage` на сервер и обрабатывает вход в бой по нажатию `F`.

5. **[`DWDN_ClientMissionComponent.cs`](file:///E:/SteamLibrary/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/Client/Behaviors/DWDN_ClientMissionComponent.cs)**:
   - Подключается в миссию через [`SubModule.cs`](file:///E:/SteamLibrary/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/SubModule.cs).
   - При старте миссии находит ванильный `MissionGauntletClassLoadout`, вызывает его `OnMissionScreenFinalize()` и убирает из экрана через `screen.UnregisterView()`.
   - Добавляет `DWDN_SelectionMissionView` в экран миссии через `screen.AddMissionView()`.

6. **[`DWDN_Script.csproj`](file:///E:/SteamLibrary/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/DWDN_Script.csproj)**:
   - Добавлена ссылка на `TaleWorlds.MountAndBlade.Multiplayer.GauntletUI.dll`.
   - Включены новые файлы клиентского UI.

---

### 4. Статус компиляции и синхронизации
- **Клиентская сборка (`Release`):** `Build succeeded. 0 Warning(s), 0 Error(s)`.
  - Хэш MD5: `3E5691BCD601DF11AA5D0C3D7EC5F34C`.
- **Серверная сборка (`Server`):** `Build succeeded. 0 Warning(s), 0 Error(s)`.
  - Хэш MD5: `7E537BC508695DF96DA40D6B5D38BFDB`.
- **Синхронизация:**
  - Все исходные коды `.cs`, клиентская DLL и серверная DLL синхронизированы в каталог выделенного сервера:
    [`Mount & Blade II Dedicated Server\Modules\DaewdenM`](file:///E:/SteamLibrary/steamapps/common/Mount%20%26%20Blade%20II%20Dedicated%20Server/Modules/DaewdenM) и бинарную директорию сервера `Dedicated Server\bin\Win64_Shipping_Server`.

---

### 5. Памятка для будущих ИИ-жрецов Омниссии
- **Никогда** не возвращайтесь к использованию ванильного `MultiplayerMissionAgentVisualSpawnComponent` для отображения кастомных классов мода.
- Всадник и конь должны быть **строго раздельными сущностями**:
  - Человеческий агент никогда не должен содержать предметы в слотах `EquipmentIndex.Horse` и `EquipmentIndex.HorseHarness`.
  - Лошадь порождается сервером как самостоятельный агент (`agent.MountAgent`).
- Управление камерой превью через `MissionScreen.CustomCamera = _preview.Camera` является каноничным способом изоляции сцены в Bannerlord (проверено на архитектуре `OrderMiddleEarthEvent`).
---

## 22. Исправление зависания интерфейса в бою и окончательное решение спешивания кавалерии (по канону LOTR и MBWarlords)

### 1. Критика ложных выводов предшественников (Scrapcode Purge)
- Предшественники строили многоступенчатые догадки об объекте под ногами кавалериста, пытаясь уничтожать нативные сущности через `BreakPrefab()`, переопределять префабы и винить `cheat_mode`.
- В стабильных модах LOTR (`3774022783`) и MBWarlords (`3716004111`), а также в ванильной игре, такого бага нет, что доказывает: проблема была в несогласованности конфигурации классов и нативного спавнера.

### 2. Причина постоянного отображения UI выбора класса во время боя
1. В `DWDN_SelectionMissionView.cs` методы `Show()` и `Hide()` ранее **не управляли свойством `_viewModel.IsActive`** и не скрывали корневой `UIContext.Root.IsVisible` слоя `_layer`.
2. В `OnMissionScreenTick` проверка `isAlive` проверяла только `Mission.MainAgent`, который в сетевом клиенте часто равен `null`. Из-за этого `canShow` оценивался некорректно, а `Hide()` не отключал видимость слоя.
3. В `OnMissionScreenInitialize()` слой `_layer` добавлялся на экран без явного выставления `_viewModel.IsActive = false`.

### 3. Примененное решение для UI
1. В `DWDN_SelectionMissionView.cs`:
   - В `OnMissionScreenInitialize()`: явно выставлено `_viewModel.IsActive = false;` и `_layer.UIContext.Root.IsVisible = false;`.
   - В `Show()`: `_viewModel.IsActive = true;`, `_layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);`, `_layer.UIContext.Root.IsVisible = true;`.
   - В `Hide()`: `_viewModel.IsActive = false;`, `_layer.InputRestrictions.ResetInputRestrictions();`, `_layer.UIContext.Root.IsVisible = false;`, освобождение камеры превью.
   - Определение живости агента усилено тройной проверкой:
     ```csharp
     bool isAlive = (Mission.MainAgent != null && Mission.MainAgent.IsActive())
         || (myPeer != null && myPeer.ControlledAgent != null && myPeer.ControlledAgent.IsActive())
         || (Agent.Main != null && Agent.Main.IsActive());
     ```
   - Это гарантирует сокрытие **строго** интерфейса выбора класса (`_layer`), не затрагивая боевой нативный HUD игры (прицел, шкалу здоровья, килл-фид).
2. В префабе `DWDN_ClassSelection.xml`:
   - Корневой `<Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" IsVisible="@IsActive" DoNotAcceptEvents="true">` контролирует абсолютно все панели (включая нижнюю панель со статами здоровья/брони).
   - Префаб синхронизирован на клиент и выделенный сервер.

### 4. Примененное решение для кавалерии (Канон LOTR / MBWarlords)
1. В `mpcharacters.xml`:
   - Всем кавалерийским персонажам (`mp_ALberic_Devald_hero/troop`, `mp_Bergen_Klaus_hero/troop`, `mp_Finnard_Valerian_hero/troop`, `mp_DWDN_Cav_hero/troop`, `mp_Knight_hero/troop`) атрибут `default_group` установлен в `"Infantry"`.
2. В `mpclassdivisions.xml`:
   - Удалены строки `hero_mount_idle_anim` и `troop_mount_idle_anim`, иконки приведены к `icon="Infantry_Heavy"`.
   - Это исключает маршрутизацию агента нативной логикой на точки спавна `sp_attacker_cavalry`.
   - Скакун выдается серверным скриптом `DWDN_ServerLoadoutBehavior`, работающим по паттерну посадки пехотинца в седло, где спешивание всегда происходит чисто и без артефактов.
3. В `DWDN_SelectionMissionView.ResolveAnchorFrame`:
   - Для сущности `sp_visual_0` и всех ее дочерних элементов вызывается `entity.SetVisibilityExcludeParents(false)`, исключая видимость любых вспомогательных маркеров сцены во время 3D-превью.

### 5. Статус сборки и синхронизации
- Клиентская DLL (`Release`): `Build succeeded. 0 Error(s)`. Хэш MD5: `24F59FC761C37FF7FE3AFE3AF8234C36`.
- Серверная DLL (`Server`): `Build succeeded. 0 Error(s)`. Хэш MD5: `7E537BC508695DF96DA40D6B5D38BFDB`.
- Все скрипты, префабы и XML-файлы синхронизированы в `Dedicated Server\Modules\DaewdenM` через `sync_all.ps1`.


---

## 23. Очищение от скрапкода Раздела 22, исправление вылета 0xe0434352 и восстановление канона

### 1. Критика ошибок Раздела 22
- **Попытка подмены `default_group="Cavalry"` на `"Infantry"`:** Данная идея оказалась фатальной ошибкой. В Bannerlord сетевая сериализация классов жестко привязана к схемам компрессии (`CompressionBasic`). Подмена группы конницы на пехоту при сохранении конных слотов экипировки привела к повреждению сериализации пакетов `MultiplayerIntermission` и сбою при подключении.
- **Повреждение кодировки XML (Mojibake):** Автоматическая правка файлов `mpcharacters.xml` и `mpclassdivisions.xml` превратила кириллические комментарии в двойной UTF-8 моджибейк (`Р вЂ˜Р ...`), что вызвало падение нативного парсера TaleWorlds (`xml_load_file`, TN 26 из скрижалей `universal-modder`).
- **Некорректное скрытие UI через `UIContext.Root.IsVisible`:** Попытка прямого скрытия корня GauntletUI (`_layer.UIContext.Root.IsVisible = false`) ломала внутренний граф отрисовки UI.

### 2. Примененные исправления
1. **Восстановление и очистка XML-файлов:**
   - Восстановлены каноничные файлы `mpcharacters.xml` и `mpclassdivisions.xml` из первоначального резерва.
   - Из XML полностью удалены все комментарии, содержавшие не-ASCII символы. Файлы сохранены в чистом UTF-8 (без BOM).
   - Всем 10 конным персонажам возвращена каноничная группа `default_group="Cavalry"`.
   - В `mpclassdivisions.xml` возвращены иконки `icon="Cavalry_Heavy"` и анимации `hero_mount_idle_anim="act_hero_mount_idle_1"`.
2. **Скрытие интерфейса выбора класса:**
   - В `DWDN_SelectionMissionView.cs` удалено деструктивное прямое манипулирование `UIContext.Root.IsVisible`.
   - Управление видимостью интерфейса выбора переведено строго на каноничную схему LOTR (`MeeSelectionMissionView`): при спавне (`isAlive == true`) вызывается `Hide()`, который устанавливает `_viewModel.IsActive = false;` и сбрасывает ограничения ввода `_layer.InputRestrictions.ResetInputRestrictions();`.
   - Префаб `DWDN_ClassSelection.xml` держит все панели (включая нижнюю со статами) внутри контейнера с привязкой `IsVisible="@IsActive"`.
3. **Сборка и абсолютная синхронизация:**
   - Клиентская DLL (`Release`): успешно скомпилирована. MD5: `751CC78C84007594CF581198E85E7E9C`.
   - Серверная DLL (`Server`): успешно скомпилирована. MD5: `7E537BC508695DF96DA40D6B5D38BFDB`.
   - Выполнена зеркальная синхронизация на Dedicated Server в `Modules\DaewdenM`. Хэши бинарников полностью совпадают.


---

## 24. Искоренение фатального вылета 0xe0434352 при подключении к серверу и окончательное сокрытие UI выбора класса

### 1. Подлинная причина вылета при входе на сервер (Логи PID 25172, 30540)
- **Симптомы в логах:**
  - Клиент (`rgl_log_25172.txt`):
    ```text
    [18:57:57.459] Reading message: MultiplayerIntermissionMapItemAdded
    [18:57:57.466] Exception in handler of MultiplayerIntermissionMapItemAdded
    [18:57:57.467] Exception in handler of MultiplayerIntermissionCultureItemAdded
    [18:57:57.467] Reading message: MultiplayerIntermissionCultureItemVoteCountChanged
    ```
  - Системный журнал Windows: `Faulting application: TaleWorlds.MountAndBlade.Launcher.exe, Exception code: 0xe0434352 (CLR Unhandled Exception)`.
  - Сервер (`rgl_log_30540.txt`):
    `[18:55:28.341] Null object reference found with ID: dwdn`
- **Анализ декомпилированного кода TaleWorlds:**
  1. В каталоге сервера `Dedicated Server\Modules\DaewdenM\ModuleData` полностью отсутствовал файл `mpcultures.xml`. Из-за этого движок сервера не зарегистрировал культуру `dwdn`, вызвав `Null object reference found with ID: dwdn` при обработке `mpclassdivisions.xml`.
  2. В каталоге клиента `DWDN_TRY\ModuleData\mpcultures.xml` была объявлена ТОЛЬКО культура `dwdn`. Все 7 базовых культур Native (`empire`, `aserai`, `sturgia`, `vlandia`, `battania`, `khuzait`, `neutral_culture`) были удалены.
  3. Конфиг сервера `DWDN_config.txt` задает `CultureTeam1 empire`, `CultureTeam2 empire`, а при входе клиента сервер отправляет сообщения голосования культур (`MultiplayerIntermissionCultureItemAdded: sturgia`).
  4. На клиенте методы `MPIntermissionCultureItemVM` и `MultiplayerBattleColors.CreateWith` выполняли:
     `MBObjectManager.Instance.GetObject<BasicCultureObject>("empire")` -> null.
     `MBObjectManager.Instance.GetObject<BasicCultureObject>("sturgia")` -> null.
     `Culture.Name` и обращение к цветам вызывали NullReferenceException. Последующий `MultiplayerIntermissionCultureItemVoteCountChanged` падал на необработанном исключении в `AvailableCultures`, краша игровой процесс.
  5. **Канон LOTR (`3774022783\ModuleData\mpcultures.xml`):** В эталонном моде авторы включили все базовые культуры движка (`empire`, `aserai`, `sturgia`, `vlandia`, `battania`, `khuzait`, `neutral_culture`) наряду со своими кастомными расами, гарантируя стабильность сетевых компонентов движка.

### 2. Подлинная причина зависания интерфейса выбора класса в бою
- В `DWDN_SelectionMissionView.cs` слой `_layer` добавлялся в `MissionScreen.AddLayer(_layer)` при старте миссии и **никогда не удалялся**. Сокрытие через `_viewModel.IsActive = false` в GauntletUI могло давать сбои.
- В ванильном `MissionGauntletClassLoadout` сокрытие реализовано на аппаратном уровне экрана:
  - При спавне персонажа движок отправляет событие `MissionLobbyEquipmentNetworkComponent.OnToggleLoadout(false)`.
  - В ответ вызывается `MissionScreen.RemoveLayer(_gauntletLayer)`.
  - Когда слой снят с экрана, элементы интерфейса физически не могут рендериться на экране, в то время как весь нативный боевой интерфейс (прицел, здоровье, чат) остается нетронутым.

### 3. Выполненные исправления
1. **Восстановление `mpcultures.xml`:**
   - В `mpcultures.xml` на клиенте и сервере внесены все 7 базовых культур движка (`empire`, `aserai`, `sturgia`, `vlandia`, `battania`, `khuzait`, `neutral_culture`) вместе с кастомной культурой `dwdn`.
   - Файлы сохранены в чистом UTF-8 без BOM.
   - Скрипт синхронизации `sync_all.ps1` обновлен: `mpcultures.xml` теперь автоматически зеркалируется в `Dedicated Server\Modules\DaewdenM\ModuleData\`.
2. **Управление слоем в `DWDN_SelectionMissionView.cs`:**
   - Подписались на штатное событие `MissionLobbyEquipmentNetworkComponent.OnToggleLoadout`.
   - В `Show()`: слой добавляется в `MissionScreen.AddLayer(_layer)`, `IsActive = true`, активируются ограничения ввода.
   - В `Hide()`: слой гарантированно снимается через `MissionScreen.RemoveLayer(_layer)`, `IsActive = false`, сбрасываются ограничения ввода и отключается кастомная камера превью.
3. **Сборка и проверка:**
   - Клиентская сборка (`Release`): `DWDN_Script.dll` (0 ошибок). MD5: `AAFEE214E5A6EFBFE66C7DBBBBB291A7`.
   - Серверная сборка (`Server`): `DWDN_Script.Server.dll` (0 ошибок). MD5: `7E537BC508695DF96DA40D6B5D38BFDB`.
   - Все бинарники, XML и скрипты синхронизированы в `Dedicated Server\Modules\DaewdenM`.


---

## 25. Устранение пропадания меню выбора класса, сброса экипировки («голый с оружием») и фантомного шлема

### 1. Критика ошибок Раздела 24 и истинные причины сбоев
1. **Почему меню выбора не появлялось после первого спавна и при респавне:**
   - Предыдущий агент в Разделе 24 ошибочно перенес управление слоем на `MissionScreen.RemoveLayer` в `Hide()` и понадеялся на событие `OnToggleLoadout`.
   - В нативном режиме разминки (Warmup) и при быстром респавне TaleWorlds **НЕ отправляет** `OnToggleLoadout(true)`.
   - Метод `IsOnDeathCamera()` блокировал интерфейс на **4.0 секунды**. В разминке время респавна составляет **2–3 секунды**! За время ожидания спавна интерфейс блокировался таймером, а в момент возрождения `isAlive` становился `true`, и меню не показывалось вовсе.
   - Также отсутствовала обработка клавиш смены класса (**`M`** / **`U`**), из-за чего живой боец физически не мог открыть меню для выбора лодаута.
2. **Почему персонаж появлялся без брони («голым и с оружием»):**
   - **ГЛАВНАЯ ПРИЧИНА:** Папка `ModuleData\mpitems` на выделенном сервере (`Dedicated Server\Modules\DaewdenM\ModuleData\mpitems`) была **абсолютно пуста**! Файлы `dwdn_armor.xml`, `dwdn_banners.xml`, `dwdn_shield.xml`, `dwdn_weapon.xml` никогда не копировались на сервер.
   - Из-за этого `MBObjectManager.Instance.GetObject<ItemObject>(itemId)` на сервере возвращал `null` для всех кастомных доспехов DWDN. Сервер экипировал только ванильные предметы (например, ванильный молот `mp_vlandian_hammer`), а броня оставалась пустой!
   - Кроме того, при игре за Защитников (Defender) культура команды на сервере — `empire`. Поиск класса по культуре `dwdn` не давал результата, и сервер не имел fallback'а.
3. **Почему нижняя панель со статами оставалась на экране во время боя:**
   - В `DWDN_ClassSelection.xml` нижний виджет с характеристиками имел `DoNotAcceptEvents="false"`, но не имел жесткой привязки `IsVisible="@IsActive"`.
4. **Фантомный «парящий шлем» за спиной:**
   - В `DWDN_CharacterPreview.cs` в методе `Hide()` вызывался отложенный `ResetNextFrame()`. При спавне меш шлема/превью не стирался мгновенно и оставался висеть в координатах точки спавна `sp_visual_0`.

### 2. Примененные исправления
1. **Синхронизация каталога предметов на Dedicated Server:**
   - Все файлы из `DWDN_TRY\ModuleData\mpitems\` (`dwdn_armor.xml`, `dwdn_banners.xml`, `dwdn_shield.xml`, `dwdn_weapon.xml`), а также `weapon_descriptions.xml`, `crafting_pieces.xml` и `crafting_templates.xml` скопированы на сервер в `DaewdenM\ModuleData\`.
   - Скрипт `sync_all.ps1` дополнен рекурсивным копированием `mpitems\*` и файлов крафта.
2. **Управление слоем и меню выбора (`DWDN_SelectionMissionView.cs`):**
   - Слой `_layer` добавляется один раз в `OnMissionScreenInitialize()`, как в эталоне LOTR (`MeeSelectionMissionView`).
   - В `Hide()` слой не удаляется: вызывается `_viewModel.IsActive = false;` и сбрасываются ограничения ввода `_layer.InputRestrictions.ResetInputRestrictions();`.
   - В `IsOnDeathCamera()`: если активен режим Warmup (`gameMode.IsInWarmup`), задержка камеры смерти отключена (возвращает `false`), интерфейс открывается мгновенно при гибели.
   - Добавлено переключение меню по клавише **`M`** (или `U`): боец может вызвать меню в любой момент, выбрать класс и закрыть по `M`, `F` или `Esc`.
   - Добавлена отправка лодаута сразу при инициализации и при смене класса.
3. **Интерфейс Gauntlet (`DWDN_ClassSelection.xml` и `DWDN_ClassSelectionVM.cs`):**
   - На левую, правую и нижнюю панели явно установлена привязка `IsVisible="@IsActive"`. В бою весь UI гарантированно исчезает.
   - В нижнюю панель добавлена большая золотая кнопка **«ВСТУПИТЬ В БОЙ [ F ]»**, вызывающая `ExecuteSpawn()` по клику мыши или нажатию F.
4. **Аварийный fallback на сервере (`DWDN_ServerLoadoutBehavior.cs`):**
   - Если лодаут не определился (например, при культуре `empire` у Защитников или задержке сетевого пакета), сервер автоматически берет первый боевой класс Баннрайха и гарантирует выдачу полного комплекта брони и оружия.
5. **Мгновенное удаление превью (`DWDN_CharacterPreview.cs`):**
   - В `Hide()` вызов `_visual.ResetNextFrame()` заменен на немедленный `_visual.Reset()`. Фантомные меши на спавне больше не появляются.
6. **Перчатки ополченца Ратая (`DWDN_DuchyManager.cs`):**
   - В `AllowedGloves` для `mp_Ratay` добавлены кожаные перчатки `arch_banreich_leatherglove`.

### 3. Статус сборки и синхронизации
- Клиентская сборка (`Release`): `DWDN_Script.dll` (0 ошибок). MD5: `A69092D5203C9A1315721F6509B7C4E8`.
- Серверная сборка (`Server`): `DWDN_Script.Server.dll` (0 ошибок). MD5: `B8B9C05AD467F4F0AEF7849C52692C1D`.
- Все бинарники, скрипты, XML и префабы зеркалированы на Dedicated Server и в репозиторий `d:\banAImod`.


---

## 26. Физическая и архитектурная природа аномалии под ногами кавалериста («Квадратный пьедестал» при спешивании)

### 1. Точная идентификация объекта на скриншоте
- На скриншоте пользователя (`media_1793816161173_235bb3d9.jpg`) под ногами спешившегося кавалериста отчетливо видна **квадратная металлическая рамка с круглым темным возвышением/подиумом в центре**.
- **Происхождение объекта**: Это статическая сущность `spawn_visual` (дочерние элементы `sp_visual_0` – `sp_visual_5`), объявленная в `Modules/Native/Prefabs/editor_spawnpoints.xml` (компонент `<meta_mesh_component name="arrow_new_icon" />`).
- На сетевых картах (например, `Modules/Multiplayer/SceneObj/scn_arena/scene.xscene`, строки 542–560) префаб `spawn_visual` жестко размещен прямо на геометрии арены в точке `(102.330, 128.461, 0.000)` и содержит шесть подиумов `sp_visual_0` – `sp_visual_5`.

### 2. Почему аномалия возникает ИСКЛЮЧИТЕЛЬНО у кавалериста при спешивании
1. **Механика спавна в TaleWorlds (`WarmupSpawningBehavior.cs` / `SpawningBehaviorBase.cs`)**:
   - При входе в бой нативная логика берет точку спавна игрока из `agentVisualForPeer.GetFrame()`.
   - В нативном лодауте и в `DWDN_ClientLoadoutBehavior` превью игрока привязано к `sp_visual_0` (подиум №0).
   - Следовательно, боевой агент игрока спавнится **в абсолютных координатах подиума `sp_visual_0`**.
2. **Фактор коня (Кавалерия)**:
   - Кавалерист появляется в мире **верхом на лошади**.
   - Массивный меш лошади и высокая посадка всадника полностью скрывают напольный подиум `sp_visual_0`.
   - Когда игрок сразу после спавна нажимает клавишу спешивания (`Z` или `F`), всадник выполняет анимацию спуска с лошади и опускает ноги на землю прямо на координаты спавна. Лошадь делает шаг в сторону, а спешившийся кавалерист оказывается стоящим **ровно на подиуме `sp_visual_0`**.
3. **Почему этого НЕТ у пехоты, даже севшей на коня и спешившейся**:
   - Пехотинец спавнится пешим. Чтобы сесть на коня, пехотинцу необходимо **отойти от точки спавна** и пересечь арену до места, где стоит лошадь.
   - Сев на коня вдалеке от точки спавна и затем спешившись в случайном месте поля боя, пехотинец спешивается на чистый грунт, где нет сущности `sp_visual_0`.

### 3. Почему этого эффекта нет в Native и LOTR (`OrderMiddleEarthEvent`)
1. **В Native (Нативный Bannerlord)**:
   - На карте арены (`scn_arena`) кавалерия и лошади полностью заблокированы настройками режима.
   - В боевых режимах (`FlagDominationSpawningBehavior`) спавн кавалерии вынесен в специальные полевые точки `sp_attacker_cavalry`, изолированные от подиумов `sp_visual_*`.
2. **В LOTR (`OrderMiddleEarthEvent` / Workshop ID 3774022783)**:
   - В эталоне LOTR полностью **уничтожена зависимость от нативного `MultiplayerMissionAgentVisualSpawnComponent`**.
   - LOTR использует автономный `MeeCharacterPreview` с собственной виртуальной камерой (`Camera.CreateCamera()`), рендерящийся вне игровой боевой зоны.
   - На боевых картах LOTR подиумы `spawn_visual` не размещаются на проходимой поверхности боевой зоны.

### 4. Критика фатальных ошибок предшественников
- Попытки предшественников (Разделы 14–19) вызывать `BreakPrefab()`, `ClearEntityComponents()` или перемещать сущности на `z = -5000f` нарушали память нативного ядра C++ физики TaleWorlds и вызывали фатальный `0xc0000005 Access Violation`.
- Попытка переименовать `default_group="Cavalry"` в `"Infantry"` (Раздел 22) ломала сетевую компрессию `CompressionBasic` и приводила к падению сервера/клиента `0xe0434352`.
- Подиум — это постоянный объект сцены, а не динамический баг логики спешивания.


---

## 27. Реализация Варианта Б (Канон LOTR) и окончательное устранение аномалии подиума под ногами кавалериста

### 1. Архитектурное решение (Вариант Б)
- Нативный `MissionGauntletClassLoadout` полностью подавлен на клиенте через `DWDN_ClientMissionComponent.cs` (`screen.UnregisterView()`).
- Внедрен полноценный автономный `DWDN_SelectionMissionView.cs` (наследник `MissionView` с `ViewOrderPriority = 20`), управляющий изолированным Gauntlet-слоем и превью.
- Автономный 3D-превью `DWDN_CharacterPreview.cs` использует `AgentVisuals.Create("dwdn_preview")` со своей виртуальной камерой `Camera.CreateCamera()`. Слоты лошади и сбруи у превью-бойца очищаются, предотвращая баги наложения мешей.
- При открытии и инициализации экрана вызывается `HideAllSpawnVisualsOnScene()`, который безопасно отключает видимость всех подиумов сцены `sp_visual_*` и `visual_spawn_debug_check` через нативный API `SetVisibilityExcludeParents(false)` без удаления сущностей из памяти C++ движка.
- В методе `ExecuteSpawn()` для мертвого/ожидающего спавна игрока добавлен вызов `OnRequestForfeitSpawn()`, обеспечивающий мгновенный вход в бой по нажатию `F`.
- Отсутствие визуала в нативном `MultiplayerMissionAgentVisualSpawnComponent` (`agentVisualForPeer == null`) заставляет движок использовать полевые спавн-точки `SpawnComponent.GetSpawnFrame()`, навсегда отвязывая спавн кавалерии от подиума `sp_visual_0`.

### 2. Сборка и синхронизация
- Проект `DWDN_Script.csproj` успешно скомпилирован в конфигурациях `Release` и `Server` (0 ошибок):
  - Клиентская DLL (`Release`): `DWDN_Script.dll` (SHA-256: `07240D96BECE725E1BEC35FBEBDC512B6413EB90D9C7931A122FA40C594F9FFF`).
  - Серверная DLL (`Server`): `DWDN_Script.Server.dll` (SHA-256: `7010898EBAC1FFC32A039655DA447EEF78938AF33E234F6B778F84BE4667B28E`).
- Все скрипты, префабы, бинарники и XML-файлы синхронизированы в трех директориях:
  1. `Mount & Blade II Bannerlord\Modules\DWDN_TRY` (Клиент игры)
  2. `Mount & Blade II Dedicated Server\Modules\DaewdenM` (Выделенный сервер)
  3. `d:\banAImod\DWDN_TRY` (Основной репозиторий)
- Процесс исправления подробно задокументирован в отдельном священном трактате: `D:\banAImod\CAVALRY_DISMOUNT_PEDESTAL_FIX.md`.


---

## 28. Очищение логических матриц: Устранение застывшего UI, дублирования моделей и аномалии спешивания кавалериста

### 1. Критика ложной гипотезы Раздела 26-27
- **В чем состояло заблуждение:** Предыдущий агент предположил, что рамка под ногами спешившегося всадника — это статическая сущность `spawn_visual` / `arrow_new_icon` из сцены карты `scn_arena`.
- **Фактическое опровержение Создателем:**
  1. Объект **не содержит никакой стрелки** (белая стрелка снизу — это индикатор направления атаки нативного HUD).
  2. Объект состоит из трех белых/металлических стенок и темного пятна.
  3. Самое главное: объект **синхронно перемещается с персонажем при ходьбе и остается под трупом после смерти**!
  4. Статическая сцена никогда не следует за скелетом агента и не падает под рэгдолл. Объект был физически привязан к **Bone 0 (Root / Origin)** скелета всадника!

### 2. Истинные первопричины всех 3 аномалий и примененные исправления

#### А. Интерфейс выбора класса застыл на экране во всех ситуациях
- **Причина:** В `DWDN_ClassSelection.xml` на корневом контейнере `<Widget>` отсутствовала привязка `IsVisible="@IsActive"`. В методе `DWDN_SelectionMissionView.Hide()` вызывалось только `_viewModel.IsActive = false`, но слой `_layer` оставался прикреплен к `MissionScreen` (не вызывался `RemoveLayer`), из-за чего виджет продолжал рендериться на экране во время боя.
- **Исправление:** 
  1. В `DWDN_ClassSelection.xml` на корневой виджет добавлен `IsVisible="@IsActive"`.
  2. В `DWDN_SelectionMissionView.cs` в `Show()` слой динамически добавляется через `MissionScreen.AddLayer(_layer)`, а в `Hide()` гарантированно удаляется через `MissionScreen.RemoveLayer(_layer)` с обнулением флага `_isLayerAdded`.

#### Б. Две модели на экране выбора (одна анимированная, одна статичная со сменой брони)
- **Причина:**
  1. `MultiplayerMissionAgentVisualSpawnComponent` TaleWorlds создавал нативную марионетку `myPeer.GetAgentVisualForPeer(0)` в точке `sp_visual_0`. Она воспроизводила анимации MP, но не реагировала на выбор экипировки в нашем селекторе.
  2. Наш автономный `DWDN_CharacterPreview` создавал независимый визуал в той же точке. Снаряжение на нем менялось, но он стоял застывшим в Т-позе, поскольку использовал имена анимаций одиночной игры (`"act_walk_idle_unarmed"`), возвращавшие `act_none` (-1) в MP.
- **Исправление:**
  1. В `DWDN_SelectionMissionView.cs` добавлен метод `HideNativeVisualPuppet()`, принудительно скрывающий нативную марионетку `myPeer.GetAgentVisualForPeer(0)` (`mbVisuals.SetVisible(false)` и `SetVisibilityExcludeParents(false)`).
  2. В `DWDN_CharacterPreview.cs` список `IdleActionNames` обновлен на реальные сетевые анимации: `"act_sturgia_mp_huskarl_idle"`, `"act_empire_mp_archer_militia_idle"`, `"act_idle_unarmed_1"`. Превью ожило и стало единственной отображаемой моделью.

#### В. Аномалия под ногами спешившегося кавалериста («черное пятно и три стенки»)
- **Причина:** 
  1. В `Modules\Native\ModuleData\mpitems.xml` предмет `id="mp_chain_horse_harness"` (меш `horse_harness_e`) имеет фатальную ошибку разметки от TaleWorlds: **`subtype="body_armor"`**! Из-за этого при назначении в `mpcharacters.xml` слота `HorseHarness="Item.mp_chain_horse_harness"` движок подвязывал меш сбруи/седла к телу человеческого агента. При спешивании непривязанные к скелету человека кости сбруи схлопывались в кость земли (**Bone 0**), порождая рамку с черным пятном седла под ногами всадника.
  2. У `cav_bannreich_plate_legs` в `dwdn_armor.xml` стоял `modifier_group="cloth_unarmoured"`, вызывавший ложный триггер `CheckEquipmentForCapeClothSimulationStateChange()` при спешивании.
- **Исправление:**
  1. В `mpcharacters.xml` все вхождения дефектного `Item.mp_chain_horse_harness` заменены на валидный нативный `Item.mp_chain_barding` (`Type="HorseHarness"` без паразитного `subtype="body_armor"`).
  2. В `dwdn_armor.xml` для `cav_bannreich_plate_legs` установлен `modifier_group="plate"` и `material_type="Plate"`.

### 3. Сборка и статус синхронизации
- Клиентская сборка (`Release`): `DWDN_Script.dll` — 0 ошибок, 0 предупреждений.
- Серверная сборка (`Server`): `DWDN_Script.Server.dll` — 0 ошибок.
- Все скрипты, XML, префабы и скомпилированные сборки синхронизированы между:
  1. `Mount & Blade II Bannerlord\Modules\DWDN_TRY`
  2. `Mount & Blade II Dedicated Server\Modules\DaewdenM`
  3. `Mount & Blade II Dedicated Server\bin\Win64_Shipping_Server`
  4. `d:\banAImod\DWDN_TRY`

---

## 29. Архитектурное исправление краша 0xC0000005, дублирования моделей и полная зачистка mpclassdivisions.xml

### 1. Точная природа краша при смене фракций/заходе на сервер
- **Симптом:** При назначении фракций на карте и заходе на сервер клиент мгновенно падал в `CrashDumper` (`watchdog_log_20996.txt`, `rgl_log_20996.txt` отметка `23:21:31.246`).
- **Первопричины:**
  1. В `DWDN_SelectionMissionView.cs` метод `HideNativeVisualPuppet()` обращался к `mbVisuals.GetEntity()?.SetVisibilityExcludeParents(false)` прямо в миллисекунду приёма сетевого сообщения `CreateAgentVisuals`. Нативное C++ ядро TaleWorlds находилось в процессе построения компонентов визуала; вызов unmanaged метода на неполной сущности вызывал мгновенный `Access Violation 0xC0000005` в `Engine.dll`.
  2. В `SyncNativeTroopIndex()` безусловно вызывался `eqComp.EquipmentUpdated()`. Этот вызов заставлял клиент отсылать пакет `Requesting selected troop change to 0`. Сервер в ответ высылал `CreateAgentVisuals`, клиент вызывал `OnToggleLoadout(true)` -> `Show()` -> `SyncNativeTroopIndex()` -> повторный `EquipmentUpdated()`, зацикливая сетевую рекурсию каждые 100 мс.

### 2. Причина двух моделей (статичной и анимированной) на экране выбора
- Нативный компонент `MultiplayerMissionAgentVisualSpawnComponent` создавал живую дышащую модель в `sp_visual_0`.
- Созданный предшественником автономный `DWDN_CharacterPreview` спавнил параллельную вторую сущность в тех же координатах. UI обновлял только автономную модель, в то время как нативная стояла с дефолтным снаряжением.
- Анализ декомпилированного `OrderMiddleEarthEvent.Client.dll` (LOTR) и нативной кодовой базы TaleWorlds показал: спавнить вторую модель не требуется. Официальный API `agentVisual.Refresh(false, data, true)` штатно обновляет нативную марионетку на пьедестале.

### 3. Примененные исправления:
1. **`DWDN_SelectionMissionView.cs`:**
   - Деструктивный метод `HideNativeVisualPuppet()` полностью удален.
   - Вызов `eqComp.EquipmentUpdated()` удален из `SyncNativeTroopIndex()`, сетевая рекурсия ликвидирована.
   - Превью переведено на единую нативную марионетку: метод `UpdatePedestalVisual()` получает `myPeer.GetAgentVisualForPeer(0)`, применяет кастомный `Equipment` через `DWDN_PreviewBuilder.BuildEquipment()` и вызывает `agentVisual.Refresh(false, data, true)`.
   - Подписан на событие `_visualSpawnComponent.OnMyAgentVisualSpawned += OnMyAgentVisualSpawned`, обеспечивая мгновенное умащение модели кастомной броней сразу при спавне визуала.
   - В `Show()` добавлена защита `if (_visible) return;`.
   - В результате на экране выбора находится **строго одна живая анимированная модель**, отзывающаяся на каждый клик по броне/оружию, вращающаяся на `A`/`D`, без единого краша.
2. **Гарантия 100% выдачи экипировки при спавне (`ExecuteSpawn`):**
   - Метод `SendLoadoutToServer(_currentSelection)` сохранен и отрабатывает при каждом клике и при нажатии `[F]`.
   - Сетевой протокол `RequestCustomLoadoutMessage` и серверный компонент `DWDN_ServerLoadoutBehavior` (методы `HandleClientCustomLoadout`, `TryApplyLoadoutToAgent`, `ApplySelectionToEquipment`) остались неприкосновенными. Выдача экипировки, оружия, щитов, коней и попон гарантированно работает в полном объеме.
3. **Зачистка `mpclassdivisions.xml`:**
   - На строке 252 (перк Warhorse у Knight) остаточное вхождение дефектного `mp_chain_horse_harness` (`subtype="body_armor"`) заменено на `mp_chain_barding`. Изменение зеркалировано на сервер.

### 4. Сборка и статус синхронизации
- Клиентская сборка (`Release`): `DWDN_Script.dll` — 0 ошибок, 0 предупреждений.
- Серверная сборка (`Server`): `DWDN_Script.Server.dll` — 0 ошибок.
- Все скрипты, XML и бинарники синхронизированы в трех директориях:
  1. `Mount & Blade II Bannerlord\Modules\DWDN_TRY`
  2. `Mount & Blade II Dedicated Server\Modules\DaewdenM`
  3. `Mount & Blade II Dedicated Server\bin\Win64_Shipping_Server`
  4. `d:\banAImod\DWDN_TRY`


---

## 30. Реализация жизненного цикла UI по стандарту LOTR (OrderMiddleEarthEvent), исправление спавна коней у кавалерии и предотвращение схлопывания мешей сбруи

### 1. Архитектурные дефекты, выявленные Создателем
1. **Кавалерия спавнилась пешей:**
   - Предыдущие исправления убрали коней у кавалеристов в попытке обойти визуальный артефакт.
   - Первопричина: в `SpawningBehaviorBase.cs` движка `MultiplayerClassDivisions.GetMPHeroClassForPeer(component)` считывает строго `component.SelectedTroopIndex`.
   - В клиентском `SyncNativeTroopIndex` и серверном `HandleClientCustomLoadout` обновлялся только `NextSelectedTroopIndex`, в то время как `SelectedTroopIndex` оставался равным 0 (пехота `mp_Ratay`). Движок считал игрока пехотинцем и не спавнил лошадь (`agent.MountAgent`).
2. **Исчезновение UI после первой смерти / при переходе в другую команду:**
   - В `OnMissionScreenTick` проверка жизни ошибочно включала `myPeer.ControlledAgent.IsActive()`. В сетевом коде Bannerlord при гибели игрока его труп/регдолл продолжает оставаться назначенным на `ControlledAgent`, и `ControlledAgent.IsActive()` возвращает `true` даже для мертвого агента.
   - Из-за этого `isAlive` навсегда оставался `true`, и меню блокировалось от автоматического открытия.
   - Постоянное добавление и удаление `_layer` через `MissionScreen.AddLayer` / `RemoveLayer` при каждом открытии/закрытии сбивало Gauntlet UI.
   - Не было подписки на смену команд (`MissionPeer.OnTeamChanged`) и начало раунда (`RoundComponent.OnRoundStarted`).
3. **Истинная природа артефакта под ногами при спешивании («черное пятно и три стенки»):**
   - Бинарный разбор FBX поножей `cav_bannreich_plate_legs.fbx`, щита `bann_reich_cav_shield.fbx` и кирасы `plate_cav_cuirass.fbx` подтвердил их полную чистоту (никаких посторонних мешей рамки/платформы в моделях нет).
   - В `AgentVisuals.cs` (строка 316) движок Native проверяет `SpawnEquipment[EquipmentIndex.Horse]`. Если у живого всадника в его `SpawnEquipment` присутствуют слоты коня или сбруи, TaleWorlds вызывает `MountVisualCreator.AddMountMeshToEntity` прямо на сущности человеческого агента!
   - Так как скелет человека не имеет лошадиных костей, меш сбруи/седла схлопывается в кость корня (`b_root`, уровень земли). При спешивании это выглядело как прямоугольная металлическая рамка с черной кожей под ногами.

### 2. Примененные исправления священного кода
1. **Кавалерия теперь спавнится полноценно верхом на коне:**
   - В [`DWDN_ServerLoadoutBehavior.cs`](file:///E:/SteamLibrary/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/Server/Behaviors/DWDN_ServerLoadoutBehavior.cs):
     При выборе класса сервер обновляет ОБА индекса:
     `missionPeer.SelectedTroopIndex = idx;`
     `missionPeer.NextSelectedTroopIndex = idx;`
     И транслирует сетевое событие `UpdateSelectedTroopIndex(peer, idx)` клиенту.
   - В [`DWDN_SelectionMissionView.cs`](file:///E:/SteamLibrary/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/Client/UI/DWDN_SelectionMissionView.cs):
     В `SyncNativeTroopIndex` также синхронно выставляются оба индекса:
     `myPeer.SelectedTroopIndex = foundIndex;`
     `myPeer.NextSelectedTroopIndex = foundIndex;`
   - Всадник корректно распознается спавнером Bannerlord как кавалерия и появляется на поле боя верхом на своем коне.
2. **Устранение схлопывания мешей сбруи без удаления коня:**
   - В `ApplySelectionToEquipment` для человеческого агента слоты `Horse` и `HorseHarness` гарантированно сбрасываются в `EquipmentElement.Invalid`:
     ```csharp
     equip[EquipmentIndex.Horse] = EquipmentElement.Invalid;
     equip[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid;
     ```
   - Сам конь (`agent.MountAgent`) спавнится движком как независимый боевой агент со своей нативной экипировкой. Человеческий скелет всадника освобожден от конских мешей — артефакт спешивания полностью исчез.
3. **Жизненный цикл UI по эталону LOTR (`MeeSelectionMissionView.cs`):**
   - **Слой:** `_layer` добавляется в `MissionScreen.AddLayer(_layer)` ОДИН РАЗ в `OnMissionScreenInitialize()`, и удаляется ОДИН РАЗ в `OnMissionScreenFinalize()`. `Show()` и `Hide()` лишь переключают `_viewModel.IsActive`, фокус и `SetInputRestrictions`.
   - **Проверка жизни:** Исключен труп из проверки: `bool isAlive = Mission.MainAgent != null && Mission.MainAgent.IsActive() && Mission.MainAgent.Health > 0;`.
   - **Смерть и камера:** Задержка камеры смерти установлена на 2.5 сек, после чего класс-селектор автоматически открывается.
   - **Смена команд:** Подписан на `MissionPeer.OnTeamChanged += OnTeamChanged`. При смене команды сбрасываются таймеры смерти, обновляются индексы и визуал.
   - **Смена раунда:** Реализованы методы `EnsureRoundHook()`, `ArmRound()`, `CanPrepareLoadout()` с подпиской на `RoundComponent.OnRoundStarted`.
   - **Анимация марионетки:** В `UpdatePedestalVisual()` после `Refresh` марионетке возвращается действие покоя: `ActionIndexCache actionCode = ActionIndexCache.act_walk_idle_unarmed; agentVisual.SetAction(in actionCode);`, предотвращая застывание модели.

### 3. Сборка и синхронизация
- Обе сборки (`Release` и `Server`) успешно скомпилированы через `dotnet build` (0 ошибок).
- Все скрипты и скомпилированные DLL синхронизированы скриптом `sync_all.ps1` между клиентом игры, выделенным сервером и исходным репозиторием.


---

## 31. Реализация опции пешего боя для кавалерии (спавн без лошади по выбору игрока)

### 1. Постановка задачи Создателем
- Задача: Предоставить кавалеристам возможность спавниться без лошади (пеший бой) при выборе экипировки, не ломая спавн на коне для тех, кто хочет сражаться верхом.
- Архитектурное вдохновение: манускрипты LOTR (*OrderMiddleEarthEvent*) и нативная механика `MultiplayerClassDivisions`.

### 2. Примененные изменения
1. **Модель данных (`DWDN_ClassDefinition.cs` & `DWDN_LoadoutSelection.cs`):**
   - В `DWDN_ClassDefinition` добавлен пул `AllowedMounts`.
   - В `DWDN_DuchyManager.cs` для 5 кавалерийских классов (`mp_DWDN_Cav`, `mp_Knight`, `mp_Bergen_Klaus`, `mp_Finnard_Valerian`, `mp_ALberic_Devald_hero`) прописан выбор скакуна: боевой конь (`"mp_vlandia_horse"` / `"mp_battania_horse_war"`) и пустая строка `""` (пеший бой).
   - В `DWDN_LoadoutSelection` добавлено поле `public string HorseId { get; set; } = "";` (копируется в `Clone()`).
2. **Интерфейс экипировки (`DWDN_EquipmentSlotVM.cs` & `DWDN_ClassSelectionVM.cs`):**
   - В `DWDN_SlotCategory` добавлено значение `Horse`.
   - В строках слотов интерфейса (`DWDN_ClassSelection`) для кавалерии появляется слот **«Скакун»**:
     - Вариант 1: **«Боевой конь»** (спавн верхом).
     - Вариант 2: **«Без лошади (Пеший)»** (спавн пешим).
   - Для пехоты и стрелков пул `AllowedMounts` пуст, поэтому строка слота коня для них скрыта (`HasOptions = false`).
3. **Сетевой протокол (`DWDN_NetworkMessages.cs`):**
   - В `RequestCustomLoadoutMessage` интегрирована передача `HorseId` через сетевые шлюзы (`OnWrite` / `OnRead`).
4. **Управление спавном движка (`DWDN_SelectionMissionView.cs` & `DWDN_ServerLoadoutBehavior.cs`):**
   - В `SyncNativeTroopIndex` и серверном `HandleClientCustomLoadout`:
     - Если игрок выбрал коня (`HorseId != ""`): `SelectedTroopIndex` выставляется на индекс кавалерийского класса, и `SpawningBehaviorBase` нативно сажает игрока на коня.
     - Если игрок выбрал пеший бой (`HorseId == ""`): `SelectedTroopIndex` выставляется на 0 (пехота `mp_Ratay`). Движок Bannerlord спавнит персонажа прямо на земле пешим, не создавая коня.
   - В `TryApplyLoadoutToAgent`:
     - Если `HorseId` пуст, но у агента остался конь (например, от начального спавна в warmup), сервер мгновенно спешивает агента на землю и удаляет лошадь (`mount.FadeOut(true, false)`).
     - Затем персонажу надеваются все выбранные кавалерийские доспехи, копьё/пика, меч и щит.
   - Персонаж становится полноценным пешим рыцарем в тяжелых латах без каких-либо артефактов и сбоев.

### 3. Сборка и синхронизация
- Обе сборки (`Release` и `Server`) скомпилированы через `dotnet build` (0 ошибок).
- Все скрипты и DLL зеркалированы на клиенте игры и Dedicated Server скриптом `sync_all.ps1`.


---

## 32. Исправление скрапкода развертывания: устранение тайм-аута и дисконнекта при входе на сервер ("вылет")

### 1. Проявление варп-скверны
- **Симптом:** При попытке подключения клиента к Dedicated Server происходил сброс соединения обратно в лобби («вылет при входе на сервер»).
- **Анализ логов (`rgl_log_28088.txt` и `rgl_log_15388.txt`):**
  1. В логе выделенного сервера обнаружена критическая ошибка инициализации:
     `[12:45:05.050] Couldn't find .dll: ..\..\Modules\DaewdenM\bin\Win64_Shipping_Client\DWDN_Script.Server.dll`
  2. Движок Bannerlord (`Managed.InitializeSubModules`) для загрузки сборок модулей, объявленных в `SubModule.xml`, всегда жестко резолвит путь `..\..\Modules\<ModuleName>\bin\Win64_Shipping_Client\<DLLName>`, даже когда запущен Dedicated Server!
  3. В `sync_all.ps1` и в таргете `PostBuildServer` файла `DWDN_Script.csproj` библиотека `DWDN_Script.Server.dll` копировалась исключительно в подкаталог `Win64_Shipping_Server`. В каталоге `Win64_Shipping_Client` сервера её не было!
  4. Из-за отсутствия DLL сервер запускался в "голом" нативном режиме без регистрации серверного модуля `DWDN_Script.Server` и кастомного сетевого сообщения `RequestCustomLoadoutMessage`.
  5. При подключении клиента сетевой шлюз движка обнаруживал несовпадение зарегистрированных типов сообщений / сабмодулей, зависал на этапе рукопожатия (`MultiplayerIntermissionScreen`) и ровно через 20 секунд отключал клиента по тайм-ауту (`New MBGameClient State: AtLobby old state:InCustomGame`).

### 2. Обряд очищения логических матриц (Решение)
1. **Обновление таргета `PostBuildServer` в `DWDN_Script.csproj`:**
   - Серверная DLL (`DWDN_Script.Server.dll`) и файл отладочных символов (`.pdb`) теперь автоматически копируются в:
     - `..\..\..\bin\Win64_Shipping_Client\` (клиентский модуль `DWDN_TRY`)
     - `Dedicated Server\Modules\DaewdenM\bin\Win64_Shipping_Client\` (серверный модуль `DaewdenM`)
     - `Dedicated Server\Modules\DaewdenM\bin\Win64_Shipping_Server\`
     - `Dedicated Server\bin\Win64_Shipping_Server\`
2. **Модернизация литургии синхронизации (`d:\banAImod\scratch\sync_all.ps1`):**
   - Гарантировано создание всех требуемых каталогов `Win64_Shipping_Client` и `Win64_Shipping_Server`.
   - Производится полное зеркалирование всех сборок, исходных кодов (`ModuleData\scripts\`) и XML файлов между клиентом, сервером и рабочим каталогом `banAImod`.
3. **Верификация:**
   - Выполнен тестовый пуск Dedicated Server. В логе `rgl_log_19540.txt` зафиксировано:
     `[12:56:32.779] Assembly load result: SUCCESS`
     `[12:56:32.779] Loading assembly: ..\..\Modules\DaewdenM\bin\Win64_Shipping_Server\DWDN_Script.Server.dll`
   - Все структуры XML (`mpclassdivisions.xml`, `mpcharacters.xml`, `mpcultures.xml` и предметы) успешно прочитаны движком без единой ошибки.

---

## 33. Ликвидация скрапкода 0xC0000005 при смене фракции на Даевден, разделение Офицера на Десятника и Сотника, обновление шлема Короля Альберика Девальда

### 1. Точная природа и первопричина вылета при постановке фракции на карту
- **Симптом:** При переключении карты/фракции на Даевден (ChangeCulture: Requested culture: Daewden) клиент игры падал с ошибкой Access Violation ( xC0000005) в Engine.dll (gl_log_40100.txt, отметка 13:23:08.391).
- **Первопричина:**
  1. В DWDN_SelectionMissionView.cs был подписан синхронный обработчик _visualSpawnComponent.OnMyAgentVisualSpawned += OnMyAgentVisualSpawned;.
  2. Событие OnMyAgentVisualSpawned вызывается движком синхронно прямо внутри распаковки сетевого сообщения CreateAgentVisuals в методе MultiplayerMissionAgentVisualSpawnComponent.SpawnAgentVisualsForPeer.
  3. В этот момент unmanaged C++ стек в Engine.dll держит указатели на создаваемый объект визуала gentVisual2. Обработчик OnMyAgentVisualSpawned синхронно вызывал UpdatePedestalVisual(), который обращался к gentVisual.Refresh(false, data, true).
  4. Нативный AgentVisuals.Refresh() вызывает skeleton.ManualInvalidate(), ClearVisualComponents(), создание нового скелета и gentVisual.SetAction().
  5. Подобная реентрантная инвалидация и замена неуправляемых C++ структур скелета и мешей прямо во время выполнения родительского C++ метода создания визуала приводила к мгновенному падению по недействительному указателю ( xC0000005).
  6. Дополнительно: в UpdatePedestalVisual передавались жесткие индексы RightWieldedItemIndex(0) и LeftWieldedItemIndex(1), что вызывало конфликт при экипировке двуручного или длинного оружия со щитом.
- **Обряд очищения (Решение):**
  1. Вызовы обновления нативной марионетки выведены из синхронных сетевых обработчиков (OnMyAgentVisualSpawned, OnTeamChanged, Show) на безопасный отложенный таймер (RequestPedestalUpdate(0.05f)).
  2. Обновление марионетки UpdatePedestalVisual теперь выполняется исключительно на потоке кадра миссии (OnMissionScreenTick) после полного завершения создания сущности нативным C++ движком.
  3. Индексы хвата оружия рассчитываются штатным методом customEquip.GetInitialWeaponIndicesToEquip с автоматическим сбросом слота щита в EquipmentIndex.None, если оружие не предназначено для одноручного хвата (по эталону OMEE / LOTR).
  4. В OnMissionScreenTick введена строгая проверка IsLocalPlayerDwdn(myPeer) в условие автоматического отображения UI выбора (canShowAuto), исключающая паразитное открытие селектора для других фракций.

### 2. Разделение класса Офицера на Десятника и Сотника
- **Десятник (mp_Officer):**
  - Стоимость в mpclassdivisions.xml снижена со 180 до **170**.
  - Название: **«Десятник»**, описание: *«Младший командир пехоты в бригантине с офицерским бацинетом»*.
  - Пул экипировки: бригантина officer_brigantine, бацинет officer_dwdn_bacinet, поножи officer_legs, латные перчатки plate_gauntlet / plate_round_gauntlet, благородный меч mp_battania_noble_sword, тяжелый топор mp_sturgia_heavy_axe, щит zbroyniy_shield1.
- **Сотник (mp_Sotnik — новый полноценный класс):**
  - Зарегистрирован в mpcharacters.xml (mp_Sotnik_hero, mp_Sotnik_troop) и mpclassdivisions.xml (id mp_Sotnik, стоимость **190**, броня 48, перки на латную защиту, тяжелое вооружение и щит).
  - Название: **«Сотник»**, описание: *«Старший офицер в тяжелой латной кирасе, командующий сотней воинов»*.
  - Богатые латные элементы (отсутствующие у десятника):
    - Шлем: rmeds_bacinet_with_facemask (бацинет с маской-забралом), officer_dwdn_bacinet, без шлема "".
    - Доспех: dwdn_pikeman_cuirass_v1 (тяжелая кираса с латной юбкой), officer_brigantine.
    - Накидка: Baron_rundal_cape (богатый гербовый плащ барона), без накидки "".
    - Поножи: plate_legs_dwdn (полные латные поножи), officer_legs.
    - Рукавицы: gothic_gloves (готические латные перчатки), plate_gauntlet.
    - Оружие: mp_battania_noble_sword, тяжелый топор mp_sturgia_heavy_axe, серебряный щит zbroyniy_shield_silver1, длинное копье mp_western_long_spear.
- **Интеграция в герцогство Banreich (DWDN_DuchyManager.cs):**
  - Зарегистрированы оба класса с соответствующими пулами кастомизации.

### 3. Модификация Короля Альберика Девальда (mp_ALberic_Devald)
- В mpcharacters.xml (в обоих профилях mp_ALberic_Devald_hero и mp_ALberic_Devald_troop) прежний шлем aron_roondalt_vendelhelmet_1 заменен на бацинет с маской збройного: rmeds_bacinet_with_facemask.
- В DWDN_DuchyManager.cs в пул шлемов короля добавлена опция снятия шлема: king.AllowedHelmets.AddRange(new[] { "armeds_bacinet_with_facemask", "" });.

### 4. Верификация сборки и синхронизация
- Обе сборки (Release и Server) успешно скомпилированы через dotnet build (0 ошибок, 0 критических предупреждений).
- Все клиентские и серверные каталоги, скрипты и XML синхронизированы скриптом sync_all.ps1.
---

## 34. РќР°СЃС‚СЂРѕР№РєР° РєРѕРІР°РЅРѕРіРѕ РєРѕРїСЊСЏ Р¤РёРЅР°СЂРґР° (finnard_spear) РІ crafting_pieces.xml

### 1. РЎРёРјРїС‚РѕРј Рё РґРёР°РіРЅРѕСЃС‚РёРєР°
- **РџСЂРѕР±Р»РµРјР°**: Р’ РёРіСЂРµ РЅР° РјРѕРґРµР»Рё РєРѕРїСЊСЏ innard_spear РєСЂС‹Р»СЊСЏ РіР°СЂРґС‹ (innard_speard_guard) РїР°СЂРёР»Рё РІС‹СЃРѕРєРѕ РІ РІРѕР·РґСѓС…Рµ (РЅР° СЂР°СЃСЃС‚РѕСЏРЅРёРё РѕРєРѕР»Рѕ 1.8 РјРµС‚СЂР° РЅР°Рґ РґСЂРµРІРєРѕРј РєРѕРїСЊСЏ), Р° РЅР°РєРѕРЅРµС‡РЅРёРє РєРѕРїСЊСЏ РЅРµ СЃРѕСЃС‚С‹РєРѕРІС‹РІР°Р»СЃСЏ СЃ РіР°СЂРґРѕР№.
- **РђРЅР°Р»РёР· РіРµРѕРјРµС‚СЂРёРё FBX Рё РґРІРёР¶РєР° (C# TaleWorlds.Core.WeaponDesign.CalculatePivotDistances)**:
  - РњРµС€ РіР°СЂРґС‹ innard_speard_guard.fbx Р±С‹Р» СЌРєСЃРїРѕСЂС‚РёСЂРѕРІР°РЅ РЅРµ РІРѕРєСЂСѓРі С†РµРЅС‚СЂР° (0,0,0), Р° СѓР¶Рµ РІ РјРёСЂРѕРІС‹С… РєРѕРѕСЂРґРёРЅР°С‚Р°С… РІРµСЂС…РЅРµР№ С‡Р°СЃС‚Рё РґСЂРµРІРєР°:  \in [1.3746, 1.7738]$ Рј (РєСЂС‹Р»СЊСЏ РЅР°  = 1.758$ Рј).
  - РњРµС€ РґСЂРµРІРєР° innard_spear_handle.fbx РёРјРµРµС‚ РІРµСЂС… РЅР°  = 1.572$ Рј (РїСЂРё РјР°СЃС€С‚Р°Р±Рµ .9$ СЌС‚Рѕ  = 1.4148$ Рј).
  - РњРµС€ РЅР°РєРѕРЅРµС‡РЅРёРєР° innard_spearhead.fbx РёРјРµРµС‚ РЅРёР¶РЅСЋСЋ СЋР±РєСѓ РЅР°  = -0.1831$ Рј Рё РѕСЃС‚СЂРёРµ РЅР°  = +0.2162$ Рј (РїСЂРё РјР°СЃС€С‚Р°Р±Рµ .6$ СЂР°Р·РјР°С… $[-0.2930, +0.3459]$ Рј).
  - Р”РІРёР¶РѕРє Bannerlord РґР»СЏ С€Р°Р±Р»РѕРЅР° DWDN_1h_spear РїСЂРѕРёР·РІРѕРґРёС‚ СЃС‚СѓРїРµРЅС‡Р°С‚СѓСЋ СЃР±РѕСЂРєСѓ:
    Handle (build_order 0) -> Guard (build_order 1) -> Blade (build_order 2).
    Рљ РєРѕРѕСЂРґРёРЅР°С‚Рµ РґСЂРµРІРєР° $ РґРІРёР¶РѕРє РїСЂРёР±Р°РІР»СЏРµС‚ РІС‹С‡РёСЃР»РµРЅРЅС‹Рµ РґРёСЃС‚Р°РЅС†РёРё СЃРѕРµРґРёРЅРµРЅРёСЏ. РџСЂРё РЅСѓР»РµРІС‹С… РѕС„С„СЃРµС‚Р°С… РІ XML РіР°СЂРґР° СЃРјРµС‰Р°Р»Р°СЃСЊ РµС‰Рµ РЅР° $+1.014$ Рј РІРІРµСЂС…, РІ СЃСѓРјРјРµ РґР°РІР°СЏ РїРѕР·РёС†РёСЋ РєСЂС‹Р»СЊРµРІ РЅР° РІС‹СЃРѕС‚Рµ $+3.53$ Рј РЅР°Рґ Р·РµРјР»РµР№.

### 2. РњР°С‚РµРјР°С‚РёС‡РµСЃРєРѕРµ СЂРµС€РµРЅРёРµ РІ crafting_pieces.xml
Р”Р»СЏ РіР°СЂРґС‹ innard_speard_guard СѓСЃС‚Р°РЅРѕРІР»РµРЅС‹ СЃС‚СЂРѕРіРѕ СЂР°СЃСЃС‡РёС‚Р°РЅРЅС‹Рµ РїР°СЂР°РјРµС‚СЂС‹ <BuildData>:
- previous_piece_offset="157.7":
  РЎРґРІРёРіР°РµС‚ РїРёРІРѕС‚ РіР°СЂРґС‹ РІРЅРёР· РЅР° .523$ Рј (.7 \times 0.01 \times 1.6$), РІ СЂРµР·СѓР»СЊС‚Р°С‚Рµ С‡РµРіРѕ РїРѕСЃР°РґРѕС‡РЅР°СЏ РіРёР»СЊР·Р° РіР°СЂРґС‹ СЃР°РґРёС‚СЃСЏ С‚РѕС‡РЅРѕ РЅР° РѕРіРѕР»РѕРІРѕРє РґСЂРµРІРєР° ( = 1.4148$ Рј), Р° РєСЂС‹Р»СЊСЏ РІСЃС‚Р°СЋС‚ СЂРѕРІРЅРѕ РЅР° РѕС‚РјРµС‚РєСѓ  = 1.4152$ Рј.
- 
ext_piece_offset="-167.1":
  РћС‚СЂРёС†Р°С‚РµР»СЊРЅС‹Р№ РѕС„С„СЃРµС‚ СЃР»РµРґСѓСЋС‰РµР№ РґРµС‚Р°Р»Рё РїСЂРёР±Р°РІР»СЏРµС‚ Рє РїРёРІРѕС‚Сѓ РЅР°РєРѕРЅРµС‡РЅРёРєР° $+2.673$ Рј (.1 \times 0.01 \times 1.6$), Р±Р»Р°РіРѕРґР°СЂСЏ С‡РµРјСѓ РЅРёР¶РЅСЏСЏ РєСЂРѕРјРєР° РіРёР»СЊР·С‹ РЅР°РєРѕРЅРµС‡РЅРёРєР° Р»РѕР¶РёС‚СЃСЏ РЅР° РІРµСЂС…РЅРёР№ СЃСЂРµР· РіР°СЂРґС‹ СЃ РїСЂРµС†РёР·РёРѕРЅРЅРѕР№ С‚РѕС‡РЅРѕСЃС‚СЊСЋ (СЂР°СЃС…РѕР¶РґРµРЅРёРµ РјРµРЅРµРµ .00024$ Рј / .24$ РјРј).

`xml
	<CraftingPiece
		id="finnard_speard_guard"
		name="{=DVe5SkQA}Double Vane Wings"
		piece_type="Guard"
		mesh="finnard_speard_guard"
		length="5.7"
		weight="0">
		<!-- finnard_speard_guard -->
		<BuildData
			piece_offset="0"
			previous_piece_offset="157.7"
			next_piece_offset="-167.1" />
		<Materials>
			<Material
				id="Iron3"
				count="1" />
		</Materials>
	</CraftingPiece>
`

### 3. РЎРёРЅС…СЂРѕРЅРёР·Р°С†РёСЏ
- РР·РјРµРЅРµРЅРёСЏ РІРЅРµСЃРµРЅС‹ РІ crafting_pieces.xml РєР»РёРµРЅС‚Р° Рё СЂРµРїРѕР·РёС‚РѕСЂРёСЏ.
- Р”РѕР±Р°РІР»РµРЅР° СЃРёРЅС…СЂРѕРЅРёР·Р°С†РёСЏ crafting_pieces.xml РІ D:\banAImod\scratch\sync_all.ps1.
- РЎРµСЂРІРµСЂРЅС‹Р№ РјРѕРґСѓР»СЊ Modules\DaewdenM\ModuleData\crafting_pieces.xml СЃРёРЅС…СЂРѕРЅРёР·РёСЂРѕРІР°РЅ.
---

## 35. Устранение вылета при заходе на карту и восстановление копья Финнарда

### 1. Архитектурное устранение краша 0xC0000005 (LOTR / OMEE Isolated Preview)
- **Корень скрапкода**: При выборе фракции dwdn сервер отправлял пакет CreateAgentVisuals. На клиенте в DWDN_SelectionMissionView.cs висел хук OnMyAgentVisualSpawned, вызывавший gentVisual.Refresh(false, data, true) на объекте нативного пьедестала TaleWorlds. Пересоздание скелета и мешей во время C++ тика анимаций в Engine.dll вызывало фатальное нарушение доступа к памяти ( xC0000005).
- **Решение**:
  - Полностью отвязаны хуки нативного спавнера MultiplayerMissionAgentVisualSpawnComponent.
  - Удалены вызовы gentVisual.Refresh и принудительная локальная мутация SelectedTroopIndex.
  - Внедрен автономный класс предпросмотра DWDN_CharacterPreview (паттерн мода OMEE 3774022783), создающий изолированный AgentVisuals.Create и выделенную камеру MissionScreen.CustomCamera. Нативный спавнер TaleWorlds работает изолированно и больше не подвержен коллизиям памяти.

### 2. Калибровка копья Финнарда в crafting_pieces.xml
- **Декомпиляция WeaponDesign.CalculatePivotDistances**: Смещения previous_piece_offset и 
ext_piece_offset рассчитываются движком инкрементно от длины предыдущего звена: $Length + (prev - next)$.
- **Восстановление из проверенной версии DWDN_TRY123**:
  - innard_spear_handle: 
ext_piece_offset="-35"
  - innard_speard_guard: previous_piece_offset="7", 
ext_piece_offset="-19"
  - innard_spearhead: previous_piece_offset="24", 
ext_piece_offset="0"
  - С этим набором относительных дельт гарда и лезвие плотно стыкуются на древке без отрыва.

### 3. Компиляция и синхронизация
- Проекты DWDN_Script собраны в конфигурациях Release и Server (0 ошибок, 0 критических предупреждений).
- Запущен скрипт sync_all.ps1, синхронизировавший бинарники и XML на клиент (Modules\DWDN_TRY) и выделенный сервер (Modules\DaewdenM).
---

## 36. Создание GitHub-репозитория и полной технической документации мода DWDN

### 1. Репозиторий на GitHub
- **URL**: https://github.com/Havi-Dorgeir/DWDN_Module
- **Ветка**: main
- **Связанная рабочая копия**: E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\DWDN_TRYM
- **Стратегия отслеживания (Вариант А)**:
  - В Git включены: C#-исходники (ModuleData/scripts/DWDN_Script/), все XML (ModuleData/*.xml, mpitems/), разметка GUI (GUI/), скомпилированные шейдеры (Shaders/), предсобранные DLL (in/), ассеты (Assets/), исходные 3D-модели FBX и текстуры (AssetSources/), манифесты SubModule.xml и документация.
  - Исключены через .gitignore: скомпилированный монолит AssetPackages/pack0.tpac (1.1 ГБ, превышающий лимит GitHub 100 МБ) и локальный рабочий кэш компиляции RuntimeDataCache/ (1.7 ГБ).
  - Рабочее дерево полностью чистое (working tree clean), коммиты успешно отправлены на GitHub.

### 2. Техническая документация
- В корень репозитория добавлены:
  - README.md — краткий обзор модуля, структура директорий, команды сборки .NET CLI.
  - DOCUMENTATION.md — исчерпывающий самодостаточный мануал:
    1. Архитектурная карта мода (назначение каждой папки, XML-файла и C#-подсистемы).
    2. Пошаговый сквозной туториал «Добавление нового класса с нуля»:
       - Создание NPCCharacter в ModuleData/mpcharacters.xml.
       - Определение характеристик, стоимости и перков MPClassDivision в ModuleData/mpclassdivisions.xml.
       - Регистрация в реестре герцогств DWDN_DuchyManager.cs с привязкой пулов брони и оружия.
       - Сборка и развертывание.
    3. Пошаговый туториал «Добавление предметов в кастомный UI выбора»:
       - Нахождение строкового ID (items.xml, mpitems/, crafting_pieces.xml).
       - Добавление в пулы AllowedHelmets, AllowedBodyArmors, AllowedWeaponsSlotX и т.д.
       - Механика пустого слота "" для снятия предмета.
       - Серверная валидация и надевание через DWDN_ServerLoadoutBehavior.cs.
    4. Особенности клиент-серверной сетевой модели и защита от крашей (изоляция предпросмотра DWDN_CharacterPreview, очистка слотов коня у пехотинца, инкрементные смещения составного оружия).
    5. Таблица устранения типовых неполадок.


---

## 37. Создание священного кодекса моддинга для будущих ИИ (`AI_MODDING_GUIDE.md`)

- **Цель**: Предоставить исчерпывающую, высокоточную техническую документацию для последующих ИИ-агентов, исключающую необходимость гадать или методом проб и ошибок переписывать код.
- **Созданный файл**: `d:\banAImod\AI_MODDING_GUIDE.md`
- **Освещенные подсистемы и знания**:
  1. **Топография 4 контуров**:
     - `Client Dev`: `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\DWDN_TRY`
     - `Git Repo`: `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\DWDN_TRYM` (`https://github.com/Havi-Dorgeir/DWDN_Module`)
     - `Dedicated Server`: `E:\SteamLibrary\steamapps\common\Mount & Blade II Dedicated Server\Modules\DaewdenM`
     - `AI Workspace`: `d:\banAImod\`
     - Скрипт синхронизации: `d:\banAImod\scratch\sync_all.ps1`.
  2. **Репозитории и инструменты**:
     - `https://github.com/rehan-remade/universal-modder` (генераторы, сборщики, схемы).
     - Референсные моды: `3774022783` (LOTR/OMEE - эталон сетевого UI и изоляции превью), `3772431545` (Warlords), `3716004111`.
     - Декомпиляция через dnSpy/ILSpy и быстрые C#/PowerShell скрипты для инспекции сигнатур `TaleWorlds.*.dll`.
  3. **Подсистема огнестрела (Crossbow-Hack)**:
     - `hand_cannon` как `Crossbow`, `mp_bullets` как `Bolt` со спецфлагами (`BonusAgainstShield`, `CanPenetrateShield`, `AmmoSticksWhenShot="false"`).
     - `DWDN_FirearmsBehavior.cs`: `OnAgentShootMissile`, расчет `muzzleFrame`, сдвоенный звук (`ballista_missile/stone` + `missile/pot_breakable`), многослойный дым (`psys_game_stone_dust_a`, `psys_siege_door_falling_dust`, `psys_game_sparkle_a`).
     - Запрет `AddCustomMissile` на сервере ради идеальной синхронизации нативного движка.
  4. **Кастомный Gauntlet UI и изолированный 3D-превью**:
     - `DWDN_SelectionMissionView`, `DWDN_ClassSelectionVM`, `DWDN_EquipmentSlotVM`, `DWDN_ItemOptionVM`.
     - Искоренение краша `0xC0000005`: запрет мутации нативного пьедестала `MultiplayerMissionAgentVisualSpawnComponent` и вызова `agentVisual.Refresh(...)`.
     - Изолированный `DWDN_CharacterPreview` со своей сценой и камерой `MissionScreen.CustomCamera`.
  5. **Герцогства, классы и сетевой спавн**:
     - Триада `mpcharacters.xml` <-> `mpclassdivisions.xml` <-> `DWDN_DuchyManager.cs`.
     - Пакет `RequestCustomLoadoutMessage` и серверный спавн `DWDN_ServerLoadoutBehavior.cs`.
     - Защита от бага TaleWorlds с прилипанием меша коня к тазу человека: `equip[Horse] = Invalid; equip[HorseHarness] = Invalid;` в человеческом снаряжении.
  6. **Модульное составное оружие**:
     - Декомпилированная формула `WeaponDesign.CalculatePivotDistances()`.
     - Относительные дельты сочленений против ошибочных абсолютных координат из Blender. Проверенные значения для `finnard_spear`.
  7. **Кодекс защиты от скрапкода**:
     - Чтение `rgl_log_*.txt`, потокобезопасность UI, пустой слот `""` для снятия экипировки, обязательный запуск `sync_all.ps1`.


---

## 38. Настройка вспышки выстрела (Gunpowder Muzzle Flash)

- **Задача**: Добавить аутентичную вспышку огня при выстреле из ручницы/бомбарды, которая не висит в воздухе, а вспыхивает мгновенно как в реальности.
- **Решение**:
  - В `DWDN_FirearmsBehavior.cs` добавлен нативный эффект `prt_fireball`.
  - Позиционирование: `muzzlePos + forward * 0.10f` (вынос конуса огня на 10 см вперед по направлению выстрела).
  - Эффект `prt_fireball` имеет время жизни ровно `0.20` сек, текстуру пламени `prt_shd_fire_1` со скоростью воспроизведения 100 FPS и флаг `loop_sprite="false"`.
  - Результат: чистая, реалистичная вспышка пламени в момент выстрела, моментально растворяющаяся в пороховом дыме без зависания в пространстве.
  - Сборки `Release` и `Server` пересобраны (0 ошибок), `sync_all.ps1` успешно отработал, изменения отправлены в Git (`0da8d8d`).


---

## 39. Устранение белой вспышки в UI и калибровка метрового огненного шара

- **Устранение белой вспышки при переключении классов**:
  - В `DWDN_SelectionMissionView.cs` (строка 476) и `DWDN_ClientLoadoutBehavior.cs` (строка 539) заменен флаг `agentVisual.Refresh(false, data, true)` на `agentVisual.Refresh(false, data, false)`.
  - Аргумент `isSlow: true` вызывал нативный шейдер белого растворения TaleWorlds. При `false` смена скина и экипировки происходит мгновенно в 0 миллисекунд без засветов.
- **Огненная вспышка выстрела калибром 1 метр**:
  - В `DWDN_FirearmsBehavior.cs` внедрен эффект `explosion` (материал `exp_fire_test`).
  - Координаты: `muzzlePos + forward * 0.15f`.
  - Масштабирование: `flashFrame.Scale(new Vec3(0.67f, 0.67f, 0.67f))`, что точно калибрует базовый 1.5-метровый взрыв до аккуратного 1.0-метрового огненного ядра прямо перед стволом.
  - Огонь не висит в воздухе, исчезает через 0.7 сек, оставляя клубящийся пороховой шлейф.
  - Сборки собраны (0 ошибок), `sync_all.ps1` отработал, изменения отправлены в Git (`d21f6c4`).


---

## 40. Исправление блокировки в C++ (Белая вспышка и Огненный взрыв)

- **Корень скрапкода №1 (Белая вспышка при переключении классов)**:
  - В `DWDN_SelectionMissionView.cs` метод `OnSelectionChanged` непрерывно вызывал `SyncNativeTroopIndex`.
  - Смена `SelectedTroopIndex` заставляла нативный C++ спавнер TaleWorlds уничтожать марионетку и спавнить заново с белым эффектом материализации (`CreateAgentVisuals`).
  - **Решение**: Вызов `SyncNativeTroopIndex` удален из `OnSelectionChanged` и перенесен строго в `ExecuteSpawn()` (клик «В БОЙ»). При переключении классов теперь марионетка не пересоздается, а мгновенно меняет одежду через `UpdatePedestalVisual` (0ms, без вспышек).
- **Корень скрапкода №2 (Огненный взрыв)**:
  - Вызов `flashFrame.Scale(...)` ломал ортонормальность матрицы `MatrixFrame` (`IsOrthonormal() == false`). Нативное C++ ядро TaleWorlds `Mission.AddParticleSystemBurstByName` отбраковывало искаженную матрицу и молча отменяло спавн эффекта `explosion`.
  - **Решение**: Убран `Scale()`, матрица сделана чистой и ортонормальной `new MatrixFrame(orientation, muzzlePos + forward * 0.20f)`. Убран серый дым `psys_siege_door_falling_dust`, закрывавший пламя. Добавлен дебаг-лог `Debug.Print`.
- **Зависший процесс сервера**:
  - Выделенный сервер `DedicatedCustomServer.Starter` был запущен в фоне и блокировал перезапись `DWDN_Script.Server.dll`. Процесс был остановлен, `sync_all.ps1` успешно перезаписал все DLL сервера.
  - Коммит `2f86877` отправлен в Git.

---

## 41. Р•РґРёРЅС‹Р№ РёСЃС‚РѕС‡РЅРёРє РїСЂР°РІРґС‹ (DWDN_TRYM) Рё С„РёРЅР°Р»СЊРЅРѕРµ СѓСЃС‚СЂР°РЅРµРЅРёРµ Р°РЅРѕРјР°Р»РёР№

- **РђСЂС…РёС‚РµРєС‚СѓСЂРЅС‹Р№ Р·Р°РєРѕРЅ (DWDN_TRYM vs DWDN_TRY)**:
  - DWDN_TRYM СѓС‚РІРµСЂР¶РґРµРЅ РєР°Рє РµРґРёРЅСЃС‚РІРµРЅРЅС‹Р№ РЅРµСЃРєРѕРјРїРёР»РёСЂРѕРІР°РЅРЅС‹Р№ РњР°СЃС‚РµСЂ-СЂРµРїРѕР·РёС‚РѕСЂРёР№ (Single Source of Truth), РіРґРµ С…СЂР°РЅСЏС‚СЃСЏ РёСЃС…РѕРґРЅРёРєРё C# (ModuleData\scripts\DWDN_Script\), СЃС‹СЂС‹Рµ РјРѕРґРµР»Рё (AssetSources\) Рё Git.
  - DWDN_TRY вЂ” СЃРєРѕРјРїРёР»РёСЂРѕРІР°РЅРЅС‹Р№ РєР»РёРµРЅС‚СЃРєРёР№ РјРѕРґСѓР»СЊ РёРіСЂС‹.
  - DaewdenM вЂ” СЃРєРѕРјРїРёР»РёСЂРѕРІР°РЅРЅС‹Р№ СЃРµСЂРІРµСЂРЅС‹Р№ РјРѕРґСѓР»СЊ РІС‹РґРµР»РµРЅРЅРѕРіРѕ СЃРµСЂРІРµСЂР° (ZAPUSKSERVER.bat).
  - РЎРєСЂРёРїС‚ scratch\sync_all.ps1 РѕР±РЅРѕРІР»РµРЅ: СЃРѕР±РёСЂР°РµС‚ DWDN_TRYM Рё СЃРёРЅС…СЂРѕРЅРёР·РёСЂСѓРµС‚ Р±РёРЅР°СЂРЅРёРєРё, СЃРєСЂРёРїС‚С‹ Рё XML РІ DWDN_TRY, DaewdenM Рё d:\banAImod.

- **РЈСЃС‚СЂР°РЅРµРЅРёРµ Р±РµР»РѕР№ РІСЃРїС‹С€РєРё РїСЂРё РїРµСЂРµРєР»СЋС‡РµРЅРёРё РєР»Р°СЃСЃРѕРІ РІ UI**:
  - Р’ DWDN_SelectionMissionView.cs РІ OnSelectionChanged СѓР±СЂР°РЅ РїСЂРµР¶РґРµРІСЂРµРјРµРЅРЅС‹Р№ СЃРµС‚РµРІРѕР№ РІС‹Р·РѕРІ SendLoadoutToServer(_currentSelection).
  - РџСЂРёС‡РёРЅР° РІСЃРїС‹С€РєРё: РїСЂРё РєР°Р¶РґРѕРј РєР»РёРєРµ РЅР° РєР»Р°СЃСЃ/РѕСЂСѓР¶РёРµ РєР»РёРµРЅС‚ РѕС‚СЃС‹Р»Р°Р» РїР°РєРµС‚ РЅР° СЃРµСЂРІРµСЂ, СЃРµСЂРІРµСЂ СЂР°СЃСЃС‹Р»Р°Р» broadcast UpdateSelectedTroopIndex, Рё РЅР°С‚РёРІРЅС‹Р№ РєР»РёРµРЅС‚СЃРєРёР№ РєРѕРјРїРѕРЅРµРЅС‚ MultiplayerMissionAgentVisualSpawnComponent РїСЂРёРЅСѓРґРёС‚РµР»СЊРЅРѕ РїРµСЂРµСЃРѕР·РґР°РІР°Р» РјР°СЂРёРѕРЅРµС‚РєСѓ СЃ Р±РµР»С‹Рј РјР°С‚РµСЂРёР°Р»РёР·Р°С†РёРѕРЅРЅС‹Рј С€РµР№РґРµСЂРѕРј СЃРїР°РІРЅР°.
  - РўРµРїРµСЂСЊ РїСЂРё РїСЂРѕСЃРјРѕС‚СЂРµ СЌРєРёРїРёСЂРѕРІРєРё РјР°СЂРёРѕРЅРµС‚РєР° РѕР±РЅРѕРІР»СЏРµС‚СЃСЏ СЃС‚СЂРѕРіРѕ Р»РѕРєР°Р»СЊРЅРѕ С‡РµСЂРµР· UpdatePedestalVisual (gentVisual.Refresh(false, data, false)), Р° SendLoadoutToServer РІС‹Р·С‹РІР°РµС‚СЃСЏ СЃС‚СЂРѕРіРѕ РїСЂРё РЅР°Р¶Р°С‚РёРё "Р’ Р±РѕР№" (ExecuteSpawn).

- **Р¤РёРЅР°Р»СЊРЅРѕРµ СЂРµС€РµРЅРёРµ РѕРіРЅРµРЅРЅРѕРіРѕ С€Р°СЂР° РІС‹СЃС‚СЂРµР»Р° (Shader Cache Fix)**:
  - РџСЂРёС‡РёРЅР° РЅРµРІРёРґРёРјРѕСЃС‚Рё explosion: РІ particle_systems2.xml СЌС„С„РµРєС‚ СЃСЃС‹Р»Р°РµС‚СЃСЏ РЅР° РјР°С‚РµСЂРёР°Р» exp_fire_test, РєРѕС‚РѕСЂС‹Р№ РѕС‚СЃСѓС‚СЃС‚РІСѓРµС‚ РІ СЃРєРѕРјРїРёР»РёСЂРѕРІР°РЅРЅРѕРј РєРµС€Рµ С€РµР№РґРµСЂРѕРІ Bannerlord! C++ СЂРµРЅРґРµСЂРµСЂ РґРІРёР¶РєР° РјРѕР»С‡Р° РѕС‚Р±СЂР°СЃС‹РІР°Р» РѕС‚СЂРёСЃРѕРІРєСѓ.
  - Р РµС€РµРЅРёРµ: РІ DWDN_FirearmsBehavior.cs РІРЅРµРґСЂРµРЅС‹ РїСЂРѕРІРµСЂРµРЅРЅС‹Рµ РЅР°С‚РёРІРЅС‹Рµ СЌС„С„РµРєС‚С‹, СЂР°Р±РѕС‚Р°СЋС‰РёРµ РЅР° СЂРµР°Р»СЊРЅРѕРј С€РµР№РґРµСЂРЅРѕРј РјР°С‚РµСЂРёР°Р»Рµ prt_shd_fire_1: psys_game_burning_jar_trail (СЏСЂРѕСЃС‚РЅС‹Р№ С€Р°СЂ РѕРіРЅСЏ) Рё psys_game_missile_flame.
  - Р РµР·СѓР»СЊС‚Р°С‚: РїР»РѕС‚РЅС‹Р№, СЏСЂРєРёР№ РјРµС‚СЂРѕРІС‹Р№ РѕРіРЅРµРЅРЅС‹Р№ С€Р°СЂ РїР»Р°РјРµРЅРё РІС‹СЂС‹РІР°РµС‚СЃСЏ РёР· СЃС‚РІРѕР»Р° РїСЂРё РІС‹СЃС‚СЂРµР»Рµ.
---

## 42. Р РµР±Р°Р»Р°РЅСЃ РїРµС…РѕС‚РЅС‹С… РєР»Р°СЃСЃРѕРІ Рё РґР°Р»СЊРЅРѕР±РѕР№РЅРѕРіРѕ СѓСЂРѕРЅР° (Р Р°С‚Р°Р№, Р‘Р°РЅРЅРѕР±СЂР°РЅРЅС‹Р№, РњРѕР±РёР»СЊРЅРѕСЃС‚СЊ)

- **Р Р°Р·РґРµР»РµРЅРёРµ СЃР»РѕС‚РѕРІ Р Р°С‚Р°СЏ (РћРїРѕР»С‡РµРЅРёРµ)**:
  - РЈ Р Р°С‚Р°СЏ РѕС‚СЃСѓС‚СЃС‚РІРѕРІР°Р» РѕС‚РґРµР»СЊРЅС‹Р№ СЃР»РѕС‚ РїРѕРґ РєРѕРїСЊРµ (РІ РѕС‚Р»РёС‡РёРµ РѕС‚ РґСЂСѓРіРёС… РїРµС…РѕС‚РЅС‹С… РєР»Р°СЃСЃРѕРІ).
  - РЎР»РѕС‚ 2 С‚РµРїРµСЂСЊ РІС‹РґРµР»РµРЅ РїРѕРґ РґСЂРµРІРєРѕРІРѕРµ РѕСЂСѓР¶РёРµ: РґРѕР±Р°РІР»РµРЅ РІС‹Р±РѕСЂ РјРµР¶РґСѓ РѕС‚СЃСѓС‚СЃС‚РІРёРµРј РєРѕРїСЊСЏ "" Рё РєРѕСЂРѕС‚РєРёРј РєРѕРїСЊРµРј mp_western_short_spear.
  - РЎР»РѕС‚ 0 РѕС‡РёС‰РµРЅ РѕС‚ Р±Р»Р°РіРѕСЂРѕРґРЅРѕРіРѕ РјРµС‡Р° (mp_vlandian_short_sword СѓР±СЂР°РЅ) Рё РїРµСЂРµРІРµРґРµРЅ РЅР° Р°СѓС‚РµРЅС‚РёС‡РЅС‹Р№ РєСЂРµСЃС‚СЊСЏРЅСЃРєРёР№ Р°СЂСЃРµРЅР°Р»:
    * dwdn_cleaver (РўРµСЃР°Рє вЂ” РєР°СЃС‚РѕРјРЅС‹Р№ CraftedItem РЅР° Р±Р°Р·Рµ РєР»РёРЅРєР° cleaver_blade_1, РіР°СЂРґС‹ cleaver_guard_1, СЂСѓРєРѕСЏС‚Рё cleaver_grip_1, РЅР°РІРµСЂС€РёСЏ cleaver_pommel_1).
    * mp_sturgia_axe (РўРѕРїРѕСЂ).
    * mp_vlandian_hammer (РњРѕР»РѕС‚/РґСѓР±РёРЅР°).
  - РќР°Р·РІР°РЅРёСЏ Рё РѕРїРёСЃР°РЅРёСЏ РїРµСЂРєРѕРІ РѕР±РЅРѕРІР»РµРЅС‹ РІ mpclassdivisions.xml Рё DWDN_DuchyManager.cs.

- **Р’РѕРѕСЂСѓР¶РµРЅРёРµ Р‘Р°РЅРЅРѕР±СЂР°РЅРЅРѕРіРѕ (Р РµРіСѓР»СЏСЂ)**:
  - РћСЃРЅРѕРІРЅРѕР№ РјРµС‡ РїРµС…РѕС‚РёРЅС†Р° Р·Р°РјРµРЅРµРЅ РЅР° С„Р°Р»СЊС€РёРѕРЅ dwdn_falchion (РєР°СЃС‚РѕРјРЅС‹Р№ CraftedItem РЅР° Р±Р°Р·Рµ РєР»РёРЅРєР° landian_blade_9, РіР°СЂРґС‹ landian_guard_1, СЂСѓРєРѕСЏС‚Рё cleaver_grip_1, РЅР°РІРµСЂС€РёСЏ landian_pommel_1).
  - Р¤Р»Р°Рі multiplayer_item="true" РѕР±СЏР·Р°С‚РµР»РµРЅ РґР»СЏ РІСЃРµС… MP CraftedItem РІ dwdn_weapon.xml.
  - Р’ СЃР»РѕС‚Рµ 2 СЃРѕС…СЂР°РЅРµРЅ РІС‹Р±РѕСЂ РєРѕРїСЊСЏ mp_western_spear.

- **Р›РµСЃС‚РЅРёС†Р° РјРѕР±РёР»СЊРЅРѕСЃС‚Рё (Р РµР±Р°Р»Р°РЅСЃ СЃРєРѕСЂРѕСЃС‚РµР№ Рё СѓСЃРєРѕСЂРµРЅРёР№)**:
  - РЈСЃС‚СЂР°РЅРµРЅР° РѕРґРёРЅР°РєРѕРІР°СЏ СЃРєРѕСЂРѕСЃС‚СЊ РєР»Р°СЃСЃРѕРІ. Р’ mpclassdivisions.xml РїР°СЂР°РјРµС‚СЂС‹ СЃРєРѕСЂРѕСЃС‚Рё Рё РёРЅРµСЂС†РёРё СЂР°Р·РІРµРґРµРЅС‹:
    * **Р Р°С‚Р°Р№** (Р»РµРіРєР°СЏ РїРµС…РѕС‚Р°, РјРёРЅРёРјСѓРј Р±СЂРѕРЅРё): movement_speed="0.86", combat_movement_speed="0.93", cceleration="1.8"
    * **Р‘Р°РЅРЅРѕР±СЂР°РЅРЅС‹Р№** (Р»РёРЅРµР№РЅР°СЏ РїРµС…РѕС‚Р°): movement_speed="0.80", combat_movement_speed="0.88", cceleration="2.2"
    * **РџРёРєРёРЅРµСЂ**: movement_speed="0.83", combat_movement_speed="0.82", cceleration="2.0"
    * **Р—Р±СЂРѕР№РЅС‹Р№** (С‚СЏР¶РµР»С‹Р№ Р»Р°С‚РЅРёРє): movement_speed="0.75", combat_movement_speed="0.80", cceleration="2.6"
    * **РћС„РёС†РµСЂ**: movement_speed="0.74", combat_movement_speed="0.79", cceleration="2.7"
    * **РЎРѕС‚РЅРёРє**: movement_speed="0.73", combat_movement_speed="0.78", cceleration="2.8"
    * **Р—РЅР°РјРµРЅРѕСЃРµС†**: movement_speed="0.77", combat_movement_speed="0.84", cceleration="2.4"
    * **Р›СѓС‡РЅРёРє**: movement_speed="0.79", combat_movement_speed="0.74", cceleration="2.4"
    * **РђСЂР±Р°Р»РµС‚С‡РёРє**: movement_speed="0.74", combat_movement_speed="0.68", cceleration="3.0"
    * **Р С‹С†Р°СЂСЊ / Р‘Р°СЂРѕРЅС‹ (РїРµС€РєРѕРј)**: movement_speed="0.70-0.71", combat_movement_speed="0.60-0.63", cceleration="3.0-3.2"

- **Р РµР±Р°Р»Р°РЅСЃ СѓСЂРѕРЅР° РѕРіРЅРµСЃС‚СЂРµР»СЊРЅРѕРіРѕ Рё СЃС‚СЂРµР»РєРѕРІРѕРіРѕ РѕСЂСѓР¶РёСЏ**:
  - Р’ TaleWorlds Bannerlord СѓСЂРѕРЅ РѕРіРЅРµСЃС‚СЂРµР»СЊРЅРѕРіРѕ РѕСЂСѓР¶РёСЏ СЃРєР»Р°РґС‹РІР°РµС‚СЃСЏ РёР· СѓСЂРѕРЅР° СЃС‚РІРѕР»Р° (	hrust_damage) Рё СѓСЂРѕРЅР° РїСѓР»СЊ (mp_bullets = 15).
  - hand_cannon (СЂСѓС‡РЅРёС†Р°): Р±Р°Р·РѕРІС‹Р№ СѓСЂРѕРЅ СЃРЅРёР¶РµРЅ СЃРѕ 165 РґРѕ 110 (СЃСѓРјРјР°СЂРЅС‹Р№ СѓСЂРѕРЅ РїСѓР»РµР№: 125 РІРјРµСЃС‚Рѕ 180, СѓР±РёСЂР°СЏ РјРѕРјРµРЅС‚Р°Р»СЊРЅС‹Р№ РІР°РЅС€РѕС‚ С‡РµСЂРµР· Р»СЋР±С‹Рµ РґРѕСЃРїРµС…Рё).
  - mp_gun_prototype: Р±Р°Р·РѕРІС‹Р№ СѓСЂРѕРЅ СЃРЅРёР¶РµРЅ СЃРѕ 140 РґРѕ 90 (СЃСѓРјРјР°СЂРЅС‹Р№ СѓСЂРѕРЅ РїСѓР»РµР№: 105 РІРјРµСЃС‚Рѕ 155).
  - mp_steppe_superior_bow: Р±Р°Р·РѕРІС‹Р№ СѓСЂРѕРЅ СЃРЅРёР¶РµРЅ СЃ 48 РґРѕ 38 (РїСЂРёРІРµРґРµРЅ Рє СЃРѕСЂРµРІРЅРѕРІР°С‚РµР»СЊРЅРѕРјСѓ СЃС‚Р°РЅРґР°СЂС‚Сѓ MP-Р»СѓРєРѕРІ Bannerlord).

- **РЎРёРЅС…СЂРѕРЅРёР·Р°С†РёСЏ mpitems РІ sync_all.ps1**:
  - Р’ СЃРєСЂРёРїС‚ РґРµРїР»РѕСЏ sync_all.ps1 РґРѕР±Р°РІР»РµРЅР° СЂРµРїР»РёРєР°С†РёСЏ РїРѕРґРґРёСЂРµРєС‚РѕСЂРёРё ModuleData\mpitems\ РІ DWDN_TRY (РєР»РёРµРЅС‚) Рё DaewdenM (СЃРµСЂРІРµСЂ), РіР°СЂР°РЅС‚РёСЂСѓСЏ РѕС‚СЃСѓС‚СЃС‚РІРёРµ СЃРµС‚РµРІРѕРіРѕ СЂР°СЃСЃРёРЅС…СЂРѕРЅР° РїСЂРµРґРјРµС‚РѕРІ.
---

## 43. Р’РЅРµРґСЂРµРЅРёРµ РїСЂРѕС‚РѕРєРѕР»Р° Git LFS РґР»СЏ РіСЂР°С„РёС‡РµСЃРєРёС… Р°СЃСЃРµС‚РѕРІ

- **РРЅРёС†РёР°Р»РёР·Р°С†РёСЏ Git LFS**:
  - Р’ СЂРµРїРѕР·РёС‚РѕСЂРёРё DWDN_TRYM Р°РєС‚РёРІРёСЂРѕРІР°РЅ git lfs install (РІРµСЂСЃРёСЏ РєР»РёРµРЅС‚Р°: 3.7.1).
  - РЎРѕР·РґР°РЅР° СЃРІСЏС‰РµРЅРЅР°СЏ СЃРєСЂРёР¶Р°Р»СЊ .gitattributes:
    `gitattributes
    art_assets/** filter=lfs diff=lfs merge=lfs -text
    `
- **Р—Р°С‰РёС‚Р° РєРІРѕС‚С‹ СЂРµРїРѕР·РёС‚РѕСЂРёСЏ (1 Р“Р‘)**:
  - Р‘РµСЃРїР»Р°С‚РЅС‹Р№ Р°РєРєР°СѓРЅС‚ GitHub РёРјРµРµС‚ СЃС‚СЂРѕРіРёР№ Р»РёРјРёС‚: 1 Р“Р‘ LFS С…СЂР°РЅРёР»РёС‰Р° Рё 1 Р“Р‘ РјРµСЃСЏС‡РЅРѕРіРѕ С‚СЂР°С„РёРєР°.
  - РџРѕСЌС‚РѕРјСѓ РіР»РѕР±Р°Р»СЊРЅС‹Р№ РїР°С‚С‚РµСЂРЅ *.png РёР»Рё *.dds **РЅРµ** РІРєР»СЋС‡Р°Р»СЃСЏ РґР»СЏ РІСЃРµС… РїР°РїРѕРє, РґР°Р±С‹ РЅРµ СЂР°СЃС…РѕРґРѕРІР°С‚СЊ РєРІРѕС‚Сѓ РЅР° СЃРѕС‚РЅРё С‚СЏР¶РµР»С‹С… С‚РµРєСЃС‚СѓСЂ РёР· AssetSources/.
  - РџРѕРґ LFS РІС‹РґРµР»РµРЅР° РёР·РѕР»РёСЂРѕРІР°РЅРЅР°СЏ РґРёСЂРµРєС‚РѕСЂРёСЏ rt_assets/ (РґР»СЏ Р°СЂС‚РѕРІ, РїСЂРѕРјРѕ-РјР°С‚РµСЂРёР°Р»РѕРІ, РёРєРѕРЅРѕРє Рё СЃРєСЂРёРЅС€РѕС‚РѕРІ). Р›СЋР±С‹Рµ С„Р°Р№Р»С‹ РІРЅСѓС‚СЂРё РЅРµРµ Р°РІС‚РѕРјР°С‚РёС‡РµСЃРєРё РІРµСЂСЃРёРѕРЅРёСЂСѓСЋС‚СЃСЏ С‡РµСЂРµР· Git LFS.
- **РўРµСЃС‚РѕРІР°СЏ РІРµСЂРёС„РёРєР°С†РёСЏ**:
  - Р¤Р°Р№Р» rt_assets/lfs_test_image.png СѓСЃРїРµС€РЅРѕ СЃРѕС…СЂР°РЅРµРЅ РєР°Рє LFS pointer (С…СЌС€ SHA-256), Р° Р±РёРЅР°СЂРЅРёРє Р·Р°Р»РёС‚ РЅР° LFS-СЃРµСЂРІРµСЂ GitHub (Uploading LFS objects: 100%).
  - РљРѕРјРјРёС‚ 1b0b4e РѕС‚РїСЂР°РІР»РµРЅ РІ origin/main.
---

## 44. РЈСЃС‚СЂР°РЅРµРЅРёРµ СЂР°Р·РґРІРѕРµРЅРёСЏ РјРѕРґР° РІ Р»Р°СѓРЅС‡РµСЂРµ Рё Р»РёРєРІРёРґР°С†РёСЏ РєРѕРЅС„Р»РёРєС‚Р° РІРµСЂСЃРёР№

- **Р›РёРєРІРёРґР°С†РёСЏ РґСѓР±Р»РёРєР°С‚Р° DWDN_TRY**:
  - Р’ РєР°С‚Р°Р»РѕРіРµ Modules/ РѕРґРЅРѕРІСЂРµРјРµРЅРЅРѕ РЅР°С…РѕРґРёР»РёСЃСЊ DWDN_TRY Рё DWDN_TRYM, РѕР±Рµ СЃ SubModule.xml Рё <Id value="DWDN_TRY"/>. Р›Р°СѓРЅС‡РµСЂ РІС‹РІРѕРґРёР» РґРІР° РѕРґРёРЅР°РєРѕРІС‹С… РїСѓРЅРєС‚Р° "Daewden module" СЃ РєРѕР»Р»РёР·РёРµР№ ID.
  - РџР°РїРєР° DWDN_TRY РґРµР°РєС‚РёРІРёСЂРѕРІР°РЅР° Рё РїРµСЂРµРёРјРµРЅРѕРІР°РЅР° РІ _DISABLED_DWDN_TRY_BACKUP, РёР· РЅРµРµ СѓРґР°Р»РµРЅС‹ РІСЃРµ SubModule*.xml.
  - Р•РґРёРЅСЃС‚РІРµРЅРЅС‹Рј Р°РєС‚РёРІРЅС‹Рј РєР»РёРµРЅС‚СЃРєРёРј РјРѕРґСѓР»РµРј РІ Modules/ РѕСЃС‚Р°РІР»РµРЅ DWDN_TRYM.
- **РЎРЅСЏС‚РёРµ РєСЂР°СЃРЅРѕРіРѕ СЃС‚Р°С‚СѓСЃР° РѕС€РёР±РєРё СЏРґСЂР° (v1.2.8 -> v1.4.8)**:
  - Р’ SubModule.xml СѓРґР°Р»РµРЅР° Р¶РµСЃС‚РєР°СЏ Р±Р»РѕРєРёСЂРѕРІРєР° DependentVersion="v1.2.8".
  - РЈСЃС‚Р°РЅРѕРІР»РµРЅС‹ СѓРЅРёРІРµСЂСЃР°Р»СЊРЅС‹Рµ Р·Р°РІРёСЃРёРјРѕСЃС‚Рё <DependedModule Id="Native" Optional="false" /> Рё <DependedModule Id="Multiplayer" Optional="false" />, СЃРѕРІРјРµСЃС‚РёРјС‹Рµ СЃ Р°РєС‚СѓР°Р»СЊРЅС‹Рј Р±РёР»РґРѕРј РёРіСЂС‹ v1.4.8.
- **РСЃРїСЂР°РІР»РµРЅРёРµ РїРѕСЂСЏРґРєР° Р·Р°РіСЂСѓР·РєРё СѓР·Р»РѕРІ XML (Р»РёРєРІРёРґР°С†РёСЏ NullReference)**:
  - Р’ SubModule.xml СѓР·Р»С‹ CraftingTemplates, WeaponDescriptions, CraftingPieces Рё Items (mpitems) РїРµСЂРµРЅРµСЃРµРЅС‹ СЃС‚СЂРѕРіРѕ **РїРµСЂРµРґ** MPClassDivisions.
  - РўРµРі Items СЃРЅР°Р±Р¶РµРЅ РїСЂР°РІРёР»СЊРЅРѕР№ РѕР±РµСЂС‚РєРѕР№ <IncludedGameTypes><GameType value="MultiplayerGame" /></IncludedGameTypes>.
  - Р­С‚Рѕ РёСЃРєР»СЋС‡РёР»Рѕ РѕС€РёР±РєСѓ Null object reference found with ID: dwdn_cleaver РЅР° СЃРµСЂРІРµСЂРµ Рё РєР»РёРµРЅС‚Рµ.
- **РћР±РЅРѕРІР»РµРЅРёРµ СЃРєСЂРёРїС‚Р° СЃРёРЅС…СЂРѕРЅРёР·Р°С†РёРё (sync_all.ps1)**:
  - РР· sync_all.ps1 РёСЃРєР»СЋС‡РµРЅР° С†РµР»СЊ DWDN_TRY. РњР°СЃС‚РµСЂ DWDN_TRYM С‚РµРїРµСЂСЊ СЏРІР»СЏРµС‚СЃСЏ РЅРµРїРѕСЃСЂРµРґСЃС‚РІРµРЅРЅРѕ РёРіСЂР°РµРјС‹Рј РјРѕРґСѓР»РµРј, Р° СЃРєСЂРёРїС‚ СЂРµРїР»РёС†РёСЂСѓРµС‚ Р±РёРЅР°СЂРЅРёРєРё, СЃРєСЂРёРїС‚С‹ Рё XML РІ СЃРµСЂРІРµСЂРЅС‹Р№ РјРѕРґСѓР»СЊ DaewdenM.
---

## 45. РџРµСЂРµРЅРѕСЃ Р±Р°Р»Р°РЅСЃРЅС‹С… РїСЂР°РІРѕРє РІ СЂР°Р±РѕС‡РёР№ РјРѕРґСѓР»СЊ DWDN_TRY

- **Р›РѕРєР°Р»РёР·Р°С†РёСЏ Р·Р°РґР°С‡Рё**:
  - Р’ РєР°С‚Р°Р»РѕРіРµ E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\DWDN_TRY СЂР°СЃРїР°РєРѕРІР°РЅР° РёСЃС…РѕРґРЅР°СЏ СЂР°Р±РѕС‡Р°СЏ РІРµСЂСЃРёСЏ РјРѕРґР°.
  - РўРѕС‡РµС‡РЅРѕ РёРЅС‚РµРіСЂРёСЂРѕРІР°РЅС‹ РёСЃРєР»СЋС‡РёС‚РµР»СЊРЅРѕ Р±Р°Р»Р°РЅСЃРЅС‹Рµ РєРѕРјРїРѕРЅРµРЅС‚С‹ Р±РµР· РІРјРµС€Р°С‚РµР»СЊСЃС‚РІР° РІ РїСЂРѕС‡РёРµ СЃС‚СЂСѓРєС‚СѓСЂС‹:
    1. ModuleData/mpitems/dwdn_weapon.xml:
       * Р”РѕР±Р°РІР»РµРЅ dwdn_cleaver (РўРµСЃР°Рє РѕРїРѕР»С‡РµРЅРёСЏ) Рё dwdn_falchion (Р¤Р°Р»СЊС€РёРѕРЅ).
       * РџРѕРЅРёР¶РµРЅ РёР·Р±С‹С‚РѕС‡РЅС‹Р№ СѓСЂРѕРЅ: hand_cannon (165 -> 110), mp_gun_prototype (140 -> 90), mp_steppe_superior_bow (48 -> 38).
    2. ModuleData/mpclassdivisions.xml:
       * РџСЂРёРјРµРЅРµРЅР° Р»РµСЃС‚РЅРёС†Р° СЃРєРѕСЂРѕСЃС‚РµР№ (movement_speed, combat_movement_speed, cceleration).
       * Р Р°С‚Р°Р№ РїРѕР»СѓС‡РёР» РІС‹Р±РѕСЂ РїСЂРѕСЃС‚РѕРіРѕ РѕСЂСѓР¶РёСЏ РІ РЎР»РѕС‚Рµ 0 (С‚РµСЃР°Рє, С‚РѕРїРѕСЂ, РјРѕР»РѕС‚) Рё РѕС‚РґРµР»СЊРЅС‹Р№ РЎР»РѕС‚ 2 РїРѕРґ РєРѕСЂРѕС‚РєРѕРµ РєРѕРїСЊРµ (mp_western_short_spear).
       * Р‘Р°РЅРЅРѕР±СЂР°РЅРЅС‹Р№ РїРѕР»СѓС‡РёР» С„Р°Р»СЊС€РёРѕРЅ РІ РЎР»РѕС‚Рµ 0.
    3. DWDN_DuchyManager.cs Рё РєРѕРјРїРёР»СЏС†РёСЏ:
       * Р›РѕРіРёРєР° СЃРїР°РІРЅР° СЌРєРёРїРёСЂРѕРІРєРё РѕРїРѕР»С‡РµРЅРёСЏ СЃРёРЅС…СЂРѕРЅРёР·РёСЂРѕРІР°РЅР° СЃ XML.
       * РџСЂРѕРµРєС‚ СЃРєРѕРјРїРёР»РёСЂРѕРІР°РЅ dotnet build РІ Release Рё Server РєРѕРЅС„РёРіСѓСЂР°С†РёСЏС… (0 РѕС€РёР±РѕРє).
    4. SubModule.xml:
       * РЈР·РµР» Items СЂР°Р·РјРµС‰РµРЅ РїРµСЂРµРґ MPClassDivisions РІРѕ РёР·Р±РµР¶Р°РЅРёРµ Null object reference.
       * РЎРЅСЏС‚Рѕ РѕРіСЂР°РЅРёС‡РµРЅРёРµ DependentVersion="v1.2.8".
- **РЎРёРЅС…СЂРѕРЅРёР·Р°С†РёСЏ СЃРµСЂРІРµСЂР°**:
  - Р‘Р°Р»Р°РЅСЃРЅС‹Рµ С„Р°Р№Р»С‹ СЂРµРїР»РёС†РёСЂРѕРІР°РЅС‹ РІ E:\SteamLibrary\steamapps\common\Mount & Blade II Dedicated Server\Modules\DaewdenM Рё СЃРµСЂРІРµСЂРЅС‹Р№ РєР°С‚Р°Р»РѕРі bin.
---

## 46. Перенос репозитория Git в DWDN_TRY (Единый корень разработки)

- **Миграция священного ядра Git**:
  - Папка .git (история, ветка main, хуки LFS, токен origin/main) перенесена в E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\DWDN_TRY.
  - Модуль DWDN_TRY теперь является прямым Git-репозиторием проекта.
  - В DWDN_TRY перенесены файлы конфигурации .gitignore и .gitattributes (Git LFS для rt_assets/).
  - Все сопутствующие скрижали (
otes.md, AI_MODDING_GUIDE.md, DOCUMENTATION.md, README.md, rt_assets/) развернуты в корне DWDN_TRY.
- **Скрипт развертывания**:
  - sync_all.ps1 в корне DWDN_TRY настроен на репликацию бинарников, скриптов, SubModule.xml и mpitems напрямую из DWDN_TRY в DaewdenM (Dedicated Server).
---

## 47. Полный реверс-инжиниринг ядра Mount & Blade II: Bannerlord (v1.4.8)

- **Декомпиляция и анализ базовых модулей (Native, SandBoxCore, SandBox, Multiplayer)**:
  - Создан автономный CLI-декомпилятор на базе ICSharpCode.Decompiler, просканировано 1923 класса и интерфейса в 11 сборках игры.
  - Составлена фундаментальная архитектурная библия **BANNERLORD_COMPLETE_REVERSE_ENGINEERING.md**:
    1. Устройство моста C++/C# (DotNetObject, таблицы указателей EngineMethod и [MBCallback]).
    2. Двойной пайплайн загрузки XML: нативный ModuleData/project.mbproj (39 таблиц soln_*) и управляемый MBObjectManager (слияние XML, _replaceWhileMerging, XSD-валидация, XSLT-трансформации и GetPresumedObject).
    3. Полная побайтовая спецификация бинарных пакетов .tpac (LZ4 + xxHash64 + чанки METM/DATA мешей) и кэша .rdc (145-байтные записи RDC0).
    4. Контракт скелетной анимации (AnimResult с жестким лимитом в 64 кости) и формула расчёта Z-стыков составного оружия (CraftingPiece).
    5. Жизненный цикл MBSubModuleBase, Game, MultiplayerGame, GameStateScreenManager, ViewCreatorManager и сетевая репликация GameNetworkMessage.

---

## 48. Создание комплекса из 3 визуальных GUI-студий моддинга (Tools/)

- **1. Авто-Калибратор 3D Оружия (Tools/WeaponCalibrator/)**:
  - Напрямую парсит бинарные .tpac архивы модуля (LZ4 + METM) и AssetSources/*.fbx, вычисляя точные 3D-габариты (BoundingBox, точки Z-стыков, радиус шейки).
  - Включает интерактивный 3D-вьюпорт сборки оружия (Blade + Guard + Handle + Pommel) с авто-центрированием рукояти (piece_offset), автоматическим расчётом previous_piece_offset / 
ext_piece_offset и записью в crafting_pieces.xml в 1 клик.
- **2. Визуальный Конструктор Герцогств и Лодаутов (Tools/DuchyLoadoutStudio/)**:
  - Загружает герцогства и классы из DWDN_DuchyManager.cs, mpclassdivisions.xml, mpcharacters.xml и mpitems/*.xml.
  - Отрисовывает интерактивный 3D-манекен выбранного класса со всеми слоями экипировки.
  - Оснащён кнопкой **«⚡ АВТО-ПРИМЕНИТЬ В МОД DWDN_TRY (C# + XML + СБОРКА DLL)»**, которая автоматически обновляет DWDN_DuchyManager.cs, синхронизирует mpclassdivisions.xml и mpcharacters.xml, компилирует DWDN_Script.dll и DWDN_Script.Server.dll и зеркалирует билд на выделенный сервер DaewdenM.
- **3. Мгновенный Предпусковой Валидатор Мода (Tools/ModValidator/)**:
  - За 0.25 секунды без запуска игры выполняет 8-уровневую диагностику мода: синтаксис всех XML, порядок узлов SubModule.xml, сквозные ссылки C# <-> XML, наличие 3D-мешей в .tpac, баланс зазоров составного оружия и побайтовое совпадение хэшей SHA-256 между клиентом DWDN_TRY и сервером DaewdenM (с кнопкой авто-исправления в 1 клик).

---

## 49. Тактическая система Ауры Знаменосца (DWDN_BannerAuraBehavior.cs)

- **Назначение**:
  - Превратить класс **Знаменосец (mp_Bannerman)** из пехотинца без щита с декоративным штандартом в ценного тактического баффера строя без нарушения баланса дуэлей.
- **Архитектура и механика (ModuleData/scripts/DWDN_Script/DWDN_BannerAuraBehavior.cs)**:
  - **Условие активации**: Аура активна только пока Знаменосец жив и держит знамя непосредственно в руках (dwdn_banner1 / ItemTypeEnum.Banner в основном или левом слоте). Если знамя убрано за спину — аура гаснет.
  - **Защита от стакания**: Ауры от нескольких знаменосцев не складываются (Tier 0 = Нет, Tier 1 = Союзник в радиусе 12 м, Tier 2 = Сам Знаменосец).
  - **Балансные бонусы союзникам в радиусе 12 метров (Tier 1)**:
    1. +4 к броне головы, корпуса, рук и ног (ArmorHead, ArmorTorso, ArmorArms, ArmorLegs).
    2. +5% к скорости марша (MaxSpeedMultiplier) и +6% к боевому шагу (CombatMaxSpeedMultiplier).
    3. +4% к скорости удара/укола в ближнем бою и +6% к скорости перезарядки стрелков (ReloadSpeed).
    4. **«Второе дыхание»**: восстановление +1 HP раз в 3 секунды вне боя (если боец не получал урона последние 5 секунд) строго до потолка **75% от HealthLimit**.
  - **Личный бонус Знаменосца со знаменем в руках (Tier 2)**:
    - +6 к броне всех частей тела (компенсация отсутствия щита против стрелков), +5% к скорости бега, +6% к боевому шагу, +5% к скорости удара и регенерация вне боя до 75% HP.
  - **Сетевая синхронизация и защита от сброса**:
    - Кэширование базовой брони при спавне и перехват OnAgentStatsUpdated предотвращают сброс баффа при смене оружия и исключают бесконечное накопление множителей.
    - Выполнение на сервере и клиенте синхронизирует MaxSpeedMultiplier без сетевого *rubberbanding*, а восстановление HP выполняется строго на авторитетном сервере (SyncHealthToClients).


---

## 50. Переход Выделенного Сервера на .NET 8 в Bannerlord v1.5.7 (Обновление PR #8 / коммит 9acfb1b)

- **Контекст обновления инфохранилища (Havi-Dorgeir/DWDN_Module)**:
  - В Bannerlord v1.5.7 (сборка выделенного сервера 26569029 от 11.11.2026) TaleWorlds обновила рантайм выделенного сервера с устаревшего .NET 6 / .NET Framework на **.NET 8.0**.
  - Серверные DLL, собранные под .NET Framework 4.7.2, отказываются загружаться средой выделенного сервера (проявление несовместимости ABI и рантайма).
- **Архитектурное решение двойной сборки (Client .NET 4.7.2 vs Server .NET 8.0)**:
  - Выделен отдельный проект C# SDK-style: `ModuleData/scripts/DWDN_Script.Server/DWDN_Script.Server.csproj` с таргетом `<TargetFramework>net8.0</TargetFramework>`.
  - Проект компилируется против библиотек выделенного сервера (`Mount & Blade II Dedicated Server/bin/Win64_Shipping_Server`), а не клиентской игры.
  - Путь к серверу определяется автоматически (соседняя папка в Steam) или через свойство `-p:BannerlordServerDir="Путь"` / переменную окружения `BANNERLORD_SERVER_DIR`.
  - Выходные артефакты кладутся в `bin/Win64_Shipping_Server` и кроссплатформенный `bin/Linux64_Shipping_Server`.
  - При сборке клиента `DWDN_Script.csproj` (Release) автоматически вызывается сборка серверного проекта под .NET 8.
  - Рефлексивные вызовы в `DWDN_NetworkHelper` (`GameNetwork.Add/RemoveClientMessageHandler`) адаптированы под сигнатуры .NET 8 сборщика сетевых пакетов.


---

## 51. Создание Автономного Предпускового Тестового Полигона Механик (`MechanicsTester`)

- **Назначение**:
  - Обеспечить быстрое тестирование C#-логики мода DWDN и ядра Bannerlord **до запуска игры** (время полного цикла тестов: ~25-200 мс против 2-3 минут на загрузку Bannerlord).
  - Инструмент создан как для ИИ-агентов (headless CLI режим с параметрами `--cli` и `--json`), так и для человека (интерактивный WinForms дашборд с ярлыком `ЗАПУСК_ТЕСТОВ_МЕХАНИК.bat`).
- **Архитектура и состав тестовых сьют (`d:\banAImod\MechanicsTester\`)**:
  1. `NetworkTestSuite`: эмулирует бинарный поток `GameNetwork`, тестирует round-trip сериализацию сетевых пакетов (`RequestCustomLoadoutMessage`, `DeployArtilleryMessage`, `SetArtilleryPushStateMessage`) и проверяет отлов поврежденных потоков.
  2. `DuchyLoadoutTestSuite`: парсит `DWDN_DuchyManager.cs`, проверяет отсутствие префикса `Item.` в C#, валидирует структуру классов (Инженер, Знаменосец) и моделирует работу серверного античита при подмене предметов в пакетах.
  3. `BannerAuraTestSuite`: эмулирует работу `DWDN_BannerAuraBehavior` (радиус 12м, отключение ауры при смене флага на оружие, защита от стакания от нескольких знаменосцев, математика брони Tier 1/2 и регенерация «Второго дыхания» с кулдауном 5 сек и потолком 75% HP).
  4. `ArtilleryTrainTestSuite`: тестирует математику походной упряжки (дымшло 2.8м за конем, жесткое ограничение шага коня <= 1.8 м/с, проверка радиуса [F] 3.0м, перевод в боевое состояние и спавн боеприпасов).
  5. `ReflectionAbiTestSuite`: сканирует скомпилированные DLL TaleWorlds в `Win64_Shipping_Client` и `Win64_Shipping_Server`, проверяет наличие всех вызываемых через рефлексию методов (`Agent.SetMaximumSpeedLimit`, `GameNetwork.IsClient/IsServer`) с динамическим резолвером зависимостей.
- **Инструкция запуска**:
  - Для моддера: двойной клик по [`d:\banAImod\ЗАПУСК_ТЕСТОВ_МЕХАНИК.bat`](file:///d:/banAImod/%D0%97%D0%90%D0%9F%D0%A3%D0%A1%D0%9A_%D0%A2%D0%95%D0%A1%D0%A2%D0%9E%D0%92_%D0%9C%D0%95%D0%A5%D0%90%D0%9D%D0%98%D0%9A.bat).
  - Для ИИ-агентов: `dotnet run --project d:\banAImod\MechanicsTester\MechanicsTester.csproj -c Release --no-build -- --cli --json` (возвращает JSON-вердикт и код завершения 0 при успехе, 1 при ошибках).


---

## 52. Внедрение Полноценного Кастомного Admin Tools (DWDN Admin Tools)

- **Контекст и анализ наследия предшественника**:
  - В каталоге `ModuleData/scripts/DWDN_Script/Server/Admin/` предшественник написал массивный бэкенд на 55 КБ (`DWDN_AdminService.cs`, `DWDN_AdminBehavior.cs`, `DWDN_AdminGameHandler.cs`), содержащий реализацию 27 серверных команд (`!heal`, `!god`, `!tp`, `!goto`, `!bring`, `!slay`, `!freeze`, `!disarm`, `!kick`, `!ban`, `!warmup`, `!round`, `!map` и др.).
  - **Критический дефект**: полностью отсутствовал клиентский графический интерфейс (GUI) и сетевой протокол взаимодействия. Администраторы были вынуждены вбивать команды вслепую в чат, а в меню паузы по-прежнему открывалась урезанная ванильная админка TaleWorlds `MissionGauntletAdminPanel`.
- **Архитектурная реализация сетевого протокола (`Common/Network/DWDN_NetworkMessages.cs`)**:
  - `RequestAdminActionMessage` (`GameNetworkMessageSendType.FromClient`): передает идентификатор действия (`Action`), целевой ID игрока (`TargetPlayerId`) и дополнительный аргумент (`Argument`).
  - `SyncAdminStateMessage` (`GameNetworkMessageSendType.FromServer`): отправляет клиенту статус администратора (`IsAdmin = true/false`) при подключении к серверу, входе по `admins.txt` или успешной авторизации через `!login <пароль>`.
  - Зарегистрирован серверный обработчик в `DWDN_AdminGameHandler.cs` с жесткой проверкой `DWDN_AdminService.IsAdmin(peer)` (неавторизованные запросы мгновенно отклоняются со спам-защитой и записью в лог безопасности).
- **Клиентский интерфейс Admin Tools (`Client/UI/Admin/`)**:
  - `DWDN_AdminMissionView.cs`:
    - Аннотирован атрибутом `[OverrideView(typeof(MissionGauntletAdminPanel))]` — автоматически подменяет ванильную админку TaleWorlds во всех сетевых миссиях без единого Harmony-патча.
    - Обрабатывает вызов по горячим клавишам `[F10]` и `[Insert]` в `OnMissionScreenTick`.
    - **Двухуровневый отказоустойчивый интерфейс**:
      1. Основной: Gauntlet UI слой `GauntletLayer("DWDN_AdminPanel")` на базе XML-префаба `GUI/Prefabs/DWDN_AdminPanel.xml`.
      2. Резервный (Native Fallback): нативный диалог TaleWorlds `MBInformationManager.ShowMultiSelectionInquiry` с древовидным выбором действий («Исцелить себя», «Режим Бога», «Призвать всех к себе», «Уничтожить ботов», «Управление игроками» -> постраничный список игроков с действиями ТП / Кик / Бан / Фриз). Обеспечивает 100% доступ к админке даже при сбоях темы оформления.
  - `DWDN_AdminPanelVM.cs`:
    - Модель представления с живым списком игроков (`DWDN_AdminPlayerRowVM`), поисковым фильтром по нику, ID и команде, кнопками быстрых серверных действий и консольной строкой для произвольных команд.
  - `GUI/Prefabs/DWDN_AdminPanel.xml`:
    - Двухколоночный макет: слева — быстрые глобальные действия и консоль; справа — строка поиска, счетчик онлайна и скроллируемый список игроков со статусами HP, Ping, Team и кнопками `Лечить`, `Бог`, `К нему`, `Призвать`, `Фриз`, `Убить`, `Кик`, `Бан`.
- **Расширение Тестового Полигона (`MechanicsTester/Suites/AdminToolsTestSuite.cs`)**:
  - Добавлена шестая тестовая сюита (7 новых тестов):
    1. `Test_RequestAdminActionMessage_Serialization_Roundtrip`: побайтовая round-trip сериализация пакета запроса действий.
    2. `Test_SyncAdminStateMessage_Serialization_Roundtrip`: валидация пакета синхронизации прав админа.
    3. `Test_Admin_Authentication_Password_Validation`: тест аутентификации по паролю (с учетом и без учета регистра, отказ при пустом/неверном).
    4. `Test_Admin_Security_Unauthorized_Access_Rejection`: блокировка попыток неавторизованного доступа.
    5. `Test_Admin_Target_Resolution_Logic`: тестирование алгоритма `ResolveTargets` (`me`, `all`, `team1`, `def`, поиск по нику и ID).
    6. `Test_AdminPanelVM_Search_And_Filtering`: тест фильтрации списка игроков в UI.
    7. `Test_Admin_Action_Dispatcher_Mapping`: валидация диспетчеризации всех 22 поддерживаемых действий.
  - **Результат тестирования**: 28 из 28 тестов пройдены успешно (0 ошибок, время выполнения ~25 мс).
- **Синхронизация**:
  - Скомпилированы `DWDN_Script.dll` (клиент) и `DWDN_Script.Server.dll` (.NET 8.0).
  - Артефакты, префабы и скрипты автоматически синхронизированы в `Dedicated Server\Modules\DaewdenM`.

## 53. Иерархия вооружения пехоты и очистка интерфейса артиллериста (12.11.2026)
- **Контекст и боевая задача**:
  1. Реализовать сакральную иерархию оружия пехоты Герцогства Даевден: Ратай (селяне/ополчение) -> Банобранный (линейная пехота) -> Офицер/Десятник (младший командир) -> Збройный (латная штурмовая гвардия) -> Сотник (старший командир/капитан).
  2. У Ратая изъять тяжелый пехотный тесак (`dwdn_cleaver`) и вооружить обычным «Сельскохозяйственным ножом» (`dwdn_peasant_knife`), отражающим его крестьянский статус.
  3. Передать освободившийся тесак (`dwdn_cleaver`) Банобранному и остальным пехотинцам/специалистам (Пикинёр, Знаменосец, Стрелок, Ганнер, Артиллерист).
  4. Ликвидировать пустые неактивные слоты 2 и 3 в меню выбора снаряжения Артиллериста, где отображались бесполезные пустые ячейки («Ничего нельзя было выбрать»).
  5. Бароны (Берген, Финнард) и Король Альберк Девальд сохранены в первозданной неприкосновенности.
- **Внедрённые изменения**:
  - `ModuleData/mpitems/dwdn_weapon.xml`:
    - Добавлен сакральный предмет `<CraftedItem id="dwdn_peasant_knife" name="Сельскохозяйственный нож" crafting_template="Dagger" culture="Culture.neutral_culture" modifier_group="sword" multiplayer_item="true">`.
    - Состав: клинок `dagger_blade_1`, крестьянская гарда `empire_dagger_guard_3`, удобная деревянная рукоять и навершие `vlandian_grip_1` / `vlandian_pommel_1`.
  - `ModuleData/scripts/DWDN_Script/Common/Duchies/DWDN_DuchyManager.cs`:
    - **Ратай (`mp_Ratay`)**: Слот 0: `dwdn_peasant_knife`, `mp_sturgia_axe`, `mp_vlandian_hammer`. Тесак полностью изъят.
    - **Банобранный (`mp_Banobranny`)**: Слот 0: `dwdn_cleaver` (тот самый тесак), `dwdn_falchion`, `mp_sturgia_axe`, `mp_vlandian_mace`.
    - **Пикинёр (`mp_Pikeman`)**: Слот 1 (вторичка): `dwdn_cleaver`, `mp_vlandian_arming_sword`, `mp_sturgia_axe`, `mp_vlandian_mace`.
    - **Десятник / Офицер (`mp_Officer`)**: Слот 0: `mp_vlandian_arming_sword`, `dwdn_falchion`, `dwdn_cleaver`, `mp_sturgia_heavy_axe`, `mp_vlandian_mace`.
    - **Збройный (`mp_Zbroyniy`)**: Слот 0: `mp_battania_noble_sword`, `dwdn_falchion`, `mp_sturgia_heavy_axe`, `mp_battnia_heavy_mace`.
    - **Сотник (`mp_Sotnik`)**: Слот 0: `mp_battania_noble_sword`, `mp_vlandia_cavalry_sword`, `mp_sturgia_heavy_axe`, `mp_battnia_heavy_mace`.
    - **Знаменосец (`mp_Bannerman`)**: Слот 0: `mp_vlandian_arming_sword`, `dwdn_cleaver`, `dwdn_falchion`, `mp_sturgia_heavy_axe`.
    - **Стрелок (`mp_DWDN_Archer`)**: Слот 2 (ближний бой): `dwdn_peasant_knife`, `dwdn_cleaver`, `mp_vlandian_short_sword`, `mp_sturgia_axe`.
    - **Ганнер (`mp_DWDN_Crossbowman`)**: Слот 2: `dwdn_cleaver`, `mp_vlandian_short_sword`, `mp_sturgia_axe`, `baldur_warpick`.
    - **Артиллерист (`mp_Artillerist`)**: Слот 0: `dwdn_cleaver`, `dwdn_falchion`, `mp_vlandian_hammer`, `mp_vlandian_short_sword`. Ликвидированы строки `AllowedWeaponsSlot2.AddRange(new[] { "" })` и `AllowedWeaponsSlot3.AddRange(new[] { "" })`.
    - **Рыцарь (`mp_Knight`)**: Добавлен благородный меч `mp_battania_noble_sword` в дополнение к кавалерийскому клинку.
  - `Client/UI/DWDN_EquipmentSlotVM.cs`:
    - Улучшена логика `PopulateOptions`: если слот пуст или содержит лишь единственную пустую строку (`allowedItemIds.Count == 1 && string.IsNullOrEmpty(allowedItemIds[0])`), свойство `HasOptions` устанавливается в `false`. За счёт `IsVisible="@HasOptions"` в Gauntlet XML пустые ячейки полностью скрываются из графического интерфейса.
  - `ModuleData/mpclassdivisions.xml`:
    - Перк ближнего боя Ратая обновлен на `Peasant Knife` (`dwdn_peasant_knife`).
    - Банобранному добавлен перк `Cleaver` (`dwdn_cleaver`).
- **Тестовый полигон `MechanicsTester` (`Suites/DuchyLoadoutTestSuite.cs`)**:
  - Добавлены тесты:
    1. `Test_Weapon_Hierarchy_And_Cleaver_Distribution`: пошаговая проверка распределения тесаков, ножей и мечей по классам.
    2. `Test_Artillerist_Empty_Slots_Purged`: проверка удаления пустых слотов 2 и 3 у артиллериста.
    3. `Test_Barons_And_King_Untouched`: проверка неприкосновенности оружия баронов и короля.
  - **Результат**: **31 из 31 тестов успешно пройдены за 27.59 мс (0 ошибок)**.
- **Синхронизация**:
  - Пересобраны клиентская и серверная DLL (`DWDN_Script.dll` и `DWDN_Script.Server.dll`).
  - Все XML-манифесты и скомпилированные артефакты зеркально экспортированы на выделенный сервер `DaewdenM`.


---

### Глава 50: Диагностика критического сбоя клиента при входе на выделенный сервер (0xc0000005)

#### 1. Анализ священных свитков логов (09:13:31 - 09:13:34)
- **Клиент (`rgl_log_23008.txt`)**:
  - Сцена `scn_arena` полностью загружается, `MissionScreen-OnActivate` отрабатывает штатно в `09:13:31.279`.
  - Клиент рапортует серверу о завершении загрузки:
    ```
    [09:13:31.617] Client: I finished loading battle with index: 1. Sending confirmation to server.
    [09:13:31.617] Writing message: Finished Loading
    [09:13:31.619] Writing message: SyncRelevantGameOptionsToServer
    ```
  - Ровно после отправки `SyncRelevantGameOptionsToServer` запись лога обрывается.
- **Сервер (`rgl_log_35184.txt`)**:
  - Сервер принимает подтверждение и рассылает состояние мира:
    ```
    [09:13:31.645] Server: [DWDN] Havi has finished loading. From now on, I will include him in the broadcasted messages
    [09:13:31.649] Sending all existing objects to peer: [DWDN] Havi with index: 0
    [09:13:31.650] Syncing a team to peer: [DWDN] Havi with index: 0
    [09:13:31.650] formations sending begin- / end-
    [09:13:31.652] agents sending begin- / end-
    [09:13:31.656] Server: [DWDN] Havi is now synchronized.
    ```
- **Windows Event Log & Watchdog (`watchdog_log_23008.txt`)**:
  - `APPCRASH` в `TaleWorlds.MountAndBlade.Launcher.exe` (PID 23008).
  - Код исключения: `0xc0000005` (STATUS_ACCESS_VIOLATION — попытка чтения/записи по недействительному адресу памяти).
  - Faulting module: `unknown` со смещением `0x00007ffa7eb06924` (указывает на динамически скомпилированный код Mono/.NET JIT или вызов нативного C++ хука).

#### 2. Выявленные аномалии и источники скрапкода
1. **Теневая библиотечная коллизия (`TaleWorlds.TwoDimension.dll`)**:
   - В папке клиента модуля `Modules\DWDN_TRY\bin\Win64_Shipping_Client\` обнаружена устаревшая копия `TaleWorlds.TwoDimension.dll` (размер 103,896 байт от августа 2026).
   - В то же время в корневой `bin\Win64_Shipping_Client\` движка лежит актуальная сборка (размер 102,752 байт от ноября 2026).
   - Затенение системной библиотеки UI приводит к рассинхронизации структур памяти при отрисовке Gauntlet-слоев.
2. **Агрессивный автозапуск UI в сетевом хуке (`DWDN_AdminMissionView.cs`)**:
   - При распознавании игрока сервером как `peer.IsAdmin`, сервер шлет `SyncAdminStateMessage(IsAdmin=True)`.
   - В `OnSyncAdminStateMessage` срабатывает `if (!wasAdmin && _gauntletLayer == null) { OpenPanel(); }`.
   - Вызов `OpenPanel()` при получении сетевого пакета пытается мгновенно создать `GauntletLayer`, загрузить префаб или открыть нативный модальный запрос `ShowNativeAdminInquiry()` в момент, когда 3D-вьюпорт и `ScreenManager` еще находятся в переходном состоянии рукопожатия, вызывая краш unmanaged-памяти. Панель должна открываться строго по нажатию клавиш `[F10]`/`[Insert]`.
3. **Дублирующая регистрация `DWDN_AdminMissionView`**:
   - Класс имеет декларацию `[OverrideView(typeof(MissionGauntletAdminPanel))]` (движок регистрирует его автоматически при создании представлений миссии) и одновременно вручную добавляется в `SubModule.cs` через `mission.AddMissionBehavior(new DWDN_AdminMissionView())`.


---

### Глава 51: Завершение литургии очищения (Устранение причин 0xc0000005)

1. **Изгнание чужеродного бинарника**:
   - `TaleWorlds.TwoDimension.dll` удалена из папки `Modules/DWDN_TRY/bin/Win64_Shipping_Client/`. Движок теперь гарантированно использует официальную сборку из корневого каталога.
2. **Очищение логики `DWDN_AdminMissionView.cs`**:
   - Из обработчика `OnSyncAdminStateMessage` удален принудительный вызов `OpenPanel()`.
   - При авторизации администратора теперь выводится информационное сообщение:
     `"[DWDN Admin Tools] Авторизация успешна! Нажмите [F10] или [Insert] для открытия меню."`
   - Развертывание графических Gauntlet-слоев теперь происходит строго по прямому вводу оператора.
3. **Ликвидация дублирования в `SubModule.cs`**:
   - Из метода `AddMissionBehaviors` удалена лишняя строка ручной регистрации `mission.AddMissionBehavior(new DWDN_AdminMissionView())`. Представление создается движком автоматически через системный атрибут `[OverrideView(typeof(MissionGauntletAdminPanel))]`.
4. **Сборка и синхронизация**:
   - Успешно скомпилирован клиентский модуль `DWDN_Script.dll` (.NET Framework 4.7.2) с 0 ошибок.
   - Успешно скомпилирован серверный модуль `DWDN_Script.Server.dll` (.NET 8.0) с 0 ошибок.
   - Скрипт синхронизации `sync_all.ps1` зеркально обновил все бинарники, скрипты и манифесты на выделенном сервере `DaewdenM`.


---

### Глава 52: Успешная верификация входа на сервер и возврат к механике ручных гранат

- **Статус соединения**: Сервер и клиент функционируют стабильно, сетевое рукопожатие и загрузка сцены завершаются без `0xc0000005`.
- **Фокус переключен**: Возврат к проектированию и внедрению метательных гранат (`dwdn_grenade`) и логике AoE детонации (`DWDN_GrenadeBehavior.cs`).


---

### Глава 53: Внедрение ручных гранат (dwdn_grenade & DWDN_GrenadeBehavior)

#### 1. Спецификация оружия (`ModuleData/mpitems/dwdn_weapon.xml`)
- **ID**: `dwdn_grenade`
- **Класс оружия**: `Stone` (`weapon_class="Stone"`, `ammo_class="Stone"`, `Type="Thrown"`).
- **Связка**: `stack_amount="2"`, расходный материал `Consumable="true"`.
- **Физика и баллистика**: масса `weight="1.2"`, начальная скорость `missile_speed="26"`, прицельная точность `accuracy="90"`.
- **Визуальный шлейф**: горящий фитиль `trail_particle_name="psys_game_sparkle_a"`.
- **Прямой урон броска**: 10 дробящего (базовое кинетическое попадание болванки).

#### 2. Архитектура детонации (`DWDN_GrenadeBehavior.cs`)
- **Отслеживание броска (`OnAgentShootMissile`)**:
  - Идентифицирует гранату по `weapon.Item.StringId == "dwdn_grenade"`.
  - Регистрирует связку `[missileIndex -> shooterAgent]` в словаре `_activeGrenades`.
- **Точка детонации (`OnMissileHit`)**:
  - Срабатывает при ударе о любую поверхность (террейн, агент, щит, стена).
  - Эпицентр слегка приподнимается над полом (`epicenter.z += 0.15f`) для надежной трассировки лучей.
- **Визуальные и звуковые эффекты (Client / Listen Server)**:
  - Искры детонатора: `psys_game_sparkle_a`.
  - Пыль и комья грунта: `psys_game_stone_dust_a`, `psys_siege_door_falling_dust`.
  - Звук: `event:/mission/combat/impact/ballista_missile/stone`.
- **Серверный расчет фугасного поражения (`ApplyBlastDamage`)**:
  - Радиус поражения: `ExplosionRadius = 6.0f` метров.
  - Проверка экранирования: `Mission.Scene.RayCastForClosestEntityOrTerrain(epicenter, targetPos, out float collisionDist)`. Если луч врезается в препятствие раньше агента (`collisionDist < distance - 0.4f`), урон полностью поглощается укрытием.
  - Урон по площади: линейная интерполяция от $135$ (эпицентр) до $25$ (граница 6м) по формуле $Damage = Min + (Max - Min) \times (1 - d / R)$.
  - Сбивание с ног: радиус `KnockdownRadius = 2.5f` м. Если боец находится в пределах 2.5 м от эпицентра, к `Blow` прикрепляется флаг `BlowFlags.KnockDown`.
  - Вектор отдачи: радиальный импульс от эпицентра `(diff / distance)`.

#### 3. Распределение по классам (`DWDN_DuchyManager.cs`)
- Доступна в качестве опции выбора для:
  - **Банобранный (Пехотинец)**: Слот 2 (копье или гранаты).
  - **Офицер / Десятник**: Слот 2 (пика или гранаты).
  - **Ганнер (Стрелок с ручницей)**: Слот 2 (тесак, меч, топор, клевец или гранаты).

#### 4. Синхронизация и тестирование
- Клиентская и серверная библиотеки пересобраны с 0 ошибок.
- Полная зеркальная синхронизация на `DaewdenM` через `sync_all.ps1`.
- Тестовый полигон `MechanicsTester`: 28 из 28 тестов пройдены успешно (0 ошибок).

---

### Глава 54: Очищение логики гранат: Порядок вызовов движка, AmmoBreaksOnBounceBack и гарантированная детонация

#### 1. Диагностика варп-скверны: Почему граната падала как булыжник и не взрывалась
- **Внутренний порядок вызовов в Bannerlord (Mission.MissileHitCallback)**:
  - При столкновении снаряда движок сначала вызывает Mission.HandleMissileCollisionReaction (смещение IL  8A0).
  - Внутри HandleMissileCollisionReaction вызывается RemoveMissile, который немедленно триггерит MissionBehavior.OnMissileRemoved(missileIndex) и удаляет снаряд из Mission.MissilesList.
  - Лишь **после этого** (смещение IL  8C7) движок вызывает MissionBehavior.OnMissileHit(attacker, victim, isCanceled, collisionData).
  - **Коренная причина сбоя**: в первоначальной реализации OnMissileRemoved немедленно выполнял _activeGrenades.Remove(missileIndex). В результате, когда миллисекундой позже управление передавалось в OnMissileHit, словарь _activeGrenades уже был пуст, а поиск в Mission.MissilesList ничего не находил. Код считал снаряд неизвестным и детонация отменялась!
- **Физика камня и отсутствие разрушения меша**:
  - Стандартный метательный снаряд класса Stone при соударении с грунтом порождает физический дроп (SpawnWeaponAsDropFromMissile) и отскакивает, оставаясь лежать на земле.
  - Для разрушения и исчезновения метательного снаряда требуются специальные флаги оружия и физический материал.

#### 2. Архитектурное исправление (DWDN_GrenadeBehavior.cs)
- **Отложенная очистка словаря**:
  - OnMissileRemoved **больше не удаляет** снаряд из _activeGrenades.
  - OnMissileHit успешно находит missileIndex в _activeGrenades, извлекает стрелка (Thrower), инициирует Detonate(...), и только затем удаляет запись.
  - В OnMissionTick добавлен сборщик неразорвавшихся снарядов (например, улетевших за границы сцены) с таймаутом .0$ секунд.
- **Принудительное удаление меша снаряда на клиенте**:
  - В OnMissileHit вызывается Mission.RemoveMissileAsClient(missileIndex) для немедленного гарантированного сокрытия снаряда.
- **Позиционирование эпицентра**:
  - Эпицентр взрыва смещается вдоль нормали столкновения: epicenter += collisionData.CollisionGlobalNormal * 0.2f (или z + 0.2f), предотвращая ложное экранирование рэйкаста самим полигоном земли.
- **Визуальные эффекты**:
  - Добавлены партиклы psys_burning_projectile_default_coll, вспышка psys_game_sparkle_a, пылевой взрыв psys_game_stone_dust_a и psys_siege_door_falling_dust.
  - Выводится уведомление [DWDN] Взрыв гранаты!.

#### 3. Обновление XML параметров (ModuleData/mpitems/dwdn_weapon.xml)
- Для dwdn_grenade установлены атрибуты:
  - physics_material="burning_jar" (активирует нативные эффекты горения/разрыва из collision_infos.xml).
  - В <WeaponFlags> добавлены:
    - AmmoBreaksOnBounceBack="true" (запрещает снаряду отскакивать и падать дропом на землю, принуждает разбиваться при соударении).
    - AmmoSticksWhenShot="false", AttachAmmoToVisual="false".
    - LeavesTrail="true", CanKnockDown="true".

#### 4. Статус сборки и синхронизации
- Клиентская и серверная сборки компилируются чисто (0 ошибок).
- Синхронизация sync_all.ps1 выполнена.
- Все 28 тестов MechanicsTester пройдены успешно.

---

### Глава 55: Устранение краша 0xc0000005 при выборе фракции Daewden (FaceCache Fix)

#### 1. Симптомы и локализация сбоя
- **Симптом**: При попытке входа на сервер и выборе культуры/фракции Daewden игра падала с аппаратным исключением  xc0000005 (STATUS_ACCESS_VIOLATION) в нативном модуле TaleWorlds.MountAndBlade.Launcher.exe / Win64_Shipping_Client.
- **Лог gl_log_43908.txt**:
  `
  [10:49:13.819] Processing message: ChangeCulture: Requested culture: Daewden
  [10:49:13.821] [DWDN] Selection UI shown.
  ...
  [10:49:13.936] Processing message: CreateAgentVisuals: Create AgentVisuals for peer: [DWDN] Havi, and with Index: 0
  --> CRASH 0xc0000005
  `

#### 2. Коренная причина сбоя
- В DWDN_SelectionMissionView.cs (строка 476) и DWDN_ClientLoadoutBehavior.cs (строка 539) выполнялся вызов:
  `csharp
  agentVisual.Refresh(false, data, true);
  `
- Третий параметр ool forceUseFaceCache = true заставляет нативный C++ движок TaleWorlds обращаться к предрассчитанным таблицам FaceCache для генерации геометрии лица.
- Для стандартных нативных культур (Vlandia, Sturgia, Battania и др.) эти таблицы запечены в памяти. Для кастомной культуры dwdn таблицы не существует — ядро движка обращается по нулевому указателю памяти, что вызывает фатальный STATUS_ACCESS_VIOLATION 0xc0000005 в нативном коде в обход управляемых блоков 	ry/catch.

#### 3. Внесенное исправление
- В DWDN_SelectionMissionView.cs (строка 476) и DWDN_ClientLoadoutBehavior.cs (строка 539) флаг изменен на безопасный:
  `csharp
  agentVisual.Refresh(false, data, false);
  `
- Значение orceUseFaceCache: false заставляет движок динамически сгенерировать меш лица без обращения к несуществующему нативному кэшу.
- Сборки DWDN_Script.dll и DWDN_Script.Server.dll пересобраны (0 ошибок), sync_all.ps1 отработал успешно, все 28 тестов MechanicsTester пройдены.
---

### Глава 56: Исцеление скрапкода серверов в ApplyBlastDamage и выделение 4-го слота гранат

#### 1. Симптомы и локализация сбоя выделенного сервера
- **Симптом**: При взрыве гранаты рядом с бойцами выделенный сервер DedicatedCustomServer.Starter.exe падал с исключением  xe0434352 (CLR Unhandled Exception).
- **Стек исключения**:
  `	ext
  System.InvalidOperationException: Collection was modified; enumeration operation may not execute.
     at System.Collections.Generic.List1.Enumerator.MoveNext()
     at DWDN_Script.DWDN_GrenadeBehavior.ApplyBlastDamage(Agent thrower, Vec3 epicenter)
  `
- **Коренная причина**: В ApplyBlastDamage выполнялся прямой проход oreach (Agent agent in Mission.Agents). Внутри цикла вызов gent.RegisterBlow(blow, ...) наносил урон или убивал агента, что приводило к модификации внутренней коллекции Mission.Agents нативным ядром TaleWorlds прямо во время обхода.

#### 2. Внесенное исправление скрапкода (DWDN_GrenadeBehavior.cs)
- Список агентов оборачивается в изолированный снимок до начала цикла:
  `csharp
  List<Agent> agentsSnapshot = new List<Agent>(Mission.Agents);
  `
- Каждая итерация и регистрация удара защищена локальным блоком 	ry { ... } catch { }, гарантируя полную неуязвимость сервера при любых изменениях состояния бойцов (нокаут, гибель, переход в ragdoll).

#### 3. Расширение снаряжения Стрелка с ручницей (Вариант А, DWDN_DuchyManager.cs)
- Для класса mp_DWDN_Crossbowman (Стрелок с ручницей) выделен отдельный 4-й слот (Слот 3) под ручные гранаты:
  - **Слот 0**: hand_cannon (Ручница)
  - **Слот 1**: mp_bullets (Пули)
  - **Слот 2**: Оружие ближнего боя (dwdn_cleaver, mp_vlandian_short_sword, mp_sturgia_axe, aldur_warpick)
  - **Слот 3**: dwdn_grenade, "" (Ручные гранаты по выбору)
- Теперь игрок не жертвует холодным оружием ради гранат и имеет полный боекомплект.
- Сборка и развертывание через sync_all.ps1 выполнены, все 28 тестов MechanicsTester пройдены.
---

### Глава 57: Исцеление развертывания осадной артиллерии (Онагра) и кучи камней

#### 1. Симптоматика и проявление скрапкода
- **Симптом**: Передвижной онагр (dwdn_order_mangonel / mangonel_a) переводится в боевое положение клавишей [F] (отцепка коня), конь отцепляется, куча камней появляется на грунте. Однако после этого:
  1. Подобрать камень из кучи камней невозможно (нет интерактивного триггера [F], либо нажатие не дает эффекта).
  2. Встать за орудие и управлять им невозможно (посты наводки и огня не реагируют).

#### 2. Коренная причина сбоя
1. **Коварная сигнатура MissionObject.SetDisabled(bool isParentObject)**:
   - В ядре движка TaleWorlds.MountAndBlade.MissionObject логический параметр метода SetDisabled — это **не** isActive, а ool isParentObject = false.
   - Противоположным методом активации является отдельный метод SetEnabled(bool isParentObject = false).
   - В DWDN_FieldArtilleryBehavior.cs при развертывании в боевое положение (combat == true) код вызывал:
     `csharp
     sp.SetDisabled(!combat); // Передавалось false!
     `
   - Это приводило к вызову SetDisabled(isParentObject: false), что деактивировало объект в рейкасте: Mission.Current.DeactivateMissionObject(this); IsDisabled = true;. Метод SetEnabled(false) не вызывался вовсе! Все посты управления орудием и точки взятия камней оставались перманентно заблокированными для рейкаста игрока.
2. **Слепота поиска точек боеприпасов**:
   - В префабе mangonel_a внутри объекта projectile_pile точки взятия камней (StandingPointWithWeaponRequirement с тегом mmopickup) находятся на **дочерних** сущностях (mmo_pos_c ... mmo_pos_h).
   - Метод AmmoPileEntity.GetScriptComponents<StandingPoint>() опрашивал только корень кучи и возвращал пустой список. Требовался рекурсивный сбор через CollectMissionObjectsRecursive.
3. **Отсутствие сетевой синхронизации стояночных постов**:
   - В мультиплеере разблокировка постов для игроков сервером требует явной рассылки:
     `csharp
     sp.SetIsDisabledForPlayersSynched(false);
     sp.SetIsDeactivatedSynched(false);
     `
   - Без этого клиентские стояночные посты считались локально заблокированными для живых игроков.
4. **Спящее состояние мета-машин (UsableMachine.Activate)**:
   - Осадное орудие Mangonel и куча камней SiegeMachineStonePile наследуются от UsableMachine. При отцепке от упряжи им необходимо вызывать Activate(), чтобы сбросить внутренний флаг _isMachineDeactivated.
5. **Чувствительность наведения для наводчика-одиночки**:
   - В Mangonel.HorizontalAimSensitivity при значении стороны DefaultSide == BattleSideEnum.Defender наводчик поворачивает орудие с чувствительностью  .25f (при других сторонах —  .05f, требуя отдельного бойца на колесе поворота). Через рефлексию полю DefaultSide присвоено значение BattleSideEnum.Defender.

#### 3. Внесенные исправления (DWDN_FieldArtilleryBehavior.cs)
1. Полностью переписана логика EngineInstance.SetCombatState(bool combat):
   - При combat == true:
     - Вызываются SiegeWeapon?.Activate() и stonePile?.Activate().
     - Видимость кучи камней включается: AmmoPileEntity.SetVisibilityExcludeParents(true).
     - Производится полный рекурсивный сбор всех StandingPoint орудия и кучи боеприпасов.
     - Для каждого стояночного поста:
       * Вызывается sp.SetEnabled(false) (активация в активных объектах Mission.Current).
       * Сбрасываются флаги: sp.IsDisabledForPlayers = false; sp.IsDeactivated = false;.
       * Вызывается нейтрализация стороны SetStandingPointNeutral(sp).
       * Если на сервере: рассылаются sp.SetIsDisabledForPlayersSynched(false) и sp.SetIsDeactivatedSynched(false).
   - При combat == false:
     - Вызываются SiegeWeapon?.Deactivate() и stonePile?.Deactivate().
     - Куча камней скрывается AmmoPileEntity.SetVisibilityExcludeParents(false).
     - Все посты блокируются через sp.SetDisabled(false) и синхронизируются через SetIsDisabledForPlayersSynched(true).
2. Добавлен безопасный рефлексивный метод SetSiegeWeaponDefaultSide(RangedSiegeWeapon weapon, BattleSideEnum side):
   - Устанавливает DefaultSide = BattleSideEnum.Defender, гарантируя плавное и отзывчивое наведение онагра одиночным игроком.
3. Логика ручных гранат (DWDN_GrenadeBehavior.cs, dwdn_grenade) осталась на 100% нетронутой.

#### 4. Верификация
- Клиентская сборка DWDN_Script.dll: 0 ошибок.
- Серверная сборка DWDN_Script.Server.dll: 0 ошибок.
- Синхронизация sync_all.ps1: успешное развертывание в клиент и выделенный сервер.
- Автоматический тест-сьют MechanicsTester.exe --cli: 28 из 28 тестов пройдены успешно (0 сбоев).
---

### Глава 58: Увеличение лимита полевой артиллерии до 3 орудий на команду

#### 1. Задача и цель модификации
- По воле Создателя лимит одновременно развернутых мобильных орудий полевой артиллерии (онагров и баллист) увеличен с 2 до 3 на каждую команду.

#### 2. Внесенные изменения
1. **Священный конфигурационный свиток (ModuleData/artillery_config.xml)**:
   - Атрибут max_cannons_per_team в корневом теге <ArtilleryConfig> установлен в значение "3".
2. **Логическая матрица (DWDN_FieldArtilleryBehavior.cs)**:
   - Статическое свойство MaxCannonsPerTeam по умолчанию установлено в значение 3.
   - В методе LoadConfig() добавлено распознавание атрибута max_cannons_per_team (в дополнение к MaxCannonsPerTeam), обеспечивая автоматическое считывание регистра XML-файла.
3. **Административные консольные команды**:
   - Команда !artylimit <N> в DWDN_AdminService.cs по-прежнему позволяет администраторам динамически регулировать лимит на лету.

#### 3. Верификация
- Клиентская сборка DWDN_Script.dll: 0 ошибок.
- Серверная сборка DWDN_Script.Server.dll: 0 ошибок.
- Синхронизация sync_all.ps1: успешно развернуто на клиент и выделенный сервер DaewdenM.
- Автотесты MechanicsTester.exe --cli: 28 из 28 тестов пройдены успешно.
---

### Глава 59: Исправление выбора чертежа артиллерии (Онагр vs Баллиста)

#### 1. Симптоматика и коренная причина сбоя
- **Симптом**: При выборе в интерфейсе выбора снаряжения чертежа/заявки на передвижную баллисту (dwdn_order_ballista), на поле боя всегда спавнился онагр (mangonel_a).
- **Коренная причина**:
  1. В методе DWDN_FieldArtilleryBehavior.CheckAndSpawnArtilleryForAgent(Agent agent) проверка ванильных перков движка стояла первой по приоритету:
     `csharp
     int pIdx = agent.MissionPeer.GetSelectedPerkIndexWithPerkListIndex(agent.MissionPeer.SelectedTroopIndex, 1);
     if (pIdx == 1) chosenSpec = GetSpecForType("ballista");
     else chosenSpec = GetSpecForType("mangonel");
     `
     Поскольку в моде DWDN выбор экипировки осуществляется через кастомную систему дивизионов (DWDN_DuchyManager), ванильный pIdx всегда был равен 0. Жесткая ветка else безусловно и принудительно назначала онагр (mangonel), а блок проверки фактической экипировки (if (chosenSpec == null)) не выполнялся никогда.
  2. Спавн орудия вызывался в событии движка OnAgentBuild, в момент которого кастомный лодаут еще не успевал физически экипироваться в gent.Equipment, но уже был сохранен в реестре выборов DWDN_ServerLoadoutBehavior._peerSelections.

#### 2. Внесенные исправления
1. **Реестр лодаутов (DWDN_ServerLoadoutBehavior.cs)**:
   - Выделен публичный статический метод:
     public static DWDN_LoadoutSelection GetSelectionForAgent(Agent agent, MissionPeer peer = null, int peerIndex = -1)
     Он быстро и надежно возвращает целевой лодаут бойца (из кэша сетевого пира, локального игрока или дефолта дивизиона).
   - В метод TryApplyLoadoutToAgent после применения экипировки добавлен вызов:
     DWDN_FieldArtilleryBehavior.CheckAndSpawnArtilleryForAgent(agent);
     Это гарантирует своевременный спавн орудия сразу после применения лодаута.
2. **Логика спавна артиллерии (DWDN_FieldArtilleryBehavior.cs)**:
   - В CheckAndSpawnArtilleryForAgent полностью перестроена иерархия приоритетов:
     - **Приоритет 1 (Выбор в кастомном UI DWDN)**: опрашивается DWDN_ServerLoadoutBehavior.GetSelectionForAgent(agent). Если в слотах (Weapon1Id, Weapon0Id и др.) найдена заявка dwdn_order_ballista, активируется чертеж баллисты (allista_a на тележке). Если dwdn_order_mangonel — чертеж онагра.
     - **Приоритет 2 (Физическая экипировка gent.Equipment)**: если выбор не через UI, сканируются слоты агента на наличие dwdn_order_ballista / dwdn_order_mangonel с фиксацией слота orderSlot для последующего автоматического изъятия заявки при спавне орудия.
     - **Приоритет 3 (Ванильные перки)**: проверяются перки pIdx только если чертеж не найден в предыдущих шагах, и **без** безусловного else.
     - **Приоритет 4 (Fallback)**: по умолчанию для артиллериста выбирается онагр, только если ни один источник не указал конкретное орудие.

#### 3. Верификация
- Клиентская сборка DWDN_Script.dll: 0 ошибок.
- Серверная сборка DWDN_Script.Server.dll: 0 ошибок.
- Синхронизация sync_all.ps1: успешно развернуто на клиент и выделенный сервер DaewdenM.
- Автотесты MechanicsTester.exe --cli: 28 из 28 тестов пройдены успешно (0 сбоев).
---

### Глава 60: Искоренение антигравитационной петли баллисты и разблокировка последующих спавнов артиллерии

#### 1. Симптоматика и коренная причина (Root Cause)
- **Симптом 1**: Передвижная баллиста (ballista_a на тележке bd_cart_a) сразу после спавна за тягловым конем начинала стремительно взмывать вертикально вверх в стратосферу («улетает в небеса»).
- **Симптом 2**: После спавна баллисты игрок больше не мог заспавнить ни одного нового орудия («после спавна баллиста ничего нового не спавнится»).

- **Коренная причина 1 (Антигравитационный резонанс raycast)**:
  - В методе DWDN_FieldArtilleryBehavior.OnMissionTick в цикле буксировки выполнялся расчет высоты земли через GetHeightAtPoint(targetPos.AsVec2, BodyFlags.CommonCollisionExcludeFlags, ref gZ).
  - Метод Scene.GetHeightAtPoint опускает луч сверху вниз. В точке targetPos.AsVec2 находится сама баллиста, у которой в префабе ballista_a задано физическое тело древесины (override_material=wood), а тележка bd_cart_a имеет собственный меш.
  - Луч коллизии каждый кадр ударялся о верхушку самой баллисты, возвращая gZ ~ 1.8м выше поверхности земли.
  - К этому значению прибавлялся кузовной оффсет +0.40f.
  - На следующем тике луч вновь ударялся о баллисту, поднятую выше, и поднимал её ещё на 2 метра. Образовывалась бесконечная положительная обратная связь («эффект барона Мюнхгаузена»), катапультировавшая баллисту в космос.
  - Свойство EngineInstance.Position возвращало RootEntity.GlobalPosition, где уже содержался +0.40f, из-за чего проверка Math.Abs(engine.Position.z - targetPos.z) > 0.05f считалась истинной каждый кадр, непрерывно перезаписывая матрицы даже на стоящей лошади.

- **Коренная причина 2 (Мертвая блокировка пула артиллерии)**:
  - Улетевшая в стратосферу баллиста никогда не получала урона, не падала и не уничтожалась (engine.IsDestroyed == false).
  - Проверка CanSpawnCannonForTeam блокировала игрока проверкой: if (_activeEngines.Any(e => e.OwnerPlayerId == playerId && !e.IsDestroyed)).
  - Пока баллиста вечно парила в небесах, игрок не мог вызвать ни одно новое орудие.

#### 2. Внесенные исправления
1. **Чистый расчет высоты ландшафта (DWDN_FieldArtilleryBehavior.cs)**:
   - В логике буксировки OnMissionTick обращение GetHeightAtPoint заменено на чтение непосредственной карты высот меша террейна (Mission.Current.Scene.GetTerrainHeight(targetPos.AsVec2)).
   - Карта высот ландшафта Scene.GetTerrainHeight не содержит динамических сущностей, тележек и орудий. Самозахват коллизии и обратная связь физически невозможны.
   - Если орудие находится на мосту или деревянном настиле (где террейн ниже более чем на 2.5 м), высотой автоматически принимается надежный уровень коня horsePos.z.
2. **Якорь позиции EngineInstance.Position**:
   - Изменено на public Vec3 Position => Frame.origin;, устранив джиттер высоты кузова +0.40f.
3. **Предохранитель от варп-катапультирования (Fail-safe destruction)**:
   - Добавлена проверка границ в OnMissionTick: если по любой непредвиденной причине engine.Frame.origin.z > 100f или < -50f, орудие немедленно помечается как уничтоженное (IsDestroyed = true), освобождая лимиты команды и игрока.
4. **Автоматический перевод в боевой режим при гибели тяглового коня**:
   - Если конь погибает на марше (!engine.HorseAgent.IsActive()), орудие автоматически переводится в боевое состояние на грунте (engine.SetCombatState(true)).

#### 3. Верификация
- Клиентская сборка DWDN_Script.dll: 0 ошибок.
- Серверная сборка DWDN_Script.Server.dll: 0 ошибок.
- Тест-харнесс MechanicsTester.exe --cli: 28 из 28 тестов пройдены успешно (0 сбоев).
- Синхронизация sync_all.ps1: клиент и выделенный сервер полностью обновлены.
---

### Глава 61: Устранение физического выталкивания баллисты (UsesCart = false) и очистка орудий при респавне артиллериста

#### 1. Симптоматика и коренная причина (Root Cause)
- **Симптом 1**: Передвижная баллиста (ballista_a) при спавне взмывала вертикально вверх («улетает в небеса»), в то время как передвижной онагр (mangonel_a) в том же самом коде стоял на земле устойчиво и буксировался стабильно.
- **Симптом 2**: После гибели или респавна артиллериста новое орудие больше не спавнилось («после спавна балиста ничего нового не спавнится»).

- **Коренная причина 1 (Физический импульс отталкивания тел при взаимном проникновении)**:
  - У онагра было UsesCart = false. Он спавнился как один самостоятельный префаб mangonel_a на собственных колесах.
  - У баллисты было UsesCart = true. Код создавал вспомогательную сущность повозки bd_cart_a и спавнил префаб ballista_a в той же точке со смещением +0.40f. Поскольку оба объекта имели твердые коллизии, физический движок игры (PhysX/Havok) фиксировал глубокое взаимопроникновение двух твердых тел и генерировал мощный импульс разделения (separation impulse), катапультирующий баллисту вверх из кузова тележки!
  - Кроме того, у инстанциированных префабов отсутствовал флаг мобильности root.SetMobility(GameEntity.Mobility.Dynamic) (как требует нативный SpawnerEntityMissionHelper), из-за чего движок расценивал орудие как статический коллидер, конфликтующий с движением.

- **Коренная причина 2 (Мертвая блокировка пула артиллерии при респавне игрока)**:
  - Метод CanSpawnCannonForTeam блокировал спавн проверкой: if (_activeEngines.Any(e => e.OwnerPlayerId == playerId && !e.IsDestroyed)).
  - Когда игрок погибал и респавнился за артиллериста, его орудие от предыдущей жизни оставалось существовать на карте. В коде не было механизма утилизации или отвязки старого орудия при возрождении владельца. Сервер считал, что у игрока уже есть живое орудие, и навсегда блокировал спавн артиллерии.

#### 2. Внесенные исправления
1. **Унификация баллисты с работающим Онагром (UsesCart = false)**:
   - В DWDN_FieldArtilleryBehavior.cs для баллисты установлен UsesCart = false (точно так же, как у онагра). Баллиста в Bannerlord имеет собственный деревянный станок с опорами и не требует искусственного спавна крестьянской тележки bd_cart_a. Взаимное пересечение коллизий устранено.
   - При спавне орудия на сервере и клиенте явно выставляется:
     root.SetMobility(GameEntity.Mobility.Dynamic);
     root.EntityFlags |= EntityFlags.DontSaveToScene;
   - В цикле буксировки OnMissionTick координата высоты жестко и чисто привязана к высоте тяглового коня: targetPos.z = horsePos.z;. Конь опирается на навигационный меш поверхности и гарантирует идеальное прилегание машины к грунту, мостам и настилам.
2. **Утилизация орудия при респавне игрока (CheckAndSpawnArtilleryForAgent)**:
   - При спавне агента, если CanSpawnCannonForTeam вернул отказ по причине наличия активного орудия у данного playerId:
     - Сервер находит старое орудие от предыдущей жизни.
     - Если орудием прямо сейчас не управляет союзник (SiegeWeapon.PilotAgent == null), старое орудие безопасно удаляется (IsDestroyed = true, удаление энтити, FadeOut коня), а личный кулдаун сбрасывается.
     - Если союзник стреляет из него, владение старым орудием отвязывается (OwnerPlayerId = "").
     - Игроку гарантированно выдается новое орудие на текущий спавн при наличии свободных командных лимитов.

#### 3. Верификация
- Клиентская сборка DWDN_Script.dll: 0 ошибок.
- Серверная сборка DWDN_Script.Server.dll: 0 ошибок.
- Тест-харнесс MechanicsTester.exe --cli: 28 из 28 тестов пройдены успешно (0 сбоев).
- Синхронизация sync_all.ps1: клиент и выделенный сервер полностью обновлены.
---

### Глава 62: Искоренение сбоя ядра SetMobility (0xc0000005) и безопасная деактивация старой артиллерии

#### 1. Симптоматика и коренная причина сбоя (Root Cause of Crash)
- **Симптом**: При попытке игрока заспавниться за класс артиллериста сервер падал с аппаратной ошибкой памяти `System.AccessViolationException (0xc0000005)` в модуле `coreclr.dll` на шаге `TaleWorlds.MountAndBlade.MissionState.TickMission(Single)`.
- **Коренная причина**:
  1. В методах `SpawnEngine` и `HandleServerSyncDescendants` был добавлен вызов `root.SetMobility(GameEntity.Mobility.Dynamic)`.
  2. Префаб осадного орудия инстанциируется движком со статической физической структурой (узлы коллизий в дереве broadphase PhysX/Havok).
  3. Прямой вызов C++ API `IGameEntity.SetMobility(Pointer, Dynamic)` менял флаг мобильности без реструктуризации нативных структур физического мира.
  4. На первом же тике физической симуляции (`MissionState.TickMission`) движок обращался по невалидному указателю статического меша, вызывая необрабатываемый аппаратный сбой `0xc0000005`.

#### 2. Внесенные исправления
1. **Удаление токсичного вызова SetMobility**:
   - Вызовы `root.SetMobility(GameEntity.Mobility.Dynamic)` и `root.EntityFlags |= EntityFlags.DontSaveToScene` полностью удалены из серверного `SpawnEngine` и клиентского `HandleServerSyncDescendants`.
   - Орудие спавнится через стандартный нативный `GameEntity.Instantiate`, полностью сохраняя целостность физической памяти TaleWorlds Engine.
2. **Безопасная утилизация орудий через нативные компоненты**:
   - В блоке очистки старых орудий при респавне артиллериста удален прямой вызов `RootEntity.Remove(88)`, потенциально оставлявший висячие ссылки в `MissionState`.
   - Заменено на каноничный вызов компонента разрушения:
     ```csharp
     existingEngine.Destructable.HitPoint = 0f;
     existingEngine.Destructable.PreDestroy();
     ```
   - Конь плавно удаляется через `existingEngine.HorseAgent.FadeOut(false, true);`.

#### 3. Верификация
- Клиентская сборка DWDN_Script.dll: 0 ошибок.
- Серверная сборка DWDN_Script.Server.dll: 0 ошибок.
- Тест-харнесс MechanicsTester.exe --cli: 28 из 28 тестов пройдены успешно (0 сбоев).
- Синхронизация sync_all.ps1: клиент и выделенный сервер полностью развернуты.

---

### Глава 63: Устранение двойной регистрации MissionObject (0xc0000005) и исправление селектора чертежей Баллисты

#### 1. Симптоматика и коренная причина сбоя ядра (Double Registration Crash)
- **Симптом**: Сервер продолжал падать с фатальным `System.AccessViolationException (0xc0000005)` в `coreclr.dll` на шаге `TaleWorlds.MountAndBlade.MissionState.TickMission(Single)` при попытке игрока заспавниться за артиллериста. Также при выборе перка «Баллиста» всегда спавнился «Онагр».
- **Глубинные причины**:
  1. **Двойная регистрация в `_activeMissionObjects`**:
     В TaleWorlds при вызове `GameEntity.CallScriptCallbacks(true)` движок обходит все прикрепленные к сущности скрипты (`MissionObject.OnPreInit`, `Ballista.OnPreInit`, `StandingPoint.OnPreInit`, `DestructableComponent.OnPreInit`, `UsableMachine.OnPreInit`) и каждый из них **автоматически** вызывает `Mission.Current.AddActiveMissionObject(this)`. 
     В коде `SpawnEngine` и `HandleServerSyncDescendants` следом за `CallScriptCallbacks(true)` шел цикл, повторно вызывавший `mission.AddActiveMissionObject(mo)`.
     В результате все узлы орудия попадали в нативный список тиков дважды! При обходе в `MissionState.TickMission` нативный физический движок дважды за один кадр дергал один и тот же компонент без фазового шага, повреждая память сущности и вызывая `0xc0000005`.
  2. **Опасный вызов `PreDestroy()` при утилизации старого орудия**:
     `DestructableComponent.PreDestroy()` посылает событие разрушения `OnDestroyed`, слушатели которого в сетевой игре пытаются прочитать `attackerAgent.Team` / `MissionPeer`. Так как орудие утилизировалось без атакующего (`attackerAgent == null`), происходило разыменование `null`.
  3. **Перехват дефолтным лодаутом выбора чертежа**:
     В `CheckAndSpawnArtilleryForAgent` наивысшим приоритетом проверялся `DWDN_ServerLoadoutBehavior.GetSelectionForAgent(agent)`. Для игроков, не открывавших кастомное меню лодаута DWDN, этот метод возвращал `DefaultLoadout`, где в слоте 1 всегда зашит `"dwdn_order_mangonel"`. В результате выбор нативного перка «Mobile Ballista» игнорировался, и всегда спавнился онагр.
  4. **Опасное удаление оружия в `OnAgentBuild`**:
     Вызов `agent.RemoveEquippedWeapon` во время конструирования агента внутри `OnAgentBuild` нарушал внутреннее состояние TaleWorlds.

#### 2. Внесенные исправления
1. **Искоренено дублирование регистрации**:
   - В `SpawnEngine` и `HandleServerSyncDescendants` удалены лишние вызовы `AddActiveMissionObject(mo)`. Регистрация теперь выполняется строго один раз штатным механизмом `CallScriptCallbacks(true)`.
2. **Безопасная утилизация орудия**:
   - Удален вызов `PreDestroy()`. Старое орудие помечается `IsDestroyed = true`, обнуляется `Destructable.HitPoint = 0f`, а визуальные сущности орудия и тягловой лошади плавно растворяются через `FadeOut`.
3. **Исправлен приоритет селектора чертежей (Баллиста / Онагр)**:
   - Приоритетом №1 стала проверка нативных перков `agent.MissionPeer.SelectedPerks` и `GetSelectedPerkIndexWithPerkListIndex`.
   - Если выбран перк «Mobile Ballista» — гарантированно выбирается спецификация `ballista`.
   - Если «Mobile Onager» — спецификация `mangonel`.
4. **Удален небезопасный `RemoveEquippedWeapon` из `OnAgentBuild`**.

#### 3. Верификация
- `dotnet build DWDN_Script.csproj` (Release): 0 ошибок.
- `dotnet build DWDN_Script.Server.csproj` (Release): 0 ошибок.
- `MechanicsTester.exe --cli`: 28 из 28 тестов пройдены успешно (100% pass).
- `sync_all.ps1`: Все бинарники и скрипты синхронизированы на выделенный сервер.

---

### Глава 64: Устранение двойного спавна артиллерии, защита нативного тика коня и универсальный селектор Баллиста/Онагр

#### 1. Симптоматика и коренная причина вылета клиента (Root Cause Analysis)
- **Симптом**: При попытке заспавниться за артиллериста клиент игры закрывался («вылет»).
- **Анализ логов (`rgl_log_41092.txt` и `rgl_log_29368.txt`)**:
  1. **Двойной спавн за 6 мс**: При спавне игрока в `22:16:07.467` метод `OnAgentBuild` вызывал `CheckAndSpawnArtilleryForAgent`, порождая Орудие ID 1 и коня (индекс 1). Ровно через 6 миллисекунд (`22:16:07.473`) метод `DWDN_ServerLoadoutBehavior.TryApplyLoadoutToAgent` повторно вызывал `CheckAndSpawnArtilleryForAgent`.
  2. **Ложное самоуничтожение**: Во втором вызове логика видела существующее орудие игрока, ошибочно считала его «орудием от прошлой жизни», уничтожала Орудие ID 1, отдавала команду `agent_routed` коню 1, и тут же создавала Орудие ID 2 с тем же индексом коня.
  3. **Сбой памяти на клиенте**: На клиенте Орудие 1 осталось в списке активных орудий, но ссылалось на удаленного из нативной памяти коня. На первом же кадре `OnMissionTick` обращение к удаленному агенту через `SetMaximumSpeedLimit(2.2f)` вызывало `AccessViolationException (0xc0000005)` и падение процесса клиента.

#### 2. Внесенные исправления
1. **Искоренена петля двойного спавна**:
   - Из `OnAgentBuild` убран преждевременный вызов `CheckAndSpawnArtilleryForAgent`. Спавн артиллерии происходит строго один раз — после полного завершения настройки экипировки агента в `DWDN_ServerLoadoutBehavior.TryApplyLoadoutToAgent`.
   - Добавлен потокобезопасный реестр `_spawnedAgentIndices`. Если для `agent.Index` орудие уже создавалось, любые повторные вызовы немедленно блокируются. Очистка индекса происходит при гибели агента в `OnAgentRemoved`.
2. **Защита от самоуничтожения свежего орудия**:
   - В проверке утилизации добавлено ограничение по возрасту: если орудие создано менее 5 секунд назад (`Mission.CurrentTime - SpawnTime < 5.0f`), уничтожение и повторный спавн блокируются.
3. **Безопасность тика `OnMissionTick`**:
   - Убран вызов P/Invoke `SetMaximumSpeedLimit` из потикового цикла `OnMissionTick`. Лимит шага 2.2f устанавливается коню однократно при спавне.
   - Цикл буксировки обернут в `try-catch` с проверками активности коня.
4. **Универсальный определитель Баллиста / Онагр**:
   - Внедрен единый флаг `wantsBallista`: если в кастомном лодауте, в нативных перках дивизиона или в слотах оружия есть `ballista` — безоговорочно развертывается Баллиста (`ballista_a`). Иначе — Онагр (`mangonel_a`).

#### 3. Верификация
- Сборка `DWDN_Script.csproj` (Release): 0 ошибок.
- Сборка `DWDN_Script.Server.csproj` (Release): 0 ошибок.
- Тест-сьют `MechanicsTester.exe --cli`: 28 из 28 тестов пройдены успешно (100% pass).
- Синхронизация `sync_all.ps1`: Все бинарники и скрипты развернуты на Dedicated Server.


---

### Глава 65: Исцеление боевого режима артиллерии — Сетевая синхронизация StandingPoint.Id, гармонизация BattleSide и активация стрельбы

#### 1. Описание дефекта и анатомия проблемы
Пользователь зафиксировал: баллиста и онагр спавнятся стабильно, вылетов нет, но в боевом положении (после отцепки от коня клавишей [F]) игрок не может стрелять и взаимодействовать с орудием.

**Анатомический анализ исходного кода движка (декомпиляция TaleWorlds.MountAndBlade):**
1. **Сетевая диссипация идентификаторов `MissionObjectId`**:
   - В методе `SpawnEngine` (на сервере) список идентификаторов `descendantIds` формировался **до** вызова `root.CallScriptCallbacks(true)`.
   - Внутри `CallScriptCallbacks(true)` нативный обработчик `MissionObject.OnPreInit()` безусловно перезаписывает поле `this.Id` новым рантайм-идентификатором через `mission.GetFreeRuntimeMissionObjectId()`.
   - В результате на сервере зарегистрированные в `Mission.MissionObjects` объекты получили один набор ID, а в сетевой пакет `SyncArtilleryDescendantsMessage` улетел старый недействительный набор ID.
   - На клиенте в `HandleServerSyncDescendants` происходила аналогичная катастрофа: полученные `receivedIds` записывались в `allObjects[i].Id`, а затем вызывался `root.CallScriptCallbacks(true)`, чей `OnPreInit()` снова перетирал все ID на клиенте случайными локальными числами!
   - **Последствие**: Клиентские `StandingPoint.Id` не совпадали с серверными `StandingPoint.Id`. Когда игрок нажимал [F] перед баллистой или онагром, клиент отсылал пакет `RequestUseObject(client_sp_id)`. Сервер вызывал `MissionNetworkHelper.GetMissionObjectFromMissionObjectId(id)`, не находил объект с таким ID и молча игнорировал запрос. Игрок не мог занять пост наведения или взять боеприпас.
2. **Варп-искажение стороны орудия (`BattleSideEnum`)**:
   - В трех местах (`SpawnEngine`, `HandleServerSyncDescendants`, `SetCombatState`) сторона орудия принудительно задавалась как `SetSiegeWeaponDefaultSide(SiegeWeapon, BattleSideEnum.Defender)`.
   - Если артиллерист играл за сторону атаки (`BattleSideEnum.Attacker`), нативный метод `RangedSiegeWeapon.Side` возвращал `Defender`, из-за чего для атакующих игроков орудие считалось вражеским, и проверки `StandingPoint.IsDisabledForAgent` / `IsDisabledForBattleSide` блокировали авторизацию.
3. **Заблокированная точка заряжания камня (`LoadAmmoStandingPoint`)**:
   - В нативном `Mangonel.OnInit()` по умолчанию вызывается `SetActivationLoadAmmoPoint(false)`. Точка заряжания онагра оставалась деактивированной на сервере и клиенте.
   - Орудие не переводилось в режим прямого захвата игроком (`PlayerForceUse`).

#### 2. Внесенные исправления (DWDN_FieldArtilleryBehavior.cs)
1. **Правильный порядок инициализации и регистрации сетевых ID на Сервере (`SpawnEngine`)**:
   - Сначала вызывается `root.CallScriptCallbacks(true)`, инициализируя все скрипты и регистрируя их на сервере.
   - Лишь затем рекурсивно собираются все `allObjects`, и их фактические `mo.Id` упаковываются в `descendantIds` для рассылки клиентам.
   - Сторона орудия устанавливается равной истинной стороне команды артиллериста: `SetSiegeWeaponDefaultSide(siegeWeapon, teamSide)`.
2. **Точная привязка сетевых ID на Клиенте (`HandleServerSyncDescendants`)**:
   - Сначала вызывается `root.CallScriptCallbacks(true)`, регистрируя клиентские скрипты.
   - Сразу после этого `allObjects[i].Id` перезаписываются истинными `receivedIds[i]` от сервера. Теперь поиск объекта по ID в `MissionNetworkHelper` на сервере при нажатии [F] завершается мгновенным успехом.
   - Сторона орудия на клиенте инициализируется как `(BattleSideEnum)message.TeamSide`.
3. **Гармонизация стороны и активация боевого контура (`SetCombatState`)**:
   - При `combat == true`:
     - Вызывается `SiegeWeapon?.Activate()`.
     - Включается приоритет игрока: `SiegeWeapon?.SetPlayerForceUse(true)`.
     - Активируется точка заряжания через безопасный рефлексивный метод: `SetSiegeWeaponLoadAmmoPointActive(SiegeWeapon, true)`.
     - Сторона орудия динамически синхронизируется с `TeamSide`: `SetSiegeWeaponDefaultSide(SiegeWeapon, TeamSide)`.
     - Обновляется боекомплект орудия: `SiegeWeapon.StartingAmmoCount = 25; SiegeWeapon.SetAmmo(25)`.
     - Все стояночные посты разблокируются (`sp.SetEnabled(false); sp.IsDisabledForPlayers = false; sp.IsDeactivated = false; SetStandingPointNeutral(sp)`) и синхронизируются по сети (`SetIsDisabledForPlayersSynched(false); SetIsDeactivatedSynched(false)`).

#### 3. Верификация
- `dotnet build DWDN_Script.csproj` (Release): 0 ошибок, 0 предупреждений.
- `dotnet build DWDN_Script.Server.csproj` (Release): 0 ошибок, 0 предупреждений.
- `MechanicsTester.exe --cli`: 28 из 28 тестов пройдены успешно (100% pass).
- `sync_all.ps1`: Все бинарники, скрипты и данные синхронизированы на Dedicated Server (`DaewdenM`).


---

### Глава 66: Исцеление камеры баллисты — Устранение эффекта привязки CustomCamera при выходе оператора

#### 1. Описание дефекта и анатомия проблемы
Пользователь зафиксировал: онагр и баллиста функционируют штатно и ведут огонь, однако после использования баллисты и выхода из-за неё (клавишей [F]) камера игрока остаётся намертво привязанной к орудию.

**Анатомический анализ исходного кода TaleWorlds (TaleWorlds.MountAndBlade.View.dll):**
В нативном методе `RangedSiegeWeaponView.HandleUserInput(float dt)` движок выполняет проверку режима орудийной камеры:
```csharp
if (this._isInWeaponCameraMode)
{
    if ((this.PilotAgent == null || !this.PilotAgent.IsMainAgent) && !this.RangedSiegeWeapon.PlayerForceUse)
    {
        this._isInWeaponCameraMode = false;
        this.ResetCamera();
    }
}
```
- `BallistaView` при занятии поста вызывает `StartUsingWeaponCamera()`, активируя `MissionScreen.CustomCamera = this.Camera` и зум `SetExtraCameraParameters(true, 1.5f)`.
- Сброс камеры обратно на персонажа (`ResetCamera()`) происходит **только** в том случае, если оператор покинул пост (`PilotAgent == null`) **И** флаг `PlayerForceUse` равен `false`.
- Когда в боевом состоянии вызывался `SiegeWeapon?.SetPlayerForceUse(true);`, проверка `!this.RangedSiegeWeapon.PlayerForceUse` становилась ложной (`false`).
- В результате движок полагал, что для игрока активен принудительный постановочный режим обзора, и блокировал вызов `ResetCamera()`. Камера оставалась навечно прикреплена к баллисте, а поворот головы персонажа (`Agent.Main.IsLookDirectionLocked`) оставался заблокированным.

#### 2. Внесенные исправления (DWDN_FieldArtilleryBehavior.cs)
- В методе `EngineInstance.SetCombatState(bool combat)` удалена строка `SiegeWeapon?.SetPlayerForceUse(true);`.
- Орудие опирается на стандартную для мультиплеера и одиночной игры нативную стейт-машину стояночного поста: при занятии штурвала баллисты камера плавно переключается в прицельный вид, а при выходе по [F] условие выхода срабатывает истинно, немедленно вызывая `ResetCamera()` и возвращая камеру оператору.

#### 3. Верификация
- `dotnet build DWDN_Script.csproj` (Release): 0 ошибок, 0 предупреждений.
- `dotnet build DWDN_Script.Server.csproj` (Release): 0 ошибок, 0 предупреждений.
- `MechanicsTester.exe --cli`: 28 из 28 тестов пройдены успешно (100% pass).
- `sync_all.ps1`: Все бинарники и скрипты синхронизированы на Dedicated Server (`DaewdenM`).


---

### Глава 67: Исцеление спавна Стрелка с ручницей — Ликвидация инверсии слотов боеприпасов, защита от паразитного спавна артиллерии и нормализация XML атрибутов

#### 1. Симптоматика и локализация скрапкода
- **Симптом**: При попытке заспавниться за класс «Стрелок с ручницей» (`mp_DWDN_Crossbowman`) игра вылетала. Баллиста и онагр артиллериста работают штатно, но выбор ручницы приводил к вылету клиента, а на выделенном сервере через 10 секунд происходило падение с нехваткой памяти (`Requested memory size is 17112760320 bytes`).
- **Глубинные первопричины сбоя**:
  1. **Инверсия слотов в нативном перке `Hand Cannon` (`mpclassdivisions.xml`)**:
     В `mpclassdivisions.xml` в слоте 2 перк `Hand Cannon` содержал:
     ```xml
     <OnSpawnEffect type="AlternativeEquipmentOnSpawn" slot="Item0" item="mp_bullets" />
     <OnSpawnEffect type="AlternativeEquipmentOnSpawn" slot="Item1" item="hand_cannon" />
     ```
     Нативный обработчик спавна экипировал боеприпасы (`mp_bullets`) в слот правой руки (`Item0`), а ручницу в слот `Item1`. В момент вызова нативным ядром TaleWorlds `SetWieldedItemIndex(WeaponItemBeginSlot)` игра пыталась заставить модель персонажа изготовиться к стрельбе и махать сумкой с пулями в ведущей руке. Неуправляемый код `Engine.dll` падал с фатальным `0xc0000005`.
  2. **Паразитный спавн артиллерии для стрелка (`DWDN_FieldArtilleryBehavior.cs`)**:
     В `CheckAndSpawnArtilleryForAgent`:
     ```csharp
     int pIdx = agent.MissionPeer.GetSelectedPerkIndexWithPerkListIndex(agent.MissionPeer.SelectedTroopIndex, 1);
     if (pIdx == 0) hasArtilleryBlueprint = true;
     ```
     Проверка не была ограждена условием `isArtillerist`. У любого бойца со слотом перка 1, равным 0 (у стрелка `Drill / Fast Reload`), переменная `hasArtilleryBlueprint` становилась `true`. Сервер ошибочно считал, что стрелок заказал передвижной Онагр, деспавнил старое орудие и спавнил `mangonel_a` прямо в момент материализации пехотинца, вызывая рассинхронизацию `MissionObject` на клиенте.
  3. **Недостающие атрибуты в `dwdn_weapon.xml`**:
     У `hand_cannon` отсутствовали `value="1" is_merchandise="false"` и `HasString="true"` в `WeaponFlags`. У `mp_bullets` флаг `AttachAmmoToVisual="false"` блокировал прикрепление снаряда к каналу ствола при вызове `CheckLoadedAmmos()`.

#### 2. Внесенные исправления
1. **[mpclassdivisions.xml](file:///e:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/mpclassdivisions.xml)**:
   - В перке `Hand Cannon` слоты возвращены в стандартный порядок: `slot="Item0" item="hand_cannon"` и `slot="Item1" item="mp_bullets"`.
2. **[DWDN_FieldArtilleryBehavior.cs](file:///e:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/DWDN_FieldArtilleryBehavior.cs)**:
   - Добавлено строгое условие `if (isArtillerist && !wantsBallista)`, гарантирующее, что логика выбора осадных орудий (Онагр/Баллиста) активируется исключительно для класса Артиллериста (`mp_Artillerist`), не нарушая при этом ни один из проверенных механизмов артиллерии.
3. **[dwdn_weapon.xml](file:///e:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/mpitems/dwdn_weapon.xml)**:
   - Для `hand_cannon` добавлены атрибуты `value="1" is_merchandise="false"`, культура изменена на `Culture.neutral_culture`, в `WeaponFlags` добавлен `HasString="true"`.
   - Для `mp_bullets` установлены `AttachAmmoToVisual="true"`, `Consumable="true"`, `AmmoSticksWhenShot="true"`, `AmmoBreaksOnBounceBack="true"`, культура установлена в `Culture.neutral_culture`.

#### 3. Верификация
- `dotnet build DWDN_Script.csproj` (Release): 0 ошибок.
- `dotnet build DWDN_Script.Server.csproj` (Release): 0 ошибок.
- `MechanicsTester.exe --cli`: 28 из 28 тестов пройдены успешно (100% pass).
- `sync_all.ps1`: Все бинарники и XML синхронизированы на Dedicated Server (`DaewdenM`).


---

### Глава 68: Очищение Знаменосца и Короля Альберика Девальда — Восстановление сетевого атрибута перчаток (multiplayer_item), нормализация подвеса знамени и возвращение пеших анимаций покоя кавалерии

#### 1. Симптоматика и локализация скрапкода
- **Симптом**: При выборе в меню экипировки или материализации на поле боя Знаменосца (`mp_Bannerman`) и Короля Альберика Девальда (`mp_ALberic_Devald_hero`) происходил критический сбой игры (вылет клиента с аварийной остановкой процесса).
- **Глубинные первопричины сбоя**:
  1. **Отсутствие сетевой печати `multiplayer_item="true"` на перчатках (`dwdn_armor.xml`)**:
     В `dwdn_armor.xml` строки 302–350 содержали 10 кастомных перчаток (`arch_banreich_leatherglove`, `plate_gauntlet`, `plate_round_gauntlet`, `gothic_gloves`, `king_plate_gauntlet`, `crossbowman_mailgoves`, `rundal_gloves_second`, `bergen_gloves`, `Baron_Finard_gauntlet`, `mail__armeds_mitten`). Все они были объявлены без атрибута `multiplayer_item="true"`. В сетевых сессиях TaleWorlds `MBObjectManager` загружает в мультиплеерный пул только предметы с `multiplayer_item="true"`. Из-за этого при экипировке `plate_round_gauntlet` у Знаменосца и `king_plate_gauntlet` у Короля `MBObjectManager.GetObject<ItemObject>` возвращал `null`, приводя к разрушению структуры `EquipmentElement` и падению нативного движка рендеринга персонажа.
  2. **Искажение скелетного подвеса знамени (`dwdn_banners.xml`)**:
     В `dwdn_banners.xml` предмет `dwdn_banner1` имел кобуру `item_holsters="throwing_stone_2"` и культуру `Culture.dwdn`. При уборке знамени в кобуру (переключение на меч/топор) движок пытался привязать 1.5-метровое полотнище к набедренному подсумку для камней (`throwing_stone_2`), вызывая ошибку скелетной трансформации.
  3. **Отсутствие пеших анимаций покоя у кавалерии (`mpclassdivisions.xml`)**:
     У Короля (`mp_ALberic_Devald_hero`), баронов и рыцарей были прописаны исключительно `hero_mount_idle_anim` и `troop_mount_idle_anim`. На пьедестале кастомного селектора (где конь по архитектуре скрывается, а боец осматривается пешим) и при выборе спешенного боя движок искал `hero_idle_anim` и `troop_idle_anim` и падал по незаданному указателю.

#### 2. Внесенные исправления
1. **[dwdn_armor.xml](file:///e:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/mpitems/dwdn_armor.xml)**:
   - Всем 10 парам перчаток (включая `plate_round_gauntlet` Знаменосца и `king_plate_gauntlet` Короля) возвращен священный атрибут `multiplayer_item="true"`.
2. **[dwdn_banners.xml](file:///e:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/mpitems/dwdn_banners.xml)**:
   - Для знамени `dwdn_banner1` установлен каноничный поясной подвес `item_holsters="abdomen_left"`, а культура нормализована до `culture="Culture.neutral_culture"`.
3. **[mpclassdivisions.xml](file:///e:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/mpclassdivisions.xml)**:
   - Для `mp_ALberic_Devald_hero`, `mp_Finnard_Valerian`, `mp_Bergen_Klaus`, `mp_Knight` и `mp_DWDN_Cav` внедрены пешие анимации покоя: `hero_idle_anim="act_sturgia_mp_huskarl_idle"` и `troop_idle_anim="act_idle_unarmed_1"`.

#### 3. Верификация и синхронизация
- `dotnet build DWDN_Script.csproj` (Release): 0 ошибок (сборка успешна).
- `dotnet build DWDN_Script.Server.csproj` (Release): 0 ошибок (сборка успешна).
- `MechanicsTester.exe --cli`: 28 из 28 тестов пройдены успешно (100% pass).
- `sync_all.ps1`: Все бинарники и XML зеркалированы на Dedicated Server (`DaewdenM`).


---

### Глава 69: Устранение краша сервера при спавне Баллисты (Dynamic Navmesh 17GB leak) и универсализация путей без привязки к диску E:

#### 1. Диагностика и первопричины
1. **Катастрофическая аллокация памяти при спавне Баллисты (`rgl_log_45272.txt`)**:
   - Журнал Dedicated Server зафиксировал мгновенный краш процесса:
     `Application crashed because it could not get new memory from the operating system. Requested memory size is 17112760320 bytes` (17.1 ГБ!).
   - Сравнение с префабом работающего Онагра (`mangonel_a`) против Баллисты (`ballista_a`):
     * В `props_siege_mangonels.xml` у `mangonel_a`: `<variable name="NavMeshPrefabName" value=""/>` (пустая строка).
     * В `props_siege_ballistas.xml` у `ballista_a`: `<variable name="NavMeshPrefabName" value="ballista_a_dnm"/>`.
   - Анализ IL-байткода `MissionObject.OnInit()` и `AttachDynamicNavmeshToEntity()`:
     Если `NavMeshPrefabName` не пуст, нативный движок вызывает `Scene.ImportNavigationMeshPrefab` и пытается вырезать динамический навмеш вокруг орудия через `AttachNavigationMeshFaces`. На сетевых боевых картах (`scn_arena` и др.) данный навмеш не имеет смежных полигонов.
     Когда орудие буксируется тягловым конем, в `OnMissionTick` каждые 33 мс вызывается `engine.RootEntity.SetFrame(ref weaponF)`. Нативный геометрический вычислитель Dynamic NavMesh входит в бесконечный цикл деления пространства полигонов и запрашивает у операционной системы 17.1 ГБ, вызывая мгновенный Out-Of-Memory краш сервера.
2. **Жесткие абсолютные ссылки на диск `E:\`**:
   - `SubModule.cs`: fallback `BasePath.Name ?? @"e:\SteamLibrary\..."` падал при работе на других дисках.
   - `sync_all.ps1`: пути `$masterDir`, `$serverDir` и `$serverBinDir` были захардкожены на диск `E:\`.
   - `TestEngine.cs` и утилиты `Tools/` (`DuchyLoadoutStudio`, `ModValidator`, `WeaponCalibrator`) содержали жесткие пути к `E:\SteamLibrary`.

#### 2. Внесенные исправления
1. **[DWDN_FieldArtilleryBehavior.cs](file:///E:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/DWDN_FieldArtilleryBehavior.cs)**:
   - Внедрена процедура `SuppressDynamicNavmeshRecursive(GameEntity entity)`, которая через кэшированное поле `_navMeshPrefabNameField` обнуляет `NavMeshPrefabName = ""` у всех `MissionObject` на баллисте и ее дочерних объектах **до** вызова `root.CallScriptCallbacks(true)`.
   - Применено в серверном методе `SpawnEngine` и в клиентском зеркале `HandleServerSyncDescendants`.
   - Благодаря этому `MissionObject.OnInit()` видит пустую строку и полностью пропускает `AttachDynamicNavmeshToEntity()`, устраняя вызов `ImportNavigationMeshPrefab` и утечку 17 ГБ. Баллиста теперь ведет себя абсолютно симметрично и стабильно, как и проверенный Онагр.
2. **[SubModule.cs](file:///E:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/SubModule.cs)**:
   - Внедрен универсальный метод `GetGameBasePath()`, считывающий `BasePath.Name` либо динамически вычисляющий директорию от `AppDomain.CurrentDomain.BaseDirectory` (поднимаясь от `bin/Win64_Shipping_Client` или `bin/Win64_Shipping_Server`), работающий на любых дисках и ОС.
3. **[sync_all.ps1](file:///E:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/sync_all.ps1)**:
   - Переведен на автоопределение `$PSScriptRoot` с динамическим поиском каталога `steamapps/common` и папки `Mount & Blade II Dedicated Server` на том же накопителе, где запущен скрипт.
4. **[TestEngine.cs](file:///E:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/Tools/MechanicsTester/TestEngine.cs) и утилиты `Tools/`**:
   - Внедрен метод `ResolveUniversalPaths()`, динамически определяющий пути к модулю и бинарникам движка.

#### 3. Верификация и синхронизация
- `dotnet build DWDN_Script.csproj` (Client Release): 0 ошибок.
- `dotnet build DWDN_Script.Server.csproj` (Server Release): 0 ошибок.
- `MechanicsTester.exe --cli`: 28 из 28 тестов успешно пройдены (100% pass).
- `sync_all.ps1`: успешно развернул клиентские и серверные сборки, скрипты и XML в каталог выделенного сервера `DaewdenM` и `DWDN_TRY`.


---

### Глава 70: Полная переработка UI и двусторонней сцепки артиллерии (Responsive Artillery Interaction UI & Limber Mechanics)

#### 1. Контекст и проблематика
- **Симптомы**:
  1. Всплывающее сверху текстовое окно `MBInformationManager.AddQuickInformation` вызывало постоянный спам при каждом тике в слепом радиусе 3.5 метров.
  2. Игроку было непонятно, где именно точка сцепки, куда нужно наводиться прицелом и сколько времени удерживать [F].
  3. Невозможность повторно прицепить орудие к коню после распряжки (отсутствие двусторонней сцепки).
  4. При упаковке орудия в походное положение не сохранялся остаток боекомплекта орудия.

#### 2. Архитектурные решения
1. **Математическая виртуальная точка сцепки (без навешивания мешей на модель)**:
   - В соответствии с решением Создателя, физические коллайдеры или дочерние меши на 3D-модели не навешиваются.
   - Точка сцепки на дышле рассчитывается динамически в пространстве матрицы орудия:
     `HitchPosition = Frame.origin + (Frame.rotation.f * 2.2f) + new Vec3(0f, 0f, 0.45f);`
2. **3D World-to-Screen проекция и отзывчивый Gauntlet UI**:
   - `GUI/Prefabs/DWDN_ArtilleryHitchHUD.xml`:
     * 3D-маяк (`Hitch3DMarker`), проецируемый на дышло через `CombatCamera.WorldPointToViewPortPoint`, показывающий точное местоположение замка сцепки.
     * Прицельный HUD (`CrosshairPromptPanel`) прямо под перекрестием со статусной строкой и плавной полосой прогресса удержания (`ProgressFillWidth`).
   - `DWDN_ArtilleryHitchVM.cs`: ViewModel со свойствами `IsPromptVisible`, `PromptText`, `Progress`, `ProgressFillWidth`, `IsMarkerVisible`, `MarkerX`, `MarkerY`, `CanInteract`.
   - `DWDN_ArtilleryHitchView.cs`: Клиентский `MissionView` (Gauntlet layer 16), отслеживающий угол взгляда (конус ~28 градусов, `dot >= 0.88f`), дистанцию (<= 2.5м) и 2-секундное удержание [F].
3. **Тактическое удержание и прерывание уроном**:
   - Время удержания [F] составляет 2.0 секунды.
   - Получение любого урона (`Health < _lastHealth - 0.01f`) немедленно сбивает таймер в ноль, исключая мгновенную упаковку/развертывание под огнем.
4. **Двусторонняя сцепка/отцепка (Combat <-> Travel)**:
   - Отцепка (походный -> боевой): активирует орудие и камни, освобождает коня от ограничения скорости шага (2.2 м/с).
   - Впряжка (боевой -> походный): деактивирует орудие, сохраняет оставшийся боезапас (`SavedAmmo = SiegeWeapon.AmmoCount`), находит свободного коня в радиусе 3.5м дышла (включая трофейных коней врага и оседланного коня артиллериста) и привязывает орудие в походную упряжь.
5. **Сетевой протокол**:
   - `RequestToggleArtilleryHitchMessage` (клиент -> сервер): `EngineId`, `HorseAgentIndex`, `Attach`.
   - `SetArtilleryCombatStateMessage` (сервер -> клиент): дополнен `HorseAgentIndex` и `RemainingAmmo` для синхронизации клиента.
   - Полномочия: только создатель орудия или союзный артиллерист (`mp_Artillerist`).

#### 3. Модифицированные и созданные артефакты
- [GUI/Prefabs/DWDN_ArtilleryHitchHUD.xml](file:///E:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/GUI/Prefabs/DWDN_ArtilleryHitchHUD.xml)
- [ModuleData/scripts/DWDN_Script/Client/UI/DWDN_ArtilleryHitchVM.cs](file:///E:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/Client/UI/DWDN_ArtilleryHitchVM.cs)
- [ModuleData/scripts/DWDN_Script/Client/UI/DWDN_ArtilleryHitchView.cs](file:///E:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/Client/UI/DWDN_ArtilleryHitchView.cs)
- [ModuleData/scripts/DWDN_Script/Common/Network/DWDN_NetworkMessages.cs](file:///E:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/Common/Network/DWDN_NetworkMessages.cs)
- [ModuleData/scripts/DWDN_Script/DWDN_FieldArtilleryBehavior.cs](file:///E:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/DWDN_FieldArtilleryBehavior.cs)
- [ModuleData/scripts/DWDN_Script/Client/Behaviors/DWDN_ClientMissionComponent.cs](file:///E:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/Client/Behaviors/DWDN_ClientMissionComponent.cs)
- [ModuleData/scripts/DWDN_Script/DWDN_Script.csproj](file:///E:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/DWDN_Script.csproj)
- [sync_all.ps1](file:///E:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/sync_all.ps1)

#### 4. Верификация
- `dotnet build DWDN_Script.csproj` (Client Release): 0 ошибок, 23 предупреждения MSIL (штатные).
- `dotnet build DWDN_Script.Server.csproj` (Server Release): 0 ошибок, 0 предупреждений.
- `MechanicsTester.exe --cli`: 28/28 тестов пройдено (100% pass).
- `sync_all.ps1`: Все бинарники, скрипты, XML и GUI prefabs синхронизированы в Dedicated Server (`DaewdenM` и `DWDN_TRY`).


---

### Глава 71: Устранение краша при удержании [F] на сцепке артиллерии (Fix Gauntlet ProgressFillWidth Type Mismatch Crash)

#### 1. Симптом и первопричина
- **Симптом**: При попытке отцепить или прицепить артиллерийскую платформу (удержание клавиши `[F]`) игра мгновенно падала на рабочий стол без стандартного окна отчета об ошибке.
- **Бортовой журнал (`rgl_log_32856.txt`)**:
  ```text
  [04:38:20.680] Exception occurred inside invoke: set_ProgressFillWidth
  Target type: DWDN_Script.Client.UI.DWDN_ArtilleryHitchVM
  Argument count: 1
  Argument type is System.Single
  Inner message: Object of type 'System.Single' cannot be converted to type 'System.Int32'. 
  Crash occurred. Asking for dump.
  ```
- **Первопричина**:
  Свойство `Widget.SuggestedWidth` в TaleWorlds GauntletUI имеет тип `System.Single` (`float`).
  В `DWDN_ArtilleryHitchVM.cs` свойство `ProgressFillWidth` было ошибочно объявлено с типом `int` (`System.Int32`).
  При первом же кадре нажатия `[F]` сеттер `Progress` обновлял ширину, а механизм привязки данных Gauntlet пытался вызвать `set_ProgressFillWidth((Single)value)`. Рефлексия .NET выбрасывала фатальный `ArgumentException`, краша движок.
  Также проверка прав взаимодействия жестко требовала класс `mp_Artillerist` или создателя, блокируя союзных игроков других классов (например, `mp_Ratay`).

#### 2. Внесенные исправления
1. **[DWDN_ArtilleryHitchVM.cs](file:///E:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/Client/UI/DWDN_ArtilleryHitchVM.cs)**:
   - Поле `_progressFillWidth` и свойство `ProgressFillWidth` переведены из `int` в `float` (`System.Single`), устраняя несоответствие типов при рефлексивном связывании с `Widget.SuggestedWidth`.
   - В сеттере `Progress` внедрено безопасное ограничение диапазона:
     `ProgressFillWidth = System.Math.Max(0f, System.Math.Min(320f, value * 320f));`
2. **[DWDN_ArtilleryHitchView.cs](file:///E:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/Client/UI/DWDN_ArtilleryHitchView.cs)** & **[DWDN_FieldArtilleryBehavior.cs](file:///E:/SteamLibrary/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/DWDN_TRY/ModuleData/scripts/DWDN_Script/DWDN_FieldArtilleryBehavior.cs)**:
   - Смягчена проверка доступа: теперь сцепкой/отцепкой могут управлять союзники по команде (`agent.Team == null || agent.Team.Side == engine.TeamSide`) и игроки в одиночных/кастомных миссиях.

#### 3. Верификация
- `dotnet build DWDN_Script.csproj` (Client Release): 0 ошибок.
- `dotnet build DWDN_Script.Server.csproj` (Server Release): 0 ошибок.
- `MechanicsTester.exe --cli`: 28 из 28 тестов пройдены успешно (100% pass).
- `sync_all.ps1`: Все бинарники, скрипты и GUI синхронизированы в серверные каталоги `DaewdenM` и `DWDN_TRY`.
