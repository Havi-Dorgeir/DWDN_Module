using TaleWorlds.Library;

namespace DWDN_Script.Client.UI
{
    /// <summary>
    /// ViewModel для отзывчивого прицельного HUD сцепки полевой артиллерии DWDN.
    /// Отображает 3D-маяк на дышле, контекстную строку действия и полосу удержания [F].
    /// </summary>
    public class DWDN_ArtilleryHitchVM : ViewModel
    {
        private bool _isPromptVisible;
        private string _promptText = "";
        private float _progress;
        private float _progressFillWidth;
        private bool _isMarkerVisible;
        private float _markerX;
        private float _markerY;
        private bool _canInteract;

        [DataSourceProperty]
        public bool IsPromptVisible
        {
            get => _isPromptVisible;
            set
            {
                if (_isPromptVisible != value)
                {
                    _isPromptVisible = value;
                    OnPropertyChangedWithValue(value, "IsPromptVisible");
                }
            }
        }

        [DataSourceProperty]
        public string PromptText
        {
            get => _promptText;
            set
            {
                if (_promptText != value)
                {
                    _promptText = value;
                    OnPropertyChangedWithValue(value, "PromptText");
                }
            }
        }

        [DataSourceProperty]
        public float Progress
        {
            get => _progress;
            set
            {
                if (System.Math.Abs(_progress - value) > 0.001f)
                {
                    _progress = value;
                    OnPropertyChangedWithValue(value, "Progress");
                    ProgressFillWidth = System.Math.Max(0f, System.Math.Min(320f, value * 320f));
                }
            }
        }

        [DataSourceProperty]
        public float ProgressFillWidth
        {
            get => _progressFillWidth;
            set
            {
                if (System.Math.Abs(_progressFillWidth - value) > 0.1f)
                {
                    _progressFillWidth = value;
                    OnPropertyChangedWithValue(value, "ProgressFillWidth");
                }
            }
        }

        [DataSourceProperty]
        public bool IsMarkerVisible
        {
            get => _isMarkerVisible;
            set
            {
                if (_isMarkerVisible != value)
                {
                    _isMarkerVisible = value;
                    OnPropertyChangedWithValue(value, "IsMarkerVisible");
                }
            }
        }

        [DataSourceProperty]
        public float MarkerX
        {
            get => _markerX;
            set
            {
                if (System.Math.Abs(_markerX - value) > 0.5f)
                {
                    _markerX = value;
                    OnPropertyChangedWithValue(value, "MarkerX");
                }
            }
        }

        [DataSourceProperty]
        public float MarkerY
        {
            get => _markerY;
            set
            {
                if (System.Math.Abs(_markerY - value) > 0.5f)
                {
                    _markerY = value;
                    OnPropertyChangedWithValue(value, "MarkerY");
                }
            }
        }

        [DataSourceProperty]
        public bool CanInteract
        {
            get => _canInteract;
            set
            {
                if (_canInteract != value)
                {
                    _canInteract = value;
                    OnPropertyChangedWithValue(value, "CanInteract");
                }
            }
        }
    }
}
