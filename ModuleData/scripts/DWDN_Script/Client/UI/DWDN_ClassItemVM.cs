using System;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using DWDN_Script.Common.Duchies;

namespace DWDN_Script.Client.UI
{
    /// <summary>
    /// ViewModel карточки класса (Стрелок с ручницей, Рыцарь, Пикинер)
    /// </summary>
    public class DWDN_ClassItemVM : ViewModel
    {
        private readonly DWDN_ClassDefinition _classDef;
        private readonly Action<DWDN_ClassItemVM> _onSelected;
        private bool _isSelected;
        private ImageIdentifierVM _iconImage;

        public DWDN_ClassDefinition ClassDefinition => _classDef;
        public string Id => _classDef.Id;
        public string Name => _classDef.Name;
        public string Description => _classDef.Description;
        public string TroopId => _classDef.TroopId;

        [DataSourceProperty]
        public string DisplayName => _classDef.Name;

        [DataSourceProperty]
        public string DisplayDescription => _classDef.Description;

        [DataSourceProperty]
        public ImageIdentifierVM IconImage
        {
            get => _iconImage;
            set
            {
                if (_iconImage != value)
                {
                    _iconImage = value;
                    OnPropertyChangedWithValue(value, nameof(IconImage));
                }
            }
        }

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChangedWithValue(value, nameof(IsSelected));
                }
            }
        }

        public DWDN_ClassItemVM(DWDN_ClassDefinition classDef, Action<DWDN_ClassItemVM> onSelected)
        {
            _classDef = classDef;
            _onSelected = onSelected;

            string primaryWeaponId = (_classDef.AllowedWeaponsSlot0 != null && _classDef.AllowedWeaponsSlot0.Count > 0)
                ? _classDef.AllowedWeaponsSlot0[0]
                : "";

            ItemObject item = !string.IsNullOrEmpty(primaryWeaponId)
                ? MBObjectManager.Instance?.GetObject<ItemObject>(primaryWeaponId)
                : null;

            _iconImage = new ItemImageIdentifierVM(item, "");
        }

        public void ExecuteSelect()
        {
            _onSelected?.Invoke(this);
        }

        public override void OnFinalize()
        {
            _iconImage?.OnFinalize();
            base.OnFinalize();
        }
    }
}
