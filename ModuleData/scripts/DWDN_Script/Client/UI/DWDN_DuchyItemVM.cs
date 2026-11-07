using System;
using TaleWorlds.Library;
using DWDN_Script.Common.Duchies;

namespace DWDN_Script.Client.UI
{
    /// <summary>
    /// ViewModel элемента списка герцогств (Банрейх, Реция и др.)
    /// </summary>
    public class DWDN_DuchyItemVM : ViewModel
    {
        private readonly DWDN_Duchy _duchy;
        private readonly Action<DWDN_DuchyItemVM> _onSelected;
        private bool _isSelected;

        public DWDN_Duchy Duchy => _duchy;
        public string Id => _duchy.Id;
        public string Name => _duchy.Name;
        public string Description => _duchy.Description;
        public string BannerCode => _duchy.BannerCode;
        public bool IsAvailable => _duchy.IsEnabled;

        [DataSourceProperty]
        public string DisplayName => _duchy.Name;

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

        [DataSourceProperty]
        public bool IsEnabled => _duchy.IsEnabled;

        public DWDN_DuchyItemVM(DWDN_Duchy duchy, Action<DWDN_DuchyItemVM> onSelected)
        {
            _duchy = duchy;
            _onSelected = onSelected;
        }

        public void ExecuteSelect()
        {
            if (_duchy.IsEnabled)
            {
                _onSelected?.Invoke(this);
            }
        }
    }
}
