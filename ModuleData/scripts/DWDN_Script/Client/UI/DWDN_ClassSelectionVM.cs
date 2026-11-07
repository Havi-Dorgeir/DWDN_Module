using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using DWDN_Script.Common.Duchies;

namespace DWDN_Script.Client.UI
{
    /// <summary>
    /// Главный ViewModel кастомного экрана выбора классов и снаряжения,
    /// полностью заменяющий стандартный экран MissionGauntletClassLoadout.
    /// </summary>
    public class DWDN_ClassSelectionVM : ViewModel
    {
        private readonly Action<DWDN_LoadoutSelection, bool> _onSelectionChanged;
        private readonly Action _onSpawnRequested;

        private DWDN_DuchyItemVM _currentDuchy;
        private DWDN_ClassItemVM _currentClass;
        private DWDN_LoadoutSelection _currentSelection;
        private bool _isInitializing;
        private bool _isActive;
        private string _spawnStatusText = "Нажмите [F], чтобы вступить в бой";

        [DataSourceProperty]
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChangedWithValue(value, nameof(IsActive));
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<DWDN_DuchyItemVM> Duchies { get; } = new MBBindingList<DWDN_DuchyItemVM>();

        [DataSourceProperty]
        public MBBindingList<DWDN_ClassItemVM> Classes { get; } = new MBBindingList<DWDN_ClassItemVM>();

        [DataSourceProperty]
        public MBBindingList<DWDN_EquipmentSlotVM> EquipmentSlots { get; } = new MBBindingList<DWDN_EquipmentSlotVM>();

        [DataSourceProperty]
        public string SelectedDuchyName => _currentDuchy?.DisplayName ?? "Баннрайх";

        [DataSourceProperty]
        public string SelectedClassName => _currentClass?.DisplayName ?? "";

        [DataSourceProperty]
        public string SelectedClassDescription => _currentClass?.DisplayDescription ?? "";

        private int _selectedClassHealth = 100;
        private int _selectedClassArmor = 35;
        private int _selectedClassSpeed = 80;

        [DataSourceProperty]
        public string HitPointsText => _selectedClassHealth.ToString();

        [DataSourceProperty]
        public string ArmorText => _selectedClassArmor.ToString();

        [DataSourceProperty]
        public string SpeedText => _selectedClassSpeed.ToString();

        [DataSourceProperty]
        public int SelectedClassHealth => _selectedClassHealth;

        [DataSourceProperty]
        public int SelectedClassArmor => _selectedClassArmor;

        [DataSourceProperty]
        public int SelectedClassSpeed => _selectedClassSpeed;

        private void UpdateStats()
        {
            try
            {
                // 1. Здоровье (HP) из персонажа класса
                int hp = 100;
                BasicCharacterObject troop = null;
                if (!string.IsNullOrEmpty(_currentClass?.TroopId))
                {
                    troop = MBObjectManager.Instance?.GetObject<BasicCharacterObject>(_currentClass.TroopId);
                }
                if (troop != null)
                {
                    hp = troop.MaxHitPoints();
                    if (hp <= 0) hp = 100;
                }
                _selectedClassHealth = hp;

                // 2. Броня: точный суммарный расчет из надетых предметов текущего выбора
                int totalArmor = 0;
                void AddArmorFromItem(string itemId)
                {
                    if (string.IsNullOrEmpty(itemId)) return;
                    ItemObject item = MBObjectManager.Instance?.GetObject<ItemObject>(itemId);
                    if (item?.ArmorComponent != null)
                    {
                        totalArmor += item.ArmorComponent.HeadArmor;
                        totalArmor += item.ArmorComponent.BodyArmor;
                        totalArmor += item.ArmorComponent.ArmArmor;
                        totalArmor += item.ArmorComponent.LegArmor;
                    }
                }

                if (_currentSelection != null)
                {
                    AddArmorFromItem(_currentSelection.HeadId);
                    AddArmorFromItem(_currentSelection.CapeId);
                    AddArmorFromItem(_currentSelection.BodyId);
                    AddArmorFromItem(_currentSelection.GlovesId);
                    AddArmorFromItem(_currentSelection.LegsId);
                }
                _selectedClassArmor = totalArmor;

                // 3. Скорость (навык атлетики персонажа)
                int spd = 80;
                if (troop != null)
                {
                    spd = troop.GetSkillValue(DefaultSkills.Athletics);
                    if (spd <= 0) spd = 80;
                }
                _selectedClassSpeed = spd;

                OnPropertyChanged(nameof(SelectedClassHealth));
                OnPropertyChanged(nameof(SelectedClassArmor));
                OnPropertyChanged(nameof(SelectedClassSpeed));
                OnPropertyChanged(nameof(HitPointsText));
                OnPropertyChanged(nameof(ArmorText));
                OnPropertyChanged(nameof(SpeedText));
            }
            catch (Exception ex)
            {
                Debug.Print($"[DWDN] UpdateStats exception: {ex}");
            }
        }

        [DataSourceProperty]
        public string SpawnStatusText
        {
            get => _spawnStatusText;
            set
            {
                if (_spawnStatusText != value)
                {
                    _spawnStatusText = value;
                    OnPropertyChangedWithValue(value, nameof(SpawnStatusText));
                }
            }
        }

        public DWDN_LoadoutSelection CurrentSelection => _currentSelection;

        public void ExecuteSpawn()
        {
            _onSpawnRequested?.Invoke();
        }

        public DWDN_ClassSelectionVM(
            DWDN_LoadoutSelection initialSelection,
            Action<DWDN_LoadoutSelection, bool> onSelectionChanged,
            Action onSpawnRequested = null)
        {
            _onSelectionChanged = onSelectionChanged;
            _onSpawnRequested = onSpawnRequested;
            _currentSelection = initialSelection?.Clone() ?? new DWDN_LoadoutSelection();

            _isInitializing = true;
            InitializeDuchies();
            InitializeSlots();
            SelectInitialState();
            _isInitializing = false;

            // Синхронизируем начальный выбор с сервером и 3D-превью на пьедестале
            _onSelectionChanged?.Invoke(_currentSelection, true);
        }

        private void InitializeDuchies()
        {
            Duchies.Clear();
            foreach (var duchy in DWDN_DuchyManager.Duchies)
            {
                var item = new DWDN_DuchyItemVM(duchy, OnDuchySelected);
                Duchies.Add(item);
            }
        }

        private void InitializeSlots()
        {
            EquipmentSlots.Clear();
            EquipmentSlots.Add(new DWDN_EquipmentSlotVM(DWDN_SlotCategory.Head, "Шлем", EquipmentIndex.Head, OnSlotItemSelected));
            EquipmentSlots.Add(new DWDN_EquipmentSlotVM(DWDN_SlotCategory.Cape, "Наплечники", EquipmentIndex.Cape, OnSlotItemSelected));
            EquipmentSlots.Add(new DWDN_EquipmentSlotVM(DWDN_SlotCategory.Body, "Доспех", EquipmentIndex.Body, OnSlotItemSelected));
            EquipmentSlots.Add(new DWDN_EquipmentSlotVM(DWDN_SlotCategory.Gloves, "Перчатки", EquipmentIndex.Gloves, OnSlotItemSelected));
            EquipmentSlots.Add(new DWDN_EquipmentSlotVM(DWDN_SlotCategory.Legs, "Обувь", EquipmentIndex.Leg, OnSlotItemSelected));

            EquipmentSlots.Add(new DWDN_EquipmentSlotVM(DWDN_SlotCategory.Weapon0, "Оружие I", EquipmentIndex.WeaponItemBeginSlot + 0, OnSlotItemSelected));
            EquipmentSlots.Add(new DWDN_EquipmentSlotVM(DWDN_SlotCategory.Weapon1, "Оружие II", EquipmentIndex.WeaponItemBeginSlot + 1, OnSlotItemSelected));
            EquipmentSlots.Add(new DWDN_EquipmentSlotVM(DWDN_SlotCategory.Weapon2, "Оружие III", EquipmentIndex.WeaponItemBeginSlot + 2, OnSlotItemSelected));
            EquipmentSlots.Add(new DWDN_EquipmentSlotVM(DWDN_SlotCategory.Weapon3, "Оружие IV", EquipmentIndex.WeaponItemBeginSlot + 3, OnSlotItemSelected));

            EquipmentSlots.Add(new DWDN_EquipmentSlotVM(DWDN_SlotCategory.Horse, "Скакун", EquipmentIndex.Horse, OnSlotItemSelected));
        }

        private void SelectInitialState()
        {
            DWDN_DuchyItemVM targetDuchy = null;
            if (!string.IsNullOrEmpty(_currentSelection.DuchyId))
            {
                foreach (var d in Duchies)
                {
                    if (d.Id.Equals(_currentSelection.DuchyId, StringComparison.OrdinalIgnoreCase))
                    {
                        targetDuchy = d;
                        break;
                    }
                }
            }

            if (targetDuchy == null && Duchies.Count > 0)
            {
                targetDuchy = Duchies[0];
            }

            if (targetDuchy != null)
            {
                OnDuchySelected(targetDuchy);
            }
        }

        private void OnDuchySelected(DWDN_DuchyItemVM duchy)
        {
            if (duchy == null || !duchy.IsAvailable) return;

            foreach (var d in Duchies)
            {
                d.IsSelected = (d == duchy);
            }
            _currentDuchy = duchy;
            _currentSelection.DuchyId = duchy.Id;
            OnPropertyChanged(nameof(SelectedDuchyName));

            foreach (var c in Classes)
            {
                c.OnFinalize();
            }
            Classes.Clear();

            foreach (var cls in duchy.Duchy.Classes)
            {
                var item = new DWDN_ClassItemVM(cls, OnClassSelected);
                Classes.Add(item);
            }

            DWDN_ClassItemVM targetClass = null;
            if (!string.IsNullOrEmpty(_currentSelection.ClassId))
            {
                foreach (var c in Classes)
                {
                    if (c.Id.Equals(_currentSelection.ClassId, StringComparison.OrdinalIgnoreCase))
                    {
                        targetClass = c;
                        break;
                    }
                }
            }

            if (targetClass == null && Classes.Count > 0)
            {
                targetClass = Classes[0];
            }

            if (targetClass != null)
            {
                OnClassSelected(targetClass);
            }
        }

        private void OnClassSelected(DWDN_ClassItemVM classItem)
        {
            if (classItem == null) return;

            bool classChanged = !string.Equals(_currentSelection.ClassId, classItem.Id, StringComparison.OrdinalIgnoreCase);

            foreach (var c in Classes)
            {
                c.IsSelected = (c == classItem);
            }
            _currentClass = classItem;

            // Если переключились на другой класс или набор еще пуст — берем дефолтный набор этого класса
            if (classChanged || string.IsNullOrEmpty(_currentSelection.ClassId))
            {
                _currentSelection = classItem.ClassDefinition.CreateDefaultLoadout(_currentDuchy?.Id ?? "banreich");
            }
            else
            {
                _currentSelection.ClassId = classItem.Id;
            }

            OnPropertyChanged(nameof(SelectedClassName));
            OnPropertyChanged(nameof(SelectedClassDescription));

            RefreshAllSlotRows();
            UpdateStats();

            if (!_isInitializing)
            {
                _onSelectionChanged?.Invoke(_currentSelection, true);
            }
        }

        private void RefreshAllSlotRows()
        {
            foreach (var slot in EquipmentSlots)
            {
                List<string> pool = GetItemPoolForSlot(slot.SlotCategory);
                string currentId = GetSelectedIdForSlot(slot.SlotCategory);
                slot.PopulateOptions(pool, currentId);

                // Если текущий ID слота не был в пуле или был пуст, фиксируем первый доступный предмет
                if (slot.Options.Count > 0)
                {
                    bool found = false;
                    foreach (var opt in slot.Options)
                    {
                        if (opt.IsSelected)
                        {
                            SetSelectedIdForSlot(slot.SlotCategory, opt.ItemId);
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        SetSelectedIdForSlot(slot.SlotCategory, slot.Options[0].ItemId);
                    }
                }
            }
        }

        private void OnSlotItemSelected(DWDN_EquipmentSlotVM slot, DWDN_ItemOptionVM option)
        {
            if (slot == null || option == null) return;

            SetSelectedIdForSlot(slot.SlotCategory, option.ItemId);
            UpdateStats();

            Debug.Print($"[DWDN UI] Слот {slot.SlotCategory} изменен на: '{option.ItemId}'");

            if (!_isInitializing)
            {
                _onSelectionChanged?.Invoke(_currentSelection, false);
            }
        }

        private List<string> GetItemPoolForSlot(DWDN_SlotCategory slot)
        {
            var def = _currentClass?.ClassDefinition;
            if (def == null) return new List<string>();

            switch (slot)
            {
                case DWDN_SlotCategory.Head: return def.AllowedHelmets;
                case DWDN_SlotCategory.Cape: return def.AllowedCapes;
                case DWDN_SlotCategory.Body: return def.AllowedBodyArmors;
                case DWDN_SlotCategory.Gloves: return def.AllowedGloves;
                case DWDN_SlotCategory.Legs: return def.AllowedLegs;
                case DWDN_SlotCategory.Weapon0: return def.AllowedWeaponsSlot0;
                case DWDN_SlotCategory.Weapon1: return def.AllowedWeaponsSlot1;
                case DWDN_SlotCategory.Weapon2: return def.AllowedWeaponsSlot2;
                case DWDN_SlotCategory.Weapon3: return def.AllowedWeaponsSlot3;
                case DWDN_SlotCategory.Horse: return def.AllowedMounts;
                default: return new List<string>();
            }
        }

        private string GetSelectedIdForSlot(DWDN_SlotCategory slot)
        {
            switch (slot)
            {
                case DWDN_SlotCategory.Head: return _currentSelection.HeadId;
                case DWDN_SlotCategory.Cape: return _currentSelection.CapeId;
                case DWDN_SlotCategory.Body: return _currentSelection.BodyId;
                case DWDN_SlotCategory.Gloves: return _currentSelection.GlovesId;
                case DWDN_SlotCategory.Legs: return _currentSelection.LegsId;
                case DWDN_SlotCategory.Weapon0: return _currentSelection.Weapon0Id;
                case DWDN_SlotCategory.Weapon1: return _currentSelection.Weapon1Id;
                case DWDN_SlotCategory.Weapon2: return _currentSelection.Weapon2Id;
                case DWDN_SlotCategory.Weapon3: return _currentSelection.Weapon3Id;
                case DWDN_SlotCategory.Horse: return _currentSelection.HorseId;
                default: return "";
            }
        }

        private void SetSelectedIdForSlot(DWDN_SlotCategory slot, string itemId)
        {
            switch (slot)
            {
                case DWDN_SlotCategory.Head: _currentSelection.HeadId = itemId; break;
                case DWDN_SlotCategory.Cape: _currentSelection.CapeId = itemId; break;
                case DWDN_SlotCategory.Body: _currentSelection.BodyId = itemId; break;
                case DWDN_SlotCategory.Gloves: _currentSelection.GlovesId = itemId; break;
                case DWDN_SlotCategory.Legs: _currentSelection.LegsId = itemId; break;
                case DWDN_SlotCategory.Weapon0: _currentSelection.Weapon0Id = itemId; break;
                case DWDN_SlotCategory.Weapon1: _currentSelection.Weapon1Id = itemId; break;
                case DWDN_SlotCategory.Weapon2: _currentSelection.Weapon2Id = itemId; break;
                case DWDN_SlotCategory.Weapon3: _currentSelection.Weapon3Id = itemId; break;
                case DWDN_SlotCategory.Horse: _currentSelection.HorseId = itemId; break;
            }
        }

        public override void OnFinalize()
        {
            foreach (var c in Classes)
            {
                c.OnFinalize();
            }
            Classes.Clear();

            foreach (var s in EquipmentSlots)
            {
                s.OnFinalize();
            }
            EquipmentSlots.Clear();

            base.OnFinalize();
        }
    }
}
