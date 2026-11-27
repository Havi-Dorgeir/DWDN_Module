using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace DWDN_Script.Client.UI
{
    public enum DWDN_SlotCategory
    {
        Head,
        Cape,
        Body,
        Gloves,
        Legs,
        Weapon0,
        Weapon1,
        Weapon2,
        Weapon3,
        Horse
    }

    /// <summary>
    /// ViewModel строки слота экипировки (Шлем, Доспех, Оружие и т.д.),
    /// содержащей горизонтальный ряд визуальных ячеек предметов (Options).
    /// </summary>
    public class DWDN_EquipmentSlotVM : ViewModel
    {
        private readonly Action<DWDN_EquipmentSlotVM, DWDN_ItemOptionVM> _onItemSelected;
        private string _slotName;
        private string _categoryName = "";
        private bool _showCategoryHeader;
        private string _selectedItemName;
        private bool _hasOptions;
        private readonly string _initialSlotName;
        private readonly bool _useExplicitLabel;

        public DWDN_SlotCategory SlotCategory { get; }
        public EquipmentIndex EquipmentIndex { get; }

        [DataSourceProperty]
        public string CategoryName
        {
            get => _categoryName;
            set
            {
                if (_categoryName != value)
                {
                    _categoryName = value;
                    OnPropertyChangedWithValue(value, nameof(CategoryName));
                }
            }
        }

        [DataSourceProperty]
        public bool ShowCategoryHeader
        {
            get => _showCategoryHeader;
            set
            {
                if (_showCategoryHeader != value)
                {
                    _showCategoryHeader = value;
                    OnPropertyChangedWithValue(value, nameof(ShowCategoryHeader));
                }
            }
        }

        [DataSourceProperty]
        public string SlotName
        {
            get => _slotName;
            set
            {
                if (_slotName != value)
                {
                    _slotName = value;
                    OnPropertyChangedWithValue(value, nameof(SlotName));
                }
            }
        }

        [DataSourceProperty]
        public string SelectedItemName
        {
            get => _selectedItemName;
            set
            {
                if (_selectedItemName != value)
                {
                    _selectedItemName = value;
                    OnPropertyChangedWithValue(value, nameof(SelectedItemName));
                }
            }
        }

        [DataSourceProperty]
        public bool HasOptions
        {
            get => _hasOptions;
            set
            {
                if (_hasOptions != value)
                {
                    _hasOptions = value;
                    OnPropertyChangedWithValue(value, nameof(HasOptions));
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<DWDN_ItemOptionVM> Options { get; } = new MBBindingList<DWDN_ItemOptionVM>();

        public DWDN_EquipmentSlotVM(
            DWDN_SlotCategory category,
            string slotName,
            EquipmentIndex index,
            Action<DWDN_EquipmentSlotVM, DWDN_ItemOptionVM> onItemSelected,
            string categoryName = "",
            bool showCategoryHeader = false,
            bool useExplicitLabel = false)
        {
            SlotCategory = category;
            _slotName = slotName;
            _initialSlotName = slotName;
            _categoryName = categoryName ?? "";
            _showCategoryHeader = showCategoryHeader;
            _useExplicitLabel = useExplicitLabel;
            EquipmentIndex = index;
            _onItemSelected = onItemSelected;
            _selectedItemName = "Нет";
        }

        private string DetermineWeaponSlotName(List<string> itemIds, string fallback)
        {
            if (itemIds == null || itemIds.Count == 0) return fallback;

            bool hasShield = false;
            bool hasSpear = false;
            bool hasPike = false;
            bool hasAmmo = false;
            bool hasRanged = false;
            bool hasFirearm = false;

            foreach (var id in itemIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                string lower = id.ToLowerInvariant();
                if (lower.Contains("shield")) hasShield = true;
                if (lower.Contains("bullet") || lower.Contains("bolt") || lower.Contains("arrow")) hasAmmo = true;
                if (lower.Contains("cannon") || lower.Contains("bombard") || lower.Contains("gun")) hasFirearm = true;
                if (lower.Contains("pike")) hasPike = true;
                if (lower.Contains("spear") || lower.Contains("lance")) hasSpear = true;
                if (lower.Contains("bow") || lower.Contains("crossbow")) hasRanged = true;

                ItemObject item = MBObjectManager.Instance?.GetObject<ItemObject>(id);
                if (item != null)
                {
                    if (item.ItemType == ItemObject.ItemTypeEnum.Shield) hasShield = true;
                    if (item.ItemType == ItemObject.ItemTypeEnum.Arrows || item.ItemType == ItemObject.ItemTypeEnum.Bolts || item.ItemType == ItemObject.ItemTypeEnum.Bullets) hasAmmo = true;
                    if (item.ItemType == ItemObject.ItemTypeEnum.Bow || item.ItemType == ItemObject.ItemTypeEnum.Crossbow) hasRanged = true;
                    if (item.ItemType == ItemObject.ItemTypeEnum.Polearm) hasSpear = true;
                }
            }

            if (hasFirearm) return "Огнестрел";
            if (hasShield) return "Щит";
            if (hasPike) return "Пика";
            if (hasSpear) return "Копьё";
            if (hasAmmo) return "Боеприпасы";
            if (hasRanged) return "Стрелковое оружие";
            return "Оружие";
        }

        public void PopulateOptions(List<string> allowedItemIds, string currentSelectedId)
        {
            foreach (var opt in Options)
            {
                opt.OnFinalize();
            }
            Options.Clear();

            if (!_useExplicitLabel && SlotCategory >= DWDN_SlotCategory.Weapon0 && SlotCategory <= DWDN_SlotCategory.Weapon3)
            {
                SlotName = DetermineWeaponSlotName(allowedItemIds, _initialSlotName);
            }
            else
            {
                SlotName = _initialSlotName;
            }

            if (allowedItemIds == null || allowedItemIds.Count == 0 || (allowedItemIds.Count == 1 && string.IsNullOrEmpty(allowedItemIds[0])))
            {
                HasOptions = false;
                SelectedItemName = "Недоступно";
                return;
            }

            DWDN_ItemOptionVM matchedOption = null;

            foreach (var itemId in allowedItemIds)
            {
                string customName = null;
                if (SlotCategory == DWDN_SlotCategory.Horse)
                {
                    if (string.IsNullOrEmpty(itemId))
                        customName = "Без лошади (Пеший)";
                    else if (itemId.Contains("vlandia"))
                        customName = "Боевой конь";
                    else if (itemId.Contains("battania"))
                        customName = "Королевский конь";
                }

                var option = new DWDN_ItemOptionVM(itemId, OnOptionClicked, customName);
                bool isMatch = string.Equals(itemId ?? "", currentSelectedId ?? "", StringComparison.OrdinalIgnoreCase);
                option.IsSelected = isMatch;
                if (isMatch)
                {
                    matchedOption = option;
                }
                Options.Add(option);
            }

            if (matchedOption == null && Options.Count > 0)
            {
                matchedOption = Options[0];
                matchedOption.IsSelected = true;
            }

            SelectedItemName = matchedOption != null ? matchedOption.ItemName : "Нет";
            HasOptions = Options.Count > 0;
        }

        private void OnOptionClicked(DWDN_ItemOptionVM clickedOption)
        {
            if (clickedOption == null) return;

            foreach (var opt in Options)
            {
                opt.IsSelected = (opt == clickedOption);
            }

            SelectedItemName = clickedOption.ItemName;
            _onItemSelected?.Invoke(this, clickedOption);
        }

        public override void OnFinalize()
        {
            foreach (var opt in Options)
            {
                opt.OnFinalize();
            }
            Options.Clear();
            base.OnFinalize();
        }
    }
}
