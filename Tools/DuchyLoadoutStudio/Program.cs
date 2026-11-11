#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DuchyLoadoutStudio
{
    public static class Program
    {
        public static string ScreenshotPath = "";

        [STAThread]
        public static void Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--screenshot", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    ScreenshotPath = args[i + 1];
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new StudioForm());
        }
    }

    public class BufferedViewportPanel : Panel
    {
        public BufferedViewportPanel()
        {
            this.DoubleBuffered = true;
            this.ResizeRedraw = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
    }

    public class StudioForm : Form
    {
        private string _modRoot = @"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\DWDN_TRY";
        private string _nativeRoot = @"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\Native";
        private string _outDir = @"d:\banAImod\DuchyLoadoutStudio\output";

        private List<DuchyModel> _duchies = new();
        private Dictionary<string, GameItemEntry> _catalog = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, Mesh3DData> _meshIndex = new(StringComparer.OrdinalIgnoreCase);

        private DuchyModel? _selectedDuchy;
        private ClassModel? _selectedClass;
        private SlotCategory _activeSlotCategory = SlotCategory.HeadArmor;
        private bool _suppressEvents = false;

        // Current 3D preview equipment overrides per category (defaults to first item in class pool)
        private Dictionary<SlotCategory, string> _previewSelectedItems = new();

        // 3D Viewport camera state
        private float _camYaw = 155.0f; // Looking at the front of the warrior
        private float _camPitch = 10.0f;
        private float _camZoom = 2.65f;
        private float _camPanX = 0.0f;
        private float _camPanY = 0.0f;
        private bool _showWireframe = false;
        private bool _showArmorBounds = true;
        private Point _lastMouse;
        private MouseButtons _dragButton = MouseButtons.None;

        // UI Controls - Left Panel (Duchies, Classes, Balance)
        private ComboBox _cmbDuchies = null!;
        private TextBox _txtDuchyId = null!;
        private TextBox _txtDuchyName = null!;
        private TextBox _txtDuchyDesc = null!;
        private CheckBox _chkDuchyAvail = null!;
        private TextBox _txtDuchyBanner = null!;

        private ListBox _lstClasses = null!;
        private TextBox _txtClassId = null!;
        private TextBox _txtTroopId = null!;
        private TextBox _txtClassName = null!;
        private TextBox _txtClassDesc = null!;
        private ComboBox _cmbGroup = null!;
        private ComboBox _cmbIcon = null!;
        private NumericUpDown _numCost = null!;
        private NumericUpDown _numArmor = null!;
        private NumericUpDown _numHp = null!;
        private NumericUpDown _numMoveSpeed = null!;
        private NumericUpDown _numCombatSpeed = null!;
        private NumericUpDown _numAccel = null!;
        private NumericUpDown _numRiding = null!;
        private NumericUpDown _numOneHanded = null!;
        private NumericUpDown _numTwoHanded = null!;
        private NumericUpDown _numPolearm = null!;
        private NumericUpDown _numBow = null!;
        private NumericUpDown _numCrossbow = null!;

        // UI Controls - Center Panel (10-Slot Pool Editor + 3D Mannequin Viewport)
        private Button[] _slotTabButtons = new Button[10];
        private ListBox _lstAllowedPool = null!;
        private TextBox _txtCatalogSearch = null!;
        private CheckBox _chkOnlyCustomDwdn = null!;
        private ListBox _lstCatalog = null!;
        private Label _lblSelectedCatalogInfo = null!;
        private BufferedViewportPanel _viewport = null!;
        private Label _lblMannequinStats = null!;

        // UI Controls - Right Panel (Code Generation & Sync)
        private TabControl _tabCode = null!;
        private RichTextBox _rtbCSharp = null!;
        private RichTextBox _rtbXml = null!;
        private Label _lblStatus = null!;

        private static readonly (SlotCategory Cat, string Label, Color Accent)[] SlotTabs = new[]
        {
            (SlotCategory.HeadArmor, "1. Шлемы", Color.FromArgb(56, 189, 248)),
            (SlotCategory.Cape,      "2. Плащи", Color.FromArgb(168, 85, 247)),
            (SlotCategory.BodyArmor, "3. Корпус", Color.FromArgb(52, 211, 153)),
            (SlotCategory.Gloves,    "4. Перчатки", Color.FromArgb(251, 191, 36)),
            (SlotCategory.Legs,      "5. Поножи", Color.FromArgb(244, 114, 182)),
            (SlotCategory.Weapon0,   "6. Оружие 0", Color.FromArgb(248, 113, 113)),
            (SlotCategory.Weapon1,   "7. Оружие 1", Color.FromArgb(251, 146, 60)),
            (SlotCategory.Weapon2,   "8. Оружие 2", Color.FromArgb(250, 204, 21)),
            (SlotCategory.Weapon3,   "9. Оружие 3", Color.FromArgb(163, 230, 53)),
            (SlotCategory.Mount,     "10. Кони", Color.FromArgb(96, 165, 250))
        };

        public StudioForm()
        {
            this.Text = "DWDN Duchy & Class Loadout Studio v1.0 — Визуальный конструктор герцогств, классов и 3D-манекена";
            this.Size = new Size(1680, 990);
            this.MinimumSize = new Size(1380, 860);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(18, 20, 26);
            this.ForeColor = Color.FromArgb(230, 235, 245);
            this.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            BuildUI();
            LoadModData();

            if (!string.IsNullOrEmpty(Program.ScreenshotPath))
            {
                var timer = new System.Windows.Forms.Timer { Interval = 950 };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(Program.ScreenshotPath)!);
                        using var bmp = new Bitmap(this.Width, this.Height);
                        this.DrawToBitmap(bmp, new Rectangle(0, 0, this.Width, this.Height));
                        bmp.Save(Program.ScreenshotPath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    catch { }
                    this.Close();
                };
                this.Shown += (s, e) => timer.Start();
            }
        }

        private void BuildUI()
        {
            // Top Header
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 46,
                BackColor = Color.FromArgb(13, 15, 20)
            };

            var lblTitle = new Label
            {
                Text = "🛡️ DWDN DUCHY & LOADOUT STUDIO — КОНСТРУКТОР ГЕРЦОГСТВ, КЛАССОВ И 3D-МАНЕКЕНА",
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248),
                Location = new Point(14, 11),
                AutoSize = true
            };
            header.Controls.Add(lblTitle);

            var headerBtnBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 710,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 7, 10, 0),
                BackColor = Color.Transparent
            };

            var btnApplyMod = CreateFlatButton("⚡ АВТО-ПРИМЕНИТЬ В МОД DWDN_TRY", Color.FromArgb(217, 119, 6), Point.Empty, new Size(265, 31));
            btnApplyMod.Margin = new Padding(6, 0, 0, 0);
            btnApplyMod.Click += (s, e) => ApplyDirectlyToMod();
            headerBtnBar.Controls.Add(btnApplyMod);

            var btnSavePreview = CreateFlatButton("💾 Экспорт в output/", Color.FromArgb(22, 101, 52), Point.Empty, new Size(185, 31));
            btnSavePreview.Margin = new Padding(6, 0, 0, 0);
            btnSavePreview.Click += (s, e) => SaveGeneratedFilesToOutput();
            headerBtnBar.Controls.Add(btnSavePreview);

            var btnReload = CreateFlatButton("🔄 Перезагрузить из DWDN_TRY", Color.FromArgb(35, 48, 68), Point.Empty, new Size(215, 31));
            btnReload.Margin = new Padding(6, 0, 0, 0);
            btnReload.Click += (s, e) => LoadModData();
            headerBtnBar.Controls.Add(btnReload);

            header.Controls.Add(headerBtnBar);

            // Bottom Status Bar
            var statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                BackColor = Color.FromArgb(13, 15, 20)
            };
            _lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                ForeColor = Color.FromArgb(148, 163, 184),
                Text = "Инициализация студии..."
            };
            statusPanel.Controls.Add(_lblStatus);

            // Left Panel (Duchy + Class List + Balance & Skills)
            var leftPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 420,
                BackColor = Color.FromArgb(22, 25, 34),
                Padding = new Padding(10)
            };
            BuildLeftPanel(leftPanel);

            // Right Panel (Live C# + XML Code Preview)
            var rightPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 450,
                BackColor = Color.FromArgb(22, 25, 34),
                Padding = new Padding(10)
            };
            BuildRightPanel(rightPanel);

            // Center Panel (10-Slot Pool Editor + 3D Warrior Mannequin)
            var centerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(16, 18, 24),
                Padding = new Padding(8)
            };
            BuildCenterPanel(centerPanel);

            this.Controls.Add(centerPanel);
            this.Controls.Add(leftPanel);
            this.Controls.Add(rightPanel);
            this.Controls.Add(header);
            this.Controls.Add(statusPanel);
        }

        private void BuildLeftPanel(Panel parent)
        {
            int y = 6;

            // Section 1: Duchy Selector & Properties
            parent.Controls.Add(CreateSectionHeader("1. ГЕРЦОГСТВО (DWDN_Duchy)", ref y));

            _cmbDuchies = new ComboBox
            {
                Location = new Point(10, y),
                Size = new Size(275, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(30, 35, 48),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            _cmbDuchies.SelectedIndexChanged += (s, e) =>
            {
                if (_suppressEvents) return;
                if (_cmbDuchies.SelectedIndex >= 0 && _cmbDuchies.SelectedIndex < _duchies.Count)
                    SelectDuchy(_duchies[_cmbDuchies.SelectedIndex]);
            };
            parent.Controls.Add(_cmbDuchies);

            var btnNewDuchy = CreateFlatButton("➕ Новое", Color.FromArgb(37, 99, 235), new Point(292, y - 1), new Size(115, 27));
            btnNewDuchy.Click += (s, e) => CreateNewDuchy();
            parent.Controls.Add(btnNewDuchy);
            y += 32;

            _txtDuchyId = AddLabeledTextBox(parent, "ID герц.:", 10, y, 68, 125);
            _chkDuchyAvail = new CheckBox
            {
                Text = "Доступно в UI",
                Location = new Point(215, y + 2),
                Size = new Size(125, 22),
                ForeColor = Color.FromArgb(74, 222, 128)
            };
            parent.Controls.Add(_chkDuchyAvail);
            y += 26;

            _txtDuchyName = AddLabeledTextBox(parent, "Имя:", 10, y, 68, 329);
            y += 26;
            _txtDuchyDesc = AddLabeledTextBox(parent, "Описание:", 10, y, 68, 329);
            y += 26;
            _txtDuchyBanner = AddLabeledTextBox(parent, "Знамя:", 10, y, 68, 329);
            y += 30;

            EventHandler duchyChanged = (s, e) =>
            {
                if (_suppressEvents || _selectedDuchy == null) return;
                _selectedDuchy.Id = _txtDuchyId.Text.Trim();
                _selectedDuchy.Name = _txtDuchyName.Text;
                _selectedDuchy.Description = _txtDuchyDesc.Text;
                _selectedDuchy.BannerCode = _txtDuchyBanner.Text.Trim();
                _selectedDuchy.IsAvailable = _chkDuchyAvail.Checked;
                RefreshCodePreview();
            };
            _txtDuchyId.TextChanged += duchyChanged;
            _txtDuchyName.TextChanged += duchyChanged;
            _txtDuchyDesc.TextChanged += duchyChanged;
            _txtDuchyBanner.TextChanged += duchyChanged;
            _chkDuchyAvail.CheckedChanged += duchyChanged;

            // Section 2: Classes in Duchy
            parent.Controls.Add(CreateSectionHeader("2. КЛАССЫ ГЕРЦОГСТВА (DWDN_ClassDefinition)", ref y));

            _lstClasses = new ListBox
            {
                Location = new Point(10, y),
                Size = new Size(397, 130),
                BackColor = Color.FromArgb(14, 17, 24),
                ForeColor = Color.FromArgb(226, 232, 240),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9.5f)
            };
            _lstClasses.SelectedIndexChanged += (s, e) =>
            {
                if (_suppressEvents || _selectedDuchy == null) return;
                if (_lstClasses.SelectedIndex >= 0 && _lstClasses.SelectedIndex < _selectedDuchy.Classes.Count)
                    SelectClass(_selectedDuchy.Classes[_lstClasses.SelectedIndex]);
            };
            parent.Controls.Add(_lstClasses);
            y += 134;

            var btnAddClass = CreateFlatButton("➕ Новый класс", Color.FromArgb(22, 101, 52), new Point(10, y), new Size(118, 27));
            btnAddClass.Click += (s, e) => CreateNewClass();
            parent.Controls.Add(btnAddClass);

            var btnDupClass = CreateFlatButton("📋 Дублировать", Color.FromArgb(30, 64, 175), new Point(133, y), new Size(122, 27));
            btnDupClass.Click += (s, e) => DuplicateSelectedClass();
            parent.Controls.Add(btnDupClass);

            var btnDelClass = CreateFlatButton("🗑️ Удалить", Color.FromArgb(153, 27, 27), new Point(260, y), new Size(90, 27));
            btnDelClass.Click += (s, e) => DeleteSelectedClass();
            parent.Controls.Add(btnDelClass);

            var btnMoveUp = CreateFlatButton("▲", Color.FromArgb(45, 55, 72), new Point(355, y), new Size(25, 27));
            btnMoveUp.Click += (s, e) => MoveSelectedClass(-1);
            parent.Controls.Add(btnMoveUp);

            var btnMoveDown = CreateFlatButton("▼", Color.FromArgb(45, 55, 72), new Point(382, y), new Size(25, 27));
            btnMoveDown.Click += (s, e) => MoveSelectedClass(1);
            parent.Controls.Add(btnMoveDown);
            y += 33;

            // Section 3: Selected Class Balance & XML Properties
            parent.Controls.Add(CreateSectionHeader("3. ПАРАМЕТРЫ КЛАССА, ХАРАКТЕРИСТИКИ И НАВЫКИ", ref y));

            _txtClassId = AddLabeledTextBox(parent, "Class ID:", 10, y, 68, 128);
            _txtTroopId = AddLabeledTextBox(parent, "Troop ID:", 212, y, 64, 131);
            y += 26;

            _txtClassName = AddLabeledTextBox(parent, "Имя:", 10, y, 68, 329);
            y += 26;
            _txtClassDesc = AddLabeledTextBox(parent, "Роль/Опис:", 10, y, 68, 329);
            y += 26;

            parent.Controls.Add(new Label { Text = "Группа:", Location = new Point(10, y + 3), Size = new Size(65, 20), ForeColor = Color.FromArgb(148, 163, 184) });
            _cmbGroup = new ComboBox
            {
                Location = new Point(78, y),
                Size = new Size(118, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(30, 35, 48),
                ForeColor = Color.White
            };
            _cmbGroup.Items.AddRange(new object[] { "Infantry", "Ranged", "Cavalry", "HorseArcher" });
            parent.Controls.Add(_cmbGroup);

            parent.Controls.Add(new Label { Text = "Иконка:", Location = new Point(204, y + 3), Size = new Size(58, 20), ForeColor = Color.FromArgb(148, 163, 184) });
            _cmbIcon = new ComboBox
            {
                Location = new Point(264, y),
                Size = new Size(143, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(30, 35, 48),
                ForeColor = Color.White
            };
            _cmbIcon.Items.AddRange(new object[]
            {
                "Infantry_Light", "Infantry_Heavy", "Spearman", "Shock_Infantry", "Berserker",
                "Bowman", "Crossbowman", "Skirmisher",
                "Cavalry_Light", "Cavalry_Heavy", "Lancer", "Horse_Archer"
            });
            parent.Controls.Add(_cmbIcon);
            y += 29;

            // Numeric stats row 1: Cost, Armor, HP
            _numCost = AddLabeledNumeric(parent, "Цена:", 10, y, 48, 65, 0, 5000, 0, 10);
            _numArmor = AddLabeledNumeric(parent, "Броня:", 142, y, 52, 62, 0, 200, 0, 1);
            _numHp = AddLabeledNumeric(parent, "HP:", 275, y, 38, 68, 50, 1000, 0, 5);
            y += 28;

            // Numeric stats row 2: Speeds
            _numMoveSpeed = AddLabeledNumeric(parent, "Бег:", 10, y, 48, 65, 0.30m, 2.00m, 2, 0.02m);
            _numCombatSpeed = AddLabeledNumeric(parent, "Бой.ск:", 142, y, 52, 62, 0.30m, 2.00m, 2, 0.02m);
            _numAccel = AddLabeledNumeric(parent, "Ускор:", 275, y, 48, 58, 0.50m, 5.00m, 2, 0.05m);
            y += 30;

            // Skills grid
            parent.Controls.Add(new Label
            {
                Text = "Боевые навыки (mpcharacters.xml <skills>): обычно 60–300",
                Location = new Point(10, y),
                Size = new Size(390, 18),
                ForeColor = Color.FromArgb(56, 189, 248),
                Font = new Font("Segoe UI", 8.2f, FontStyle.Bold)
            });
            y += 20;

            _numOneHanded = AddLabeledNumeric(parent, "1H меч:", 10, y, 58, 58, 0, 500, 0, 10);
            _numTwoHanded = AddLabeledNumeric(parent, "2H меч:", 142, y, 58, 58, 0, 500, 0, 10);
            _numPolearm = AddLabeledNumeric(parent, "Древк:", 275, y, 52, 58, 0, 500, 0, 10);
            y += 27;

            _numBow = AddLabeledNumeric(parent, "Луки:", 10, y, 58, 58, 0, 500, 0, 10);
            _numCrossbow = AddLabeledNumeric(parent, "Арбал:", 142, y, 58, 58, 0, 500, 0, 10);
            _numRiding = AddLabeledNumeric(parent, "Верх.:", 275, y, 52, 58, 0, 500, 0, 10);

            EventHandler classPropsChanged = (s, e) =>
            {
                if (_suppressEvents || _selectedClass == null) return;
                _selectedClass.Id = _txtClassId.Text.Trim();
                _selectedClass.TroopId = _txtTroopId.Text.Trim();
                _selectedClass.Name = _txtClassName.Text;
                _selectedClass.Description = _txtClassDesc.Text;
                if (_cmbGroup.SelectedItem != null) _selectedClass.Group = _cmbGroup.SelectedItem.ToString()!;
                if (_cmbIcon.SelectedItem != null) _selectedClass.IconType = _cmbIcon.SelectedItem.ToString()!;

                _selectedClass.Cost = (int)_numCost.Value;
                _selectedClass.Armor = (int)_numArmor.Value;
                _selectedClass.Hitpoints = (int)_numHp.Value;
                _selectedClass.MovementSpeed = (float)_numMoveSpeed.Value;
                _selectedClass.CombatMovementSpeed = (float)_numCombatSpeed.Value;
                _selectedClass.Acceleration = (float)_numAccel.Value;

                _selectedClass.SkillOneHanded = (int)_numOneHanded.Value;
                _selectedClass.SkillTwoHanded = (int)_numTwoHanded.Value;
                _selectedClass.SkillPolearm = (int)_numPolearm.Value;
                _selectedClass.SkillBow = (int)_numBow.Value;
                _selectedClass.SkillCrossbow = (int)_numCrossbow.Value;
                _selectedClass.SkillRiding = (int)_numRiding.Value;

                UpdateClassListDisplay();
                RefreshCodePreview();
            };

            _txtClassId.TextChanged += classPropsChanged;
            _txtTroopId.TextChanged += classPropsChanged;
            _txtClassName.TextChanged += classPropsChanged;
            _txtClassDesc.TextChanged += classPropsChanged;
            _cmbGroup.SelectedIndexChanged += classPropsChanged;
            _cmbIcon.SelectedIndexChanged += classPropsChanged;
            _numCost.ValueChanged += classPropsChanged;
            _numArmor.ValueChanged += classPropsChanged;
            _numHp.ValueChanged += classPropsChanged;
            _numMoveSpeed.ValueChanged += classPropsChanged;
            _numCombatSpeed.ValueChanged += classPropsChanged;
            _numAccel.ValueChanged += classPropsChanged;
            _numOneHanded.ValueChanged += classPropsChanged;
            _numTwoHanded.ValueChanged += classPropsChanged;
            _numPolearm.ValueChanged += classPropsChanged;
            _numBow.ValueChanged += classPropsChanged;
            _numCrossbow.ValueChanged += classPropsChanged;
            _numRiding.ValueChanged += classPropsChanged;
        }

        private void BuildCenterPanel(Panel parent)
        {
            // Split Center into Top (10-Slot Equipment Pool Editor) and Bottom (3D Warrior Mannequin Viewport)
            var topSlotsPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 365,
                BackColor = Color.FromArgb(20, 23, 31),
                Padding = new Padding(8)
            };

            int y = 6;
            topSlotsPanel.Controls.Add(CreateSectionHeader("4. ПУЛЫ ДОСТУПНОГО СНАРЯЖЕНИЯ КЛАССА (10 СЛОТОВ ЛОДАУТА)", ref y));

            // 10 Slot Category Buttons (2 rows of 5 for full readability)
            var tabBar = new Panel
            {
                Location = new Point(8, y),
                Size = new Size(775, 56),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            for (int i = 0; i < SlotTabs.Length; i++)
            {
                var tab = SlotTabs[i];
                int row = i / 5;
                int col = i % 5;
                var btn = new Button
                {
                    Text = tab.Label,
                    Location = new Point(col * 154, row * 28),
                    Size = new Size(150, 26),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    BackColor = Color.FromArgb(30, 36, 48),
                    ForeColor = Color.White,
                    Cursor = Cursors.Hand
                };
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.BorderColor = tab.Accent;
                btn.Click += (s, e) => SelectSlotCategory(tab.Cat);
                _slotTabButtons[i] = btn;
                tabBar.Controls.Add(btn);
            }
            topSlotsPanel.Controls.Add(tabBar);
            y += 60;

            // Left column inside topSlotsPanel: Allowed Items in this Class Slot
            var lblAllowed = new Label
            {
                Text = "Доступно классу (1-й предмет = экипирован по умолчанию в XML):",
                Location = new Point(8, y),
                Size = new Size(375, 20),
                ForeColor = Color.FromArgb(74, 222, 128),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            topSlotsPanel.Controls.Add(lblAllowed);

            _lstAllowedPool = new ListBox
            {
                Location = new Point(8, y + 22),
                Size = new Size(365, 190),
                BackColor = Color.FromArgb(13, 16, 22),
                ForeColor = Color.FromArgb(240, 246, 252),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9.0f)
            };
            _lstAllowedPool.SelectedIndexChanged += (s, e) =>
            {
                if (_suppressEvents || _selectedClass == null) return;
                var pool = _selectedClass.Pools[_activeSlotCategory];
                if (_lstAllowedPool.SelectedIndex >= 0 && _lstAllowedPool.SelectedIndex < pool.Count)
                {
                    string itemId = pool[_lstAllowedPool.SelectedIndex];
                    _previewSelectedItems[_activeSlotCategory] = itemId;
                    ShowCatalogItemDetails(itemId);
                    _viewport?.Invalidate();
                }
            };
            topSlotsPanel.Controls.Add(_lstAllowedPool);

            // Action buttons under Allowed Pool
            int btnY = y + 216;
            var btnMakeDefault = CreateFlatButton("⭐ Сделать дефолтным (№1)", Color.FromArgb(30, 64, 175), new Point(8, btnY), new Size(172, 27));
            btnMakeDefault.Click += (s, e) => MakeAllowedItemDefault();
            topSlotsPanel.Controls.Add(btnMakeDefault);

            var btnAddEmpty = CreateFlatButton("➕ Пустой слот (\"\")", Color.FromArgb(55, 65, 81), new Point(185, btnY), new Size(115, 27));
            btnAddEmpty.Click += (s, e) => AddItemToClassPool("");
            topSlotsPanel.Controls.Add(btnAddEmpty);

            var btnRemovePool = CreateFlatButton("🗑️ Убрать", Color.FromArgb(153, 27, 27), new Point(305, btnY), new Size(68, 27));
            btnRemovePool.Click += (s, e) => RemoveSelectedFromClassPool();
            topSlotsPanel.Controls.Add(btnRemovePool);

            // Right column inside topSlotsPanel: Master Catalog Search & Add
            var lblCatalog = new Label
            {
                Text = "Каталог предметов (DWDN + Native):",
                Location = new Point(388, y),
                Size = new Size(220, 20),
                ForeColor = Color.FromArgb(56, 189, 248),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            topSlotsPanel.Controls.Add(lblCatalog);

            _chkOnlyCustomDwdn = new CheckBox
            {
                Text = "Только кастом DWDN",
                Location = new Point(620, y - 1),
                Size = new Size(155, 22),
                Checked = false,
                ForeColor = Color.FromArgb(250, 204, 21)
            };
            _chkOnlyCustomDwdn.CheckedChanged += (s, e) => RefreshCatalogList();
            topSlotsPanel.Controls.Add(_chkOnlyCustomDwdn);

            _txtCatalogSearch = new TextBox
            {
                Location = new Point(388, y + 22),
                Size = new Size(255, 24),
                BackColor = Color.FromArgb(28, 33, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "🔍 Поиск по ID или названию..."
            };
            _txtCatalogSearch.TextChanged += (s, e) => RefreshCatalogList();
            topSlotsPanel.Controls.Add(_txtCatalogSearch);

            var btnAddCatalogItem = CreateFlatButton("⬅️ Добавить в класс", Color.FromArgb(22, 101, 52), new Point(648, y + 21), new Size(130, 26));
            btnAddCatalogItem.Click += (s, e) => AddSelectedCatalogItemToClass();
            topSlotsPanel.Controls.Add(btnAddCatalogItem);

            _lstCatalog = new ListBox
            {
                Location = new Point(388, y + 50),
                Size = new Size(390, 162),
                BackColor = Color.FromArgb(13, 16, 22),
                ForeColor = Color.FromArgb(203, 213, 225),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.8f)
            };
            _lstCatalog.DoubleClick += (s, e) => AddSelectedCatalogItemToClass();
            _lstCatalog.SelectedIndexChanged += (s, e) =>
            {
                if (_lstCatalog.SelectedItem is CatalogListEntry entry)
                {
                    ShowCatalogItemDetails(entry.Item.Id);
                    // Live preview on the 3D mannequin when clicking catalog items!
                    _previewSelectedItems[_activeSlotCategory] = entry.Item.Id;
                    _viewport?.Invalidate();
                }
            };
            topSlotsPanel.Controls.Add(_lstCatalog);

            _lblSelectedCatalogInfo = new Label
            {
                Location = new Point(388, btnY + 3),
                Size = new Size(390, 24),
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Consolas", 8.3f),
                Text = "Выберите предмет для просмотра 3D-меша и статов..."
            };
            topSlotsPanel.Controls.Add(_lblSelectedCatalogInfo);

            // Bottom of Center Panel: 3D Warrior Mannequin Viewport
            var viewportContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(12, 14, 20),
                Padding = new Padding(0, 6, 0, 0)
            };

            _viewport = new BufferedViewportPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(12, 15, 22)
            };
            _viewport.Paint += Viewport_Paint;
            _viewport.MouseDown += Viewport_MouseDown;
            _viewport.MouseMove += Viewport_MouseMove;
            _viewport.MouseUp += (s, e) => _dragButton = MouseButtons.None;
            _viewport.MouseWheel += Viewport_MouseWheel;

            // Overlay Toolbar inside 3D Viewport
            var btnResetCam = CreateFlatButton("🎥 Сброс камеры", Color.FromArgb(30, 41, 59), new Point(12, 42), new Size(125, 26));
            btnResetCam.Click += (s, e) =>
            {
                _camYaw = 155f; _camPitch = 10f; _camZoom = 2.65f; _camPanX = 0f; _camPanY = 0f;
                _viewport.Invalidate();
            };
            _viewport.Controls.Add(btnResetCam);

            var chkWire = new CheckBox
            {
                Text = "Сетка (Wireframe)",
                Location = new Point(148, 45),
                Size = new Size(135, 22),
                ForeColor = Color.FromArgb(148, 163, 184),
                BackColor = Color.Transparent
            };
            chkWire.CheckedChanged += (s, e) => { _showWireframe = chkWire.Checked; _viewport.Invalidate(); };
            _viewport.Controls.Add(chkWire);

            var chkBounds = new CheckBox
            {
                Text = "Слои брони",
                Location = new Point(290, 45),
                Size = new Size(110, 22),
                Checked = true,
                ForeColor = Color.FromArgb(148, 163, 184),
                BackColor = Color.Transparent
            };
            chkBounds.CheckedChanged += (s, e) => { _showArmorBounds = chkBounds.Checked; _viewport.Invalidate(); };
            _viewport.Controls.Add(chkBounds);

            _lblMannequinStats = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                BackColor = Color.FromArgb(18, 22, 32),
                ForeColor = Color.FromArgb(56, 189, 248),
                Font = new Font("Consolas", 9.0f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Text = "3D Манекен готов."
            };
            _viewport.Controls.Add(_lblMannequinStats);

            viewportContainer.Controls.Add(_viewport);
            parent.Controls.Add(viewportContainer);
            parent.Controls.Add(topSlotsPanel);
        }

        private void BuildRightPanel(Panel parent)
        {
            int y = 6;
            parent.Controls.Add(CreateSectionHeader("5. АВТО-ПРИМЕНЕНИЕ В МОД И ГЕНЕРАТОР КОДА", ref y));

            var infoLbl = new Label
            {
                Text = "Кнопка ниже автоматически обновляет все 3 файла мода\n(DWDN_DuchyManager.cs, mpclassdivisions.xml, mpcharacters.xml)\nи компилирует DWDN_Script.dll:",
                Location = new Point(10, y),
                Size = new Size(425, 48),
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8.3f)
            };
            parent.Controls.Add(infoLbl);
            y += 50;

            var btnApplyRight = CreateFlatButton("⚡ АВТО-ПРИМЕНИТЬ В МОД DWDN_TRY (C# + XML + СБОРКА DLL)", Color.FromArgb(217, 119, 6), new Point(10, y), new Size(428, 34));
            btnApplyRight.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnApplyRight.Click += (s, e) => ApplyDirectlyToMod();
            parent.Controls.Add(btnApplyRight);
            y += 39;

            var btnCopyCode = CreateFlatButton("📋 Скопировать активную вкладку", Color.FromArgb(37, 99, 235), new Point(10, y), new Size(225, 27));
            btnCopyCode.Click += (s, e) =>
            {
                var rtb = _tabCode.SelectedIndex == 0 ? _rtbCSharp : _rtbXml;
                if (!string.IsNullOrEmpty(rtb.Text))
                {
                    Clipboard.SetText(rtb.Text);
                    _lblStatus.Text = "✅ Код скопирован в буфер обмена!";
                }
            };
            parent.Controls.Add(btnCopyCode);

            var btnExportNow = CreateFlatButton("💾 Экспорт в output/", Color.FromArgb(22, 101, 52), new Point(242, y), new Size(196, 27));
            btnExportNow.Click += (s, e) => SaveGeneratedFilesToOutput();
            parent.Controls.Add(btnExportNow);
            y += 32;

            _tabCode = new TabControl
            {
                Location = new Point(10, y),
                Size = new Size(428, 725),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var tabCs = new TabPage("1. C# (DWDN_DuchyManager.cs)");
            _rtbCSharp = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(12, 14, 20),
                ForeColor = Color.FromArgb(134, 239, 172),
                Font = new Font("Consolas", 8.5f),
                ReadOnly = true,
                WordWrap = false
            };
            tabCs.Controls.Add(_rtbCSharp);

            var tabXml = new TabPage("2. XML (Divisions + Characters)");
            _rtbXml = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(12, 14, 20),
                ForeColor = Color.FromArgb(125, 211, 252),
                Font = new Font("Consolas", 8.5f),
                ReadOnly = true,
                WordWrap = false
            };
            tabXml.Controls.Add(_rtbXml);

            _tabCode.TabPages.Add(tabCs);
            _tabCode.TabPages.Add(tabXml);
            parent.Controls.Add(_tabCode);
        }

        private void LoadModData()
        {
            try
            {
                _catalog = StudioDataLoader.LoadAllItems(_modRoot, _nativeRoot);
                _duchies = StudioDataLoader.LoadDuchiesAndClasses(_modRoot);
                _meshIndex = StudioDataLoader.LoadAll3DMeshes(_modRoot);

                _suppressEvents = true;
                _cmbDuchies.Items.Clear();
                foreach (var d in _duchies)
                {
                    _cmbDuchies.Items.Add($"{d.Name} [{d.Id}] ({d.Classes.Count} кл.)");
                }
                _suppressEvents = false;

                if (_duchies.Count > 0)
                {
                    _cmbDuchies.SelectedIndex = 0;
                    SelectDuchy(_duchies[0]);
                }

                SelectSlotCategory(SlotCategory.HeadArmor);
                _lblStatus.Text = $"✅ Загружено герцогств: {_duchies.Count} | Классов: {_duchies.Sum(d => d.Classes.Count)} | Предметов в каталоге: {_catalog.Count} | 3D-мешей DWDN: {_meshIndex.Count}";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"❌ Ошибка загрузки: {ex.Message}";
            }
        }

        private void SelectDuchy(DuchyModel duchy)
        {
            _selectedDuchy = duchy;
            _suppressEvents = true;

            _txtDuchyId.Text = duchy.Id;
            _txtDuchyName.Text = duchy.Name;
            _txtDuchyDesc.Text = duchy.Description;
            _txtDuchyBanner.Text = duchy.BannerCode;
            _chkDuchyAvail.Checked = duchy.IsAvailable;

            UpdateClassListDisplay();
            _suppressEvents = false;

            if (duchy.Classes.Count > 0)
            {
                _lstClasses.SelectedIndex = 0;
                SelectClass(duchy.Classes[0]);
            }
            else
            {
                _selectedClass = null;
                RefreshAllowedPoolList();
                RefreshCodePreview();
            }
        }

        private void UpdateClassListDisplay()
        {
            if (_selectedDuchy == null) return;
            int prevIdx = _lstClasses.SelectedIndex;
            _suppressEvents = true;
            _lstClasses.Items.Clear();
            foreach (var c in _selectedDuchy.Classes)
            {
                string iconShort = c.Group switch
                {
                    "Ranged" => "🏹",
                    "Cavalry" => "🐎",
                    "HorseArcher" => "🏇",
                    _ => "⚔️"
                };
                _lstClasses.Items.Add($"{iconShort} {c.Name,-18} [{c.Id}] (Armor:{c.Armor} Cost:{c.Cost})");
            }
            if (prevIdx >= 0 && prevIdx < _lstClasses.Items.Count)
                _lstClasses.SelectedIndex = prevIdx;
            _suppressEvents = false;
        }

        private void SelectClass(ClassModel cls)
        {
            _selectedClass = cls;
            _suppressEvents = true;

            _txtClassId.Text = cls.Id;
            _txtTroopId.Text = cls.TroopId;
            _txtClassName.Text = cls.Name;
            _txtClassDesc.Text = cls.Description;

            if (_cmbGroup.Items.Contains(cls.Group))
                _cmbGroup.SelectedItem = cls.Group;
            else
                _cmbGroup.SelectedIndex = 0;

            if (_cmbIcon.Items.Contains(cls.IconType))
                _cmbIcon.SelectedItem = cls.IconType;
            else
                _cmbIcon.SelectedIndex = 0;

            _numCost.Value = Math.Clamp(cls.Cost, 0, 5000);
            _numArmor.Value = Math.Clamp(cls.Armor, 0, 200);
            _numHp.Value = Math.Clamp(cls.Hitpoints, 50, 1000);
            _numMoveSpeed.Value = Math.Clamp((decimal)cls.MovementSpeed, 0.30m, 2.00m);
            _numCombatSpeed.Value = Math.Clamp((decimal)cls.CombatMovementSpeed, 0.30m, 2.00m);
            _numAccel.Value = Math.Clamp((decimal)cls.Acceleration, 0.50m, 5.00m);

            _numOneHanded.Value = Math.Clamp(cls.SkillOneHanded, 0, 500);
            _numTwoHanded.Value = Math.Clamp(cls.SkillTwoHanded, 0, 500);
            _numPolearm.Value = Math.Clamp(cls.SkillPolearm, 0, 500);
            _numBow.Value = Math.Clamp(cls.SkillBow, 0, 500);
            _numCrossbow.Value = Math.Clamp(cls.SkillCrossbow, 0, 500);
            _numRiding.Value = Math.Clamp(cls.SkillRiding, 0, 500);

            // Reset preview selections to the first non-empty item in each slot of the selected class
            _previewSelectedItems.Clear();
            foreach (var tab in SlotTabs)
            {
                var pool = cls.Pools[tab.Cat];
                string firstItem = pool.FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? "";
                _previewSelectedItems[tab.Cat] = firstItem;
            }

            _suppressEvents = false;

            RefreshSlotTabCounts();
            RefreshAllowedPoolList();
            RefreshCodePreview();
            _viewport?.Invalidate();
        }

        private void SelectSlotCategory(SlotCategory cat)
        {
            _activeSlotCategory = cat;
            RefreshSlotTabCounts();
            RefreshAllowedPoolList();
            RefreshCatalogList();
        }

        private void RefreshSlotTabCounts()
        {
            for (int i = 0; i < SlotTabs.Length; i++)
            {
                var tab = SlotTabs[i];
                int count = _selectedClass?.Pools[tab.Cat].Count ?? 0;
                bool active = tab.Cat == _activeSlotCategory;
                _slotTabButtons[i].Text = $"{tab.Label} ({count})";
                _slotTabButtons[i].BackColor = active ? tab.Accent : Color.FromArgb(28, 34, 46);
                _slotTabButtons[i].ForeColor = active ? Color.FromArgb(10, 12, 18) : Color.White;
            }
        }

        private void RefreshAllowedPoolList()
        {
            _suppressEvents = true;
            _lstAllowedPool.Items.Clear();
            if (_selectedClass != null)
            {
                var pool = _selectedClass.Pools[_activeSlotCategory];
                for (int i = 0; i < pool.Count; i++)
                {
                    string id = pool[i];
                    string prefix = i == 0 ? "⭐ [ДЕФОЛТ] " : $"   [{i + 1}] ";
                    if (string.IsNullOrEmpty(id))
                    {
                        _lstAllowedPool.Items.Add($"{prefix}\"\" (Пустой слот / Без предмета)");
                    }
                    else if (_catalog.TryGetValue(id, out var item))
                    {
                        string tag = item.IsCustomModItem ? "[DWDN]" : "[Native]";
                        _lstAllowedPool.Items.Add($"{prefix}{id} — {GameItemEntry.CleanLocName(item.Name)} {tag}");
                    }
                    else
                    {
                        _lstAllowedPool.Items.Add($"{prefix}{id}");
                    }
                }
            }
            _suppressEvents = false;
            RefreshSlotTabCounts();
        }

        private class CatalogListEntry
        {
            public GameItemEntry Item { get; init; } = null!;
            public override string ToString()
            {
                string tag = Item.IsCustomModItem ? "★DWDN" : "Native";
                string stat = Item.ArmorValue > 0 ? $"Броня:{Item.ArmorValue}" : Item.ItemType;
                return $"[{tag}] {Item.Id,-28} | {GameItemEntry.CleanLocName(Item.Name)} ({stat})";
            }
        }

        private void RefreshCatalogList()
        {
            string query = _txtCatalogSearch.Text.Trim();
            bool onlyDwdn = _chkOnlyCustomDwdn.Checked;

            _lstCatalog.BeginUpdate();
            _lstCatalog.Items.Clear();

            var matches = _catalog.Values
                .Where(it => !string.IsNullOrEmpty(it.Id))
                .Where(it => MatchesCategory(it, _activeSlotCategory))
                .Where(it => !onlyDwdn || it.IsCustomModItem)
                .Where(it => string.IsNullOrEmpty(query) ||
                             it.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             it.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             it.ItemType.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(it => it.IsCustomModItem)
                .ThenBy(it => it.Id);

            foreach (var it in matches)
            {
                _lstCatalog.Items.Add(new CatalogListEntry { Item = it });
            }

            _lstCatalog.EndUpdate();
        }

        private static bool MatchesCategory(GameItemEntry item, SlotCategory slot)
        {
            string t = item.ItemType ?? "";
            return slot switch
            {
                SlotCategory.HeadArmor => t.Equals("HeadArmor", StringComparison.OrdinalIgnoreCase),
                SlotCategory.Cape      => t.Equals("Cape", StringComparison.OrdinalIgnoreCase),
                SlotCategory.BodyArmor => t.Equals("BodyArmor", StringComparison.OrdinalIgnoreCase),
                SlotCategory.Gloves    => t.Equals("HandArmor", StringComparison.OrdinalIgnoreCase),
                SlotCategory.Legs      => t.Equals("LegArmor", StringComparison.OrdinalIgnoreCase),
                SlotCategory.Mount     => t.Equals("Horse", StringComparison.OrdinalIgnoreCase),
                _ => !t.Equals("HeadArmor", StringComparison.OrdinalIgnoreCase) &&
                     !t.Equals("Cape", StringComparison.OrdinalIgnoreCase) &&
                     !t.Equals("BodyArmor", StringComparison.OrdinalIgnoreCase) &&
                     !t.Equals("HandArmor", StringComparison.OrdinalIgnoreCase) &&
                     !t.Equals("LegArmor", StringComparison.OrdinalIgnoreCase) &&
                     !t.Equals("Horse", StringComparison.OrdinalIgnoreCase) &&
                     !t.Equals("HorseHarness", StringComparison.OrdinalIgnoreCase)
            };
        }

        private void ShowCatalogItemDetails(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                _lblSelectedCatalogInfo.Text = "Пустой слот (\"\") — позволяет игроку снять предмет.";
                return;
            }
            if (_catalog.TryGetValue(itemId, out var item))
            {
                bool has3D = !string.IsNullOrEmpty(item.MeshName) && _meshIndex.ContainsKey(item.MeshName);
                string meshStatus = has3D ? "3D Меш загружен ✓" : "Нет локального 3D меша";
                _lblSelectedCatalogInfo.Text = $"ID: {item.Id} | Меш: {item.MeshName} ({meshStatus}) | Броня: {item.ArmorValue} | Вес: {item.Weight:F1}";
            }
            else
            {
                _lblSelectedCatalogInfo.Text = $"ID: {itemId} (внешний предмет)";
            }
        }

        private void AddSelectedCatalogItemToClass()
        {
            if (_selectedClass == null) return;
            if (_lstCatalog.SelectedItem is CatalogListEntry entry)
            {
                AddItemToClassPool(entry.Item.Id);
            }
        }

        private void AddItemToClassPool(string itemId)
        {
            if (_selectedClass == null) return;
            var pool = _selectedClass.Pools[_activeSlotCategory];
            if (!pool.Contains(itemId))
            {
                pool.Add(itemId);
                if (!string.IsNullOrEmpty(itemId))
                    _previewSelectedItems[_activeSlotCategory] = itemId;
                RefreshAllowedPoolList();
                RefreshCodePreview();
                _viewport.Invalidate();
            }
        }

        private void RemoveSelectedFromClassPool()
        {
            if (_selectedClass == null) return;
            var pool = _selectedClass.Pools[_activeSlotCategory];
            int idx = _lstAllowedPool.SelectedIndex;
            if (idx >= 0 && idx < pool.Count)
            {
                pool.RemoveAt(idx);
                _previewSelectedItems[_activeSlotCategory] = pool.FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? "";
                RefreshAllowedPoolList();
                RefreshCodePreview();
                _viewport.Invalidate();
            }
        }

        private void MakeAllowedItemDefault()
        {
            if (_selectedClass == null) return;
            var pool = _selectedClass.Pools[_activeSlotCategory];
            int idx = _lstAllowedPool.SelectedIndex;
            if (idx > 0 && idx < pool.Count)
            {
                string item = pool[idx];
                pool.RemoveAt(idx);
                pool.Insert(0, item);
                if (!string.IsNullOrEmpty(item))
                    _previewSelectedItems[_activeSlotCategory] = item;
                RefreshAllowedPoolList();
                _lstAllowedPool.SelectedIndex = 0;
                RefreshCodePreview();
                _viewport.Invalidate();
            }
        }

        private void CreateNewDuchy()
        {
            int num = _duchies.Count + 1;
            var newDuchy = new DuchyModel
            {
                Id = $"dwdn_duchy_{num}",
                Name = $"Новое Герцогство {num}",
                Description = "Описание нового герцогства.",
                BannerCode = "11.140.19.1536.1536.768.768.1.0.0.160.0.128.512.512.768.768.0.0.0",
                IsAvailable = true
            };
            _duchies.Add(newDuchy);
            _suppressEvents = true;
            _cmbDuchies.Items.Add($"{newDuchy.Name} [{newDuchy.Id}] (0 кл.)");
            _cmbDuchies.SelectedIndex = _duchies.Count - 1;
            _suppressEvents = false;
            SelectDuchy(newDuchy);
        }

        private void CreateNewClass()
        {
            if (_selectedDuchy == null) return;
            int num = _selectedDuchy.Classes.Count + 1;
            string baseId = $"{_selectedDuchy.Id.Replace("dwdn_", "")}_warrior_{num}";
            var cls = new ClassModel
            {
                Id = baseId,
                TroopId = $"mp_{baseId}_hero",
                Name = $"Воин {num}",
                Description = "Новый боевой класс.",
                Group = "Infantry",
                IconType = "Infantry_Heavy",
                Cost = 120,
                Armor = 30,
                Hitpoints = 100
            };
            // Pre-populate with default Bergen armor so the 3D mannequin immediately renders a knight
            cls.Pools[SlotCategory.HeadArmor].Add("bergen_inf_t2_helmet");
            cls.Pools[SlotCategory.Cape].Add("bergen_leather_shoulders");
            cls.Pools[SlotCategory.BodyArmor].Add("bergen_chainmail");
            cls.Pools[SlotCategory.Gloves].Add("bergen_leather_gloves");
            cls.Pools[SlotCategory.Legs].Add("bergen_boots");
            cls.Pools[SlotCategory.Weapon0].Add("baldur_sword");
            cls.Pools[SlotCategory.Weapon1].Add("mp_bergen_shield");

            _selectedDuchy.Classes.Add(cls);
            UpdateClassListDisplay();
            _lstClasses.SelectedIndex = _selectedDuchy.Classes.Count - 1;
            SelectClass(cls);
        }

        private void DuplicateSelectedClass()
        {
            if (_selectedDuchy == null || _selectedClass == null) return;
            var copy = _selectedClass.Clone();
            _selectedDuchy.Classes.Add(copy);
            UpdateClassListDisplay();
            _lstClasses.SelectedIndex = _selectedDuchy.Classes.Count - 1;
            SelectClass(copy);
        }

        private void DeleteSelectedClass()
        {
            if (_selectedDuchy == null || _selectedClass == null) return;
            int idx = _selectedDuchy.Classes.IndexOf(_selectedClass);
            if (idx >= 0)
            {
                _selectedDuchy.Classes.RemoveAt(idx);
                UpdateClassListDisplay();
                if (_selectedDuchy.Classes.Count > 0)
                {
                    int next = Math.Clamp(idx, 0, _selectedDuchy.Classes.Count - 1);
                    _lstClasses.SelectedIndex = next;
                    SelectClass(_selectedDuchy.Classes[next]);
                }
                else
                {
                    _selectedClass = null;
                    RefreshAllowedPoolList();
                    RefreshCodePreview();
                    _viewport.Invalidate();
                }
            }
        }

        private void MoveSelectedClass(int delta)
        {
            if (_selectedDuchy == null || _selectedClass == null) return;
            int idx = _selectedDuchy.Classes.IndexOf(_selectedClass);
            int target = idx + delta;
            if (idx >= 0 && target >= 0 && target < _selectedDuchy.Classes.Count)
            {
                var item = _selectedDuchy.Classes[idx];
                _selectedDuchy.Classes.RemoveAt(idx);
                _selectedDuchy.Classes.Insert(target, item);
                UpdateClassListDisplay();
                _lstClasses.SelectedIndex = target;
                RefreshCodePreview();
            }
        }

        private void RefreshCodePreview()
        {
            _rtbCSharp.Text = StudioDataLoader.GenerateDuchyManagerCSharp(_duchies);
            if (_selectedClass != null)
            {
                _rtbXml.Text = StudioDataLoader.GenerateClassXmlSnippets(_selectedClass);
            }
            else
            {
                _rtbXml.Text = "<!-- Выберите класс слева для генерации его XML-блоков -->";
            }
        }

        private void SaveGeneratedFilesToOutput()
        {
            try
            {
                Directory.CreateDirectory(_outDir);
                string csPath = Path.Combine(_outDir, "DWDN_DuchyManager.cs");
                string xmlSnippetsPath = Path.Combine(_outDir, "generated_classes_xml_snippets.xml");

                File.WriteAllText(csPath, StudioDataLoader.GenerateDuchyManagerCSharp(_duchies));

                var allXml = string.Join("\n\n", _duchies.SelectMany(d => d.Classes).Select(StudioDataLoader.GenerateClassXmlSnippets));
                File.WriteAllText(xmlSnippetsPath, allXml);

                _lblStatus.Text = $"💾 Сохранено в {_outDir}: DWDN_DuchyManager.cs и generated_classes_xml_snippets.xml";
                MessageBox.Show(
                    $"Сгенерированные файлы успешно сохранены в:\n\n" +
                    $"1. {csPath}\n2. {xmlSnippetsPath}",
                    "Файлы сохранены",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyDirectlyToMod()
        {
            var confirm = MessageBox.Show(
                "Автоматически применить все изменения напрямую в мод DWDN_TRY и пересобрать DLL?\n\n" +
                "Будут автоматически обновлены:\n" +
                "1. ModuleData/scripts/DWDN_Script/Common/Duchies/DWDN_DuchyManager.cs\n" +
                "2. ModuleData/mpclassdivisions.xml\n" +
                "3. ModuleData/mpcharacters.xml\n" +
                "4. bin/Win64_Shipping_Client/DWDN_Script.dll (+ Server.dll)\n\n" +
                "(Резервная копия .bak всех файлов сохраняется автоматически в output/backup_*)",
                "⚡ Автоматическое применение в мод DWDN_TRY",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                _lblStatus.Text = "⏳ Создание резервной копии и обновление C# / XML файлов мода...";
                Application.DoEvents();

                string backupDir = Path.Combine(_outDir, "backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(backupDir);

                string csTarget = Path.Combine(_modRoot, "ModuleData", "scripts", "DWDN_Script", "Common", "Duchies", "DWDN_DuchyManager.cs");
                string divTarget = Path.Combine(_modRoot, "ModuleData", "mpclassdivisions.xml");
                string charTarget = Path.Combine(_modRoot, "ModuleData", "mpcharacters.xml");
                string csprojPath = Path.Combine(_modRoot, "ModuleData", "scripts", "DWDN_Script", "DWDN_Script.csproj");

                if (File.Exists(csTarget)) File.Copy(csTarget, Path.Combine(backupDir, "DWDN_DuchyManager.cs.bak"), true);
                if (File.Exists(divTarget)) File.Copy(divTarget, Path.Combine(backupDir, "mpclassdivisions.xml.bak"), true);
                if (File.Exists(charTarget)) File.Copy(charTarget, Path.Combine(backupDir, "mpcharacters.xml.bak"), true);

                // 1. Write DWDN_DuchyManager.cs
                File.WriteAllText(csTarget, StudioDataLoader.GenerateDuchyManagerCSharp(_duchies));

                // 2. Patch mpclassdivisions.xml
                StudioDataLoader.PatchAndSaveMpClassDivisionsXml(divTarget, _duchies);

                // 3. Patch mpcharacters.xml
                StudioDataLoader.PatchAndSaveMpCharactersXml(charTarget, _duchies);

                // 4. Automatically compile DWDN_Script.csproj into DWDN_Script.dll & DWDN_Script.Server.dll
                string buildResultMsg = "DLL не пересобиралась (не найден DWDN_Script.csproj).";
                if (File.Exists(csprojPath))
                {
                    _lblStatus.Text = "🔨 Компиляция DWDN_Script.dll (dotnet build)...";
                    Application.DoEvents();

                    var psi = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"build \"{csprojPath}\" -c Debug",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        string stdout = proc.StandardOutput.ReadToEnd();
                        string stderr = proc.StandardError.ReadToEnd();
                        proc.WaitForExit(20000);
                        if (proc.ExitCode == 0)
                        {
                            buildResultMsg = "✅ DWDN_Script.dll и DWDN_Script.Server.dll успешно пересобраны!";
                        }
                        else
                        {
                            buildResultMsg = $"⚠️ Код обновлён, но сборка DLL вернула код {proc.ExitCode}:\n{stderr}\n{stdout}";
                        }
                    }
                }

                _lblStatus.Text = $"⚡ Успешно применено в DWDN_TRY (C# + XML + DLL)! Бэкап: {backupDir}";
                MessageBox.Show(
                    $"Все изменения автоматически применены в мод DWDN_TRY!\n\n" +
                    $"• Обновлён: DWDN_DuchyManager.cs\n" +
                    $"• Обновлён: ModuleData/mpclassdivisions.xml\n" +
                    $"• Обновлён: ModuleData/mpcharacters.xml\n" +
                    $"• Сборка: {buildResultMsg}\n\n" +
                    $"Резервная копия сохранена в:\n{backupDir}",
                    "⚡ Успешно применено в мод",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка применения в мод: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ==================== 3D WARRIOR MANNEQUIN RENDERER ====================

        private void Viewport_MouseDown(object? sender, MouseEventArgs e)
        {
            _dragButton = e.Button;
            _lastMouse = e.Location;
        }

        private void Viewport_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_dragButton == MouseButtons.None) return;
            int dx = e.X - _lastMouse.X;
            int dy = e.Y - _lastMouse.Y;
            _lastMouse = e.Location;

            if (_dragButton == MouseButtons.Left)
            {
                _camYaw += dx * 0.55f;
                _camPitch = Math.Clamp(_camPitch + dy * 0.45f, -75f, 75f);
                _viewport.Invalidate();
            }
            else if (_dragButton == MouseButtons.Right || _dragButton == MouseButtons.Middle)
            {
                _camPanX += dx;
                _camPanY += dy;
                _viewport.Invalidate();
            }
        }

        private void Viewport_MouseWheel(object? sender, MouseEventArgs e)
        {
            float factor = e.Delta > 0 ? 1.12f : 0.89f;
            _camZoom = Math.Clamp(_camZoom * factor, 0.7f, 9.5f);
            _viewport.Invalidate();
        }

        private struct ProjectedTri
        {
            public PointF P0, P1, P2;
            public float Depth;
            public Color FillColor;
        }

        private void Viewport_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = _viewport.Width;
            int h = _viewport.Height;

            // Subtle vertical gradient background
            using (var bgBrush = new LinearGradientBrush(new Rectangle(0, 0, w, Math.Max(1, h)),
                Color.FromArgb(14, 18, 26), Color.FromArgb(8, 10, 15), LinearGradientMode.Vertical))
            {
                g.FillRectangle(bgBrush, 0, 0, w, h);
            }

            // Header overlay inside 3D Viewport
            using var titleFont = new Font("Segoe UI", 10.0f, FontStyle.Bold);
            using var subFont = new Font("Consolas", 8.5f);
            using var cyanBrush = new SolidBrush(Color.FromArgb(56, 189, 248));
            using var mutedBrush = new SolidBrush(Color.FromArgb(148, 163, 184));

            string clsHeader = _selectedClass != null
                ? $"3D МАНЕКЕН: {_selectedClass.Name} [{_selectedClass.Id}] (ЛКМ: Вращение | ПКМ: Панорама | Колесо: Зум)"
                : "3D МАНЕКЕН КЛАССА";
            g.DrawString(clsHeader, titleFont, cyanBrush, 12, 10);

            if (_selectedClass == null) return;

            // Gather active preview items for Head, Cape, Body, Gloves, Leg, and Weapons
            var activeSlots = new (SlotCategory Cat, string SlotLabel, Color Tint)[]
            {
                (SlotCategory.HeadArmor, "Head (Шлем)", Color.FromArgb(125, 211, 252)),
                (SlotCategory.Cape,      "Cape (Наплеч.)", Color.FromArgb(216, 180, 254)),
                (SlotCategory.BodyArmor, "Body (Корпус)", Color.FromArgb(110, 231, 183)),
                (SlotCategory.Gloves,    "Gloves (Перч.)", Color.FromArgb(253, 224, 71)),
                (SlotCategory.Legs,      "Leg (Поножи)", Color.FromArgb(249, 168, 212)),
                (SlotCategory.Weapon0,   "Weapon0 (Оруж.0)", Color.FromArgb(252, 165, 165)),
                (SlotCategory.Weapon1,   "Weapon1 (Щит/Оруж.1)", Color.FromArgb(253, 186, 116))
            };

            // Camera rotation math (warrior stands from Z = 0 cm to Z = 185 cm, center at Z = 95 cm)
            float yawRad = _camYaw * MathF.PI / 180f;
            float pitchRad = _camPitch * MathF.PI / 180f;
            float cosY = MathF.Cos(yawRad), sinY = MathF.Sin(yawRad);
            float cosP = MathF.Cos(pitchRad), sinP = MathF.Sin(pitchRad);

            float scale = _camZoom;
            float cx = w * 0.34f + _camPanX;
            float cy = h * 0.56f + _camPanY;

            PointF ProjectPoint(float xCm, float yCm, float zCm, out float depth)
            {
                float cz = zCm - 94.0f; // Center around warrior torso
                float rx = xCm * cosY - yCm * sinY;
                float ry = xCm * sinY + yCm * cosY;
                float rz = cz;

                float ry2 = ry * cosP - rz * sinP;
                float rz2 = ry * sinP + rz * cosP;

                depth = ry2;
                return new PointF(cx + rx * scale, cy - rz2 * scale);
            }

            // Draw floor pedestal grid at Z = 0 cm
            using var gridPen = new Pen(Color.FromArgb(40, 56, 189, 248), 1f);
            for (int gx = -60; gx <= 60; gx += 20)
            {
                var p1 = ProjectPoint(gx, -60, 0, out _);
                var p2 = ProjectPoint(gx, 60, 0, out _);
                g.DrawLine(gridPen, p1, p2);

                var p3 = ProjectPoint(-60, gx, 0, out _);
                var p4 = ProjectPoint(60, gx, 0, out _);
                g.DrawLine(gridPen, p3, p4);
            }

            var allTris = new List<ProjectedTri>(24000);
            var equippedLegend = new List<(string Text, Color Color)>();
            int totalVerts = 0, totalTris = 0, totalArmorSum = 0;

            foreach (var slot in activeSlots)
            {
                string itemId = _previewSelectedItems.TryGetValue(slot.Cat, out var sel) && !string.IsNullOrEmpty(sel)
                    ? sel
                    : (_selectedClass.Pools[slot.Cat].FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? "");

                if (string.IsNullOrEmpty(itemId)) continue;
                if (!_catalog.TryGetValue(itemId, out var catItem)) continue;

                totalArmorSum += catItem.ArmorValue;

                Mesh3DData? mesh = null;
                if (!string.IsNullOrEmpty(catItem.MeshName))
                {
                    _meshIndex.TryGetValue(catItem.MeshName, out mesh);
                }
                else if (catItem.CraftedPieces.Count > 0)
                {
                    // For crafted weapons, grab the blade or first available piece mesh for preview
                    foreach (var cp in catItem.CraftedPieces)
                    {
                        if (_meshIndex.TryGetValue(cp.PieceId, out mesh))
                            break;
                    }
                }

                if (mesh == null)
                {
                    equippedLegend.Add(($"{slot.SlotLabel}: {catItem.Id} (без 3D-меша)", Color.FromArgb(148, 163, 184)));
                    continue;
                }

                equippedLegend.Add(($"{slot.SlotLabel}: {catItem.Id} [{mesh.VerticesCm.Length} v]", slot.Tint));
                totalVerts += mesh.VerticesCm.Length;
                totalTris += mesh.Triangles.Length;

                // Determine placement offset for held weapons/shields vs body armor
                // Armor pieces in DWDN_TRY are already in human standing coordinates (Z = 0..185 cm)
                float offsetX = 0f, offsetY = 0f, offsetZ = 0f;
                bool isWeaponOrShield = slot.Cat == SlotCategory.Weapon0 || slot.Cat == SlotCategory.Weapon1;
                if (isWeaponOrShield)
                {
                    if (slot.Cat == SlotCategory.Weapon0)
                    {
                        offsetX = -38.0f;
                        offsetY = -10.0f;
                        offsetZ = 88.0f;
                    }
                    else
                    {
                        offsetX = 35.0f;
                        offsetY = -12.0f;
                        offsetZ = 95.0f;
                    }
                }

                int stride = mesh.Triangles.Length > 12000 ? 2 : 1;
                Color baseCol = slot.Tint;

                for (int t = 0; t < mesh.Triangles.Length; t += stride)
                {
                    var tri = mesh.Triangles[t];
                    if (tri.A >= mesh.VerticesCm.Length || tri.B >= mesh.VerticesCm.Length || tri.C >= mesh.VerticesCm.Length)
                        continue;

                    var v0 = mesh.VerticesCm[tri.A];
                    var v1 = mesh.VerticesCm[tri.B];
                    var v2 = mesh.VerticesCm[tri.C];

                    float x0 = v0.X + offsetX, y0 = v0.Y + offsetY, z0 = v0.Z + offsetZ;
                    float x1 = v1.X + offsetX, y1 = v1.Y + offsetY, z1 = v1.Z + offsetZ;
                    float x2 = v2.X + offsetX, y2 = v2.Y + offsetY, z2 = v2.Z + offsetZ;

                    // Compute normal for directional lighting
                    float ux = x1 - x0, uy = y1 - y0, uz = z1 - z0;
                    float vx = x2 - x0, vy = y2 - y0, vz = z2 - z0;
                    float nx = uy * vz - uz * vy;
                    float ny = uz * vx - ux * vz;
                    float nz = ux * vy - uy * vx;
                    float nLen = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (nLen < 1e-5f) continue;
                    nx /= nLen; ny /= nLen; nz /= nLen;

                    float light = MathF.Abs(nx * 0.45f - ny * 0.72f + nz * 0.52f);
                    float shade = Math.Clamp(0.32f + 0.68f * light, 0.25f, 1.0f);

                    var p0 = ProjectPoint(x0, y0, z0, out float d0);
                    var p1 = ProjectPoint(x1, y1, z1, out float d1);
                    var p2 = ProjectPoint(x2, y2, z2, out float d2);

                    Color shaded = Color.FromArgb(
                        235,
                        (int)(baseCol.R * shade),
                        (int)(baseCol.G * shade),
                        (int)(baseCol.B * shade));

                    allTris.Add(new ProjectedTri
                    {
                        P0 = p0,
                        P1 = p1,
                        P2 = p2,
                        Depth = (d0 + d1 + d2) / 3.0f,
                        FillColor = shaded
                    });
                }

                if (_showArmorBounds)
                {
                    float minZ = mesh.MinZCm + offsetZ;
                    float maxZ = mesh.MaxZCm + offsetZ;
                    var bp1 = ProjectPoint(-28, 0, minZ, out _);
                    var bp2 = ProjectPoint(28, 0, maxZ, out _);
                    using var boundPen = new Pen(Color.FromArgb(90, slot.Tint), 1f) { DashStyle = DashStyle.Dot };
                    float rx = Math.Min(bp1.X, bp2.X);
                    float ry = Math.Min(bp1.Y, bp2.Y);
                    float rw = Math.Abs(bp2.X - bp1.X);
                    float rh = Math.Abs(bp2.Y - bp1.Y);
                    if (rw > 4 && rh > 4)
                        g.DrawRectangle(boundPen, rx, ry, rw, rh);
                }
            }

            // Painter's algorithm sort (back to front)
            allTris.Sort((a, b) => b.Depth.CompareTo(a.Depth));

            using var wirePen = new Pen(Color.FromArgb(65, 255, 255, 255), 0.5f);
            PointF[] pts = new PointF[3];
            foreach (var tri in allTris)
            {
                pts[0] = tri.P0;
                pts[1] = tri.P1;
                pts[2] = tri.P2;
                if (_showWireframe)
                {
                    g.DrawPolygon(wirePen, pts);
                }
                else
                {
                    using var br = new SolidBrush(tri.FillColor);
                    g.FillPolygon(br, pts);
                }
            }

            // Draw Height Ruler on the right side of the warrior (0 cm to 180 cm)
            using var rulerPen = new Pen(Color.FromArgb(110, 148, 163, 184), 1f);
            var rBot = ProjectPoint(48, 0, 0, out _);
            var rTop = ProjectPoint(48, 0, 180, out _);
            g.DrawLine(rulerPen, rBot, rTop);
            for (int cm = 0; cm <= 180; cm += 30)
            {
                var rp = ProjectPoint(48, 0, cm, out _);
                g.DrawLine(rulerPen, rp.X - 4, rp.Y, rp.X + 4, rp.Y);
                g.DrawString($"{cm} см", subFont, mutedBrush, rp.X + 7, rp.Y - 7);
            }

            // Draw Legend of currently rendered 3D armor pieces on the right of the viewport
            int legX = Math.Max(425, w - 340);
            int legY = 40;
            using var legBg = new SolidBrush(Color.FromArgb(195, 15, 19, 28));
            g.FillRectangle(legBg, legX - 8, legY - 4, 330, equippedLegend.Count * 19 + 26);
            g.DrawString("СЛОИ 3D-МАНЕКЕНА (клик меняет превью):", subFont, cyanBrush, legX, legY);
            legY += 19;
            foreach (var item in equippedLegend)
            {
                using var b = new SolidBrush(item.Color);
                g.DrawString("● " + item.Text, subFont, b, legX, legY);
                legY += 18;
            }

            _lblMannequinStats.Text =
                $"🛡️ Класс: {_selectedClass.Name} | Суммарная броня сета: {totalArmorSum} (В классе: {_selectedClass.Armor}) | " +
                $"Цена: {_selectedClass.Cost} | HP: {_selectedClass.Hitpoints} | 3D Полигонов в сцене: {totalTris:N0} ({totalVerts:N0} вершин)";
        }

        // ==================== UI HELPER BUILDERS ====================

        private static Label CreateSectionHeader(string text, ref int y)
        {
            var lbl = new Label
            {
                Text = text,
                Location = new Point(8, y),
                Size = new Size(395, 20),
                Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248),
                BackColor = Color.FromArgb(26, 32, 44),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0)
            };
            y += 24;
            return lbl;
        }

        private static Button CreateFlatButton(string text, Color backColor, Point loc, Size size)
        {
            var btn = new Button
            {
                Text = text,
                Location = loc,
                Size = size,
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private static TextBox AddLabeledTextBox(Control parent, string label, int x, int y, int labelW, int boxW)
        {
            var lbl = new Label
            {
                Text = label,
                Location = new Point(x, y + 3),
                Size = new Size(labelW, 20),
                ForeColor = Color.FromArgb(148, 163, 184)
            };
            var txt = new TextBox
            {
                Location = new Point(x + labelW, y),
                Size = new Size(boxW, 23),
                BackColor = Color.FromArgb(30, 35, 48),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            parent.Controls.Add(lbl);
            parent.Controls.Add(txt);
            return txt;
        }

        private static NumericUpDown AddLabeledNumeric(Control parent, string label, int x, int y, int labelW, int boxW, decimal min, decimal max, int decimals, decimal inc)
        {
            var lbl = new Label
            {
                Text = label,
                Location = new Point(x, y + 3),
                Size = new Size(labelW, 20),
                ForeColor = Color.FromArgb(148, 163, 184)
            };
            var num = new NumericUpDown
            {
                Location = new Point(x + labelW, y),
                Size = new Size(boxW, 23),
                Minimum = min,
                Maximum = max,
                DecimalPlaces = decimals,
                Increment = inc,
                BackColor = Color.FromArgb(30, 35, 48),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            parent.Controls.Add(lbl);
            parent.Controls.Add(num);
            return num;
        }
    }
}
