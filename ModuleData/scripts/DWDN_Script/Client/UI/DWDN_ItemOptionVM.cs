using System;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace DWDN_Script.Client.UI
{
    /// <summary>
    /// ViewModel одной ячейки предмета (64x64 с иконкой предмета) в строке слота экипировки.
    /// </summary>
    public class DWDN_ItemOptionVM : ViewModel
    {
        private readonly Action<DWDN_ItemOptionVM> _onSelected;
        private bool _isSelected;
        private ImageIdentifierVM _itemImage;
        private HintViewModel _hint;

        public string ItemId { get; }
        public ItemObject Item { get; }

        [DataSourceProperty]
        public string ItemName { get; }

        [DataSourceProperty]
        public string ShortLabel { get; }

        [DataSourceProperty]
        public bool IsEmptyOption { get; }

        [DataSourceProperty]
        public ImageIdentifierVM ItemImage
        {
            get => _itemImage;
            set
            {
                if (_itemImage != value)
                {
                    _itemImage = value;
                    OnPropertyChangedWithValue(value, nameof(ItemImage));
                }
            }
        }

        [DataSourceProperty]
        public HintViewModel Hint
        {
            get => _hint;
            set
            {
                if (_hint != value)
                {
                    _hint = value;
                    OnPropertyChangedWithValue(value, nameof(Hint));
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

        public DWDN_ItemOptionVM(string itemId, Action<DWDN_ItemOptionVM> onSelected, string customName = null)
        {
            ItemId = itemId ?? "";
            _onSelected = onSelected;

            if (string.IsNullOrEmpty(ItemId))
            {
                Item = null;
                ItemName = !string.IsNullOrEmpty(customName) ? customName : "Без предмета (Снять)";
                ShortLabel = "НЕТ";
                IsEmptyOption = true;
                _itemImage = new ItemImageIdentifierVM(null, "");
            }
            else
            {
                Item = MBObjectManager.Instance?.GetObject<ItemObject>(ItemId);
                ItemName = !string.IsNullOrEmpty(customName) ? customName : (Item?.Name?.ToString() ?? ItemId);
                ShortLabel = Item == null ? ItemId : "";
                IsEmptyOption = (Item == null);
                _itemImage = new ItemImageIdentifierVM(Item, "");
            }

            _hint = new HintViewModel(new TextObject(ItemName));
        }

        public void ExecuteSelect()
        {
            _onSelected?.Invoke(this);
        }

        public void ExecuteBeginHint()
        {
            _hint?.ExecuteBeginHint();
        }

        public void ExecuteEndHint()
        {
            _hint?.ExecuteEndHint();
        }

        public override void OnFinalize()
        {
            _itemImage?.OnFinalize();
            base.OnFinalize();
        }
    }
}
