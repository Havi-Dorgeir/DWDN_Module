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
        private string _modRoot = "";
        private string _nativeRoot = "";
        private string _backupDir = "";

        private List<DuchyModel> _duchies = new();
        private Dictionary<string, GameItemEntry> _catalog = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, Mesh3DData> _meshIndex = new(StringComparer.OrdinalIgnoreCase);

        private DuchyModel? _selectedDuchy;
        private ClassModel? _selectedClass;
        private SlotModel? _selectedSlot;
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
        private CheckBox _chkClassEnabled = null!;
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

        // UI Controls - Center Panel (Dynamic Slot & Category Editor + 3D Mannequin Viewport)
        private ListBox _lstClassSlots = null!;
        private ComboBox _cmbSlotTarget = null!;
        private ComboBox _cmbSlotCategory = null!;
        private TextBox _txtSlotLabel = null!;
        private CheckBox _chkSlotVisible = null!;
        private CheckBox _chkSlotAllowEmpty = null!;
        private ListBox _lstAllowedPool = null!;
        private TextBox _txtCatalogSearch = null!;
        private CheckBox _chkOnlyCustomDwdn = null!;
        private CheckBox _chkFilterBySlotType = null!;
        private ListBox _lstCatalog = null!;
        private Label _lblSelectedCatalogInfo = null!;
        private BufferedViewportPanel _viewport = null!;
        private Label _lblMannequinStats = null!;

        // UI Controls - Right Panel (XML Preview, Validation & Safe Save)
        private TabControl _tabCode = null!;
        private RichTextBox _rtbLoadoutsXml = null!;
        private RichTextBox _rtbXml = null!;
        private Label _lblStatus = null!;

        private static readonly (SlotCategory Cat, string Label, Color Accent)[] SlotTabs = new[]
        {
            (SlotCategory.HeadArmor, "Head (Шлем)", Color.FromArgb(56, 189, 248)),
            (SlotCategory.Cape,      "Cape (Плащ)", Color.FromArgb(168, 85, 247)),
            (SlotCategory.BodyArmor, "Body (Доспех)", Color.FromArgb(52, 211, 153)),
            (SlotCategory.Gloves,    "Gloves (Перчатки)", Color.FromArgb(251, 191, 36)),
            (SlotCategory.Legs,      "Legs (Обувь)", Color.FromArgb(244, 114, 182)),
            (SlotCategory.Weapon0,   "Weapon0 (Слот 1)", Color.FromArgb(248, 113, 113)),
            (SlotCategory.Weapon1,   "Weapon1 (Слот 2)", Color.FromArgb(251, 146, 60)),
            (SlotCategory.Weapon2,   "Weapon2 (Слот 3)", Color.FromArgb(250, 204, 21)),
            (SlotCategory.Weapon3,   "Weapon3 (Слот 4)", Color.FromArgb(163, 230, 53)),
            (SlotCategory.Mount,     "Horse (Скакун)", Color.FromArgb(96, 165, 250))
        };

        private void ResolvePaths()
        {
            _modRoot = StudioDataLoader.ResolveModRoot();
            _nativeRoot = StudioDataLoader.ResolveNativeRoot(_modRoot);
            _backupDir = Directory.Exists(@"d:\banAImod")
                ? @"d:\banAImod\backups\DuchyLoadoutStudio"
                : Path.Combine(Path.GetTempPath(), "DuchyLoadoutStudio_backups");
        }

        public StudioForm()
        {
            ResolvePaths();
            this.Text = "DWDN Duchy & Loadout Studio v2.0 — Конструктор слотов, категорий и снаряжения (XML)";
            this.Size = new Size(1680, 990);
            this.MinimumSize = new Size(1400, 860);
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

            var btnSaveXml = CreateFlatButton("💾 СОХРАНИТЬ dwdn_loadouts.xml", Color.FromArgb(22, 101, 52), Point.Empty, new Size(245, 31));
            btnSaveXml.Margin = new Padding(6, 0, 0, 0);
            btnSaveXml.Click += (s, e) => SaveLoadoutsXmlToMod();
            headerBtnBar.Controls.Add(btnSaveXml);

            var btnRollback = CreateFlatButton("↩️ Откатить бэкап", Color.FromArgb(180, 83, 9), Point.Empty, new Size(165, 31));
            btnRollback.Margin = new Padding(6, 0, 0, 0);
            btnRollback.Click += (s, e) => RollbackLoadoutsFromBackup();
            headerBtnBar.Controls.Add(btnRollback);

            var btnReload = CreateFlatButton("🔄 Перезагрузить из XML", Color.FromArgb(35, 48, 68), Point.Empty, new Size(195, 31));
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

            _txtClassName = AddLabeledTextBox(parent, "Имя:", 10, y, 68, 220);
            _chkClassEnabled = new CheckBox
            {
                Text = "Включён",
                Location = new Point(308, y + 2),
                Size = new Size(95, 22),
                Checked = true,
                ForeColor = Color.FromArgb(74, 222, 128)
            };
            parent.Controls.Add(_chkClassEnabled);
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
                _selectedClass.IsEnabled = _chkClassEnabled.Checked;
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
            _chkClassEnabled.CheckedChanged += classPropsChanged;
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
            // Split Center into Top (Dynamic Slots & Categories Editor) and Bottom (3D Warrior Mannequin Viewport)
            var topSlotsPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 395,
                BackColor = Color.FromArgb(20, 23, 31),
                Padding = new Padding(8)
            };

            int y = 6;
            topSlotsPanel.Controls.Add(CreateSectionHeader("4. СЛОТЫ, КАТЕГОРИИ И ПУЛЫ СНАРЯЖЕНИЯ КЛАССА (dwdn_loadouts.xml)", ref y));

            // ---- Column A (Left): Dynamic List of Class Slots & Categories ----
            var lblSlots = new Label
            {
                Text = "Слоты класса (порядок в UI):",
                Location = new Point(8, y),
                Size = new Size(225, 18),
                ForeColor = Color.FromArgb(250, 204, 21),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            topSlotsPanel.Controls.Add(lblSlots);

            _lstClassSlots = new ListBox
            {
                Location = new Point(8, y + 20),
                Size = new Size(225, 290),
                BackColor = Color.FromArgb(13, 16, 22),
                ForeColor = Color.FromArgb(240, 246, 252),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.4f)
            };
            _lstClassSlots.SelectedIndexChanged += (s, e) =>
            {
                if (_suppressEvents || _selectedClass == null) return;
                if (_lstClassSlots.SelectedIndex >= 0 && _lstClassSlots.SelectedIndex < _selectedClass.Slots.Count)
                    SelectClassSlot(_selectedClass.Slots[_lstClassSlots.SelectedIndex]);
            };
            topSlotsPanel.Controls.Add(_lstClassSlots);

            int slotBtnY = y + 314;
            var btnAddSlot = CreateFlatButton("➕ Слот", Color.FromArgb(22, 101, 52), new Point(8, slotBtnY), new Size(70, 27));
            btnAddSlot.Click += (s, e) => AddNewSlotToClass();
            topSlotsPanel.Controls.Add(btnAddSlot);

            var btnDelSlot = CreateFlatButton("🗑️ Удал.", Color.FromArgb(153, 27, 27), new Point(82, slotBtnY), new Size(68, 27));
            btnDelSlot.Click += (s, e) => RemoveSelectedSlotFromClass();
            topSlotsPanel.Controls.Add(btnDelSlot);

            var btnSlotUp = CreateFlatButton("▲", Color.FromArgb(45, 55, 72), new Point(154, slotBtnY), new Size(37, 27));
            btnSlotUp.Click += (s, e) => MoveSelectedSlot(-1);
            topSlotsPanel.Controls.Add(btnSlotUp);

            var btnSlotDown = CreateFlatButton("▼", Color.FromArgb(45, 55, 72), new Point(195, slotBtnY), new Size(38, 27));
            btnSlotDown.Click += (s, e) => MoveSelectedSlot(1);
            topSlotsPanel.Controls.Add(btnSlotDown);

            // ---- Top-Right Properties Strip for the Selected Slot ----
            int propX = 240;
            var slotPropBox = new Panel
            {
                Location = new Point(propX, y),
                Size = new Size(478, 62),
                BackColor = Color.FromArgb(25, 30, 42),
                BorderStyle = BorderStyle.FixedSingle
            };

            slotPropBox.Controls.Add(new Label { Text = "Слот движка:", Location = new Point(6, 7), Size = new Size(80, 18), ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Segoe UI", 8.1f) });
            _cmbSlotTarget = new ComboBox
            {
                Location = new Point(86, 4),
                Size = new Size(126, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(16, 20, 28),
                ForeColor = Color.White
            };
            foreach (var tab in SlotTabs) _cmbSlotTarget.Items.Add(tab.Label);
            slotPropBox.Controls.Add(_cmbSlotTarget);

            slotPropBox.Controls.Add(new Label { Text = "Категория:", Location = new Point(218, 7), Size = new Size(66, 18), ForeColor = Color.FromArgb(250, 204, 21), Font = new Font("Segoe UI", 8.1f, FontStyle.Bold) });
            _cmbSlotCategory = new ComboBox
            {
                Location = new Point(286, 4),
                Size = new Size(184, 23),
                DropDownStyle = ComboBoxStyle.DropDown, // Editable so user can create ANY new category!
                BackColor = Color.FromArgb(16, 20, 28),
                ForeColor = Color.White
            };
            slotPropBox.Controls.Add(_cmbSlotCategory);

            slotPropBox.Controls.Add(new Label { Text = "Название UI:", Location = new Point(6, 35), Size = new Size(80, 18), ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Segoe UI", 8.1f) });
            _txtSlotLabel = new TextBox
            {
                Location = new Point(86, 32),
                Size = new Size(190, 23),
                BackColor = Color.FromArgb(16, 20, 28),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            slotPropBox.Controls.Add(_txtSlotLabel);

            _chkSlotVisible = new CheckBox
            {
                Text = "Видим в UI",
                Location = new Point(286, 34),
                Size = new Size(86, 20),
                Checked = true,
                ForeColor = Color.FromArgb(74, 222, 128),
                Font = new Font("Segoe UI", 8.0f)
            };
            slotPropBox.Controls.Add(_chkSlotVisible);

            _chkSlotAllowEmpty = new CheckBox
            {
                Text = "Снять (\"\")",
                Location = new Point(376, 34),
                Size = new Size(94, 20),
                Checked = false,
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8.0f)
            };
            slotPropBox.Controls.Add(_chkSlotAllowEmpty);

            EventHandler slotPropChanged = (s, e) =>
            {
                if (_suppressEvents || _selectedClass == null || _selectedSlot == null) return;
                if (_cmbSlotTarget.SelectedIndex >= 0 && _cmbSlotTarget.SelectedIndex < SlotTabs.Length)
                {
                    _selectedSlot.Target = SlotTabs[_cmbSlotTarget.SelectedIndex].Cat;
                    _activeSlotCategory = _selectedSlot.Target;
                }
                _selectedSlot.Category = _cmbSlotCategory.Text.Trim();
                _selectedSlot.Label = _txtSlotLabel.Text;
                _selectedSlot.Visible = _chkSlotVisible.Checked;
                _selectedSlot.AllowEmpty = _chkSlotAllowEmpty.Checked;
                if (_selectedSlot.AllowEmpty && !_selectedSlot.Items.Contains(""))
                    _selectedSlot.Items.Add("");
                else if (!_selectedSlot.AllowEmpty && _selectedSlot.Items.Contains(""))
                    _selectedSlot.Items.RemoveAll(x => string.IsNullOrEmpty(x));

                _selectedClass.SyncPoolsFromSlots();
                RefreshClassSlotsList(keepSelection: true);
                RefreshAllowedPoolList();
                RefreshCatalogList();
                RefreshCodePreview();
                _viewport?.Invalidate();
            };

            _cmbSlotTarget.SelectedIndexChanged += slotPropChanged;
            _cmbSlotCategory.TextChanged += slotPropChanged;
            _txtSlotLabel.TextChanged += slotPropChanged;
            _chkSlotVisible.CheckedChanged += slotPropChanged;
            _chkSlotAllowEmpty.CheckedChanged += slotPropChanged;

            topSlotsPanel.Controls.Add(slotPropBox);

            // ---- Column B (Middle): Allowed Items in Selected Slot ----
            int poolY = y + 68;
            var lblAllowed = new Label
            {
                Text = "Предметы слота (№1 = дефолт):",
                Location = new Point(propX, poolY),
                Size = new Size(240, 18),
                ForeColor = Color.FromArgb(74, 222, 128),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            topSlotsPanel.Controls.Add(lblAllowed);

            _lstAllowedPool = new ListBox
            {
                Location = new Point(propX, poolY + 20),
                Size = new Size(220, 222),
                BackColor = Color.FromArgb(13, 16, 22),
                ForeColor = Color.FromArgb(240, 246, 252),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.4f)
            };
            _lstAllowedPool.SelectedIndexChanged += (s, e) =>
            {
                if (_suppressEvents || _selectedClass == null || _selectedSlot == null) return;
                var pool = _selectedSlot.Items;
                if (_lstAllowedPool.SelectedIndex >= 0 && _lstAllowedPool.SelectedIndex < pool.Count)
                {
                    string itemId = pool[_lstAllowedPool.SelectedIndex];
                    _previewSelectedItems[_selectedSlot.Target] = itemId;
                    ShowCatalogItemDetails(itemId);
                    _viewport?.Invalidate();
                }
            };
            topSlotsPanel.Controls.Add(_lstAllowedPool);

            var btnMakeDefault = CreateFlatButton("⭐ №1", Color.FromArgb(30, 64, 175), new Point(propX, slotBtnY), new Size(68, 27));
            btnMakeDefault.Click += (s, e) => MakeAllowedItemDefault();
            topSlotsPanel.Controls.Add(btnMakeDefault);

            var btnAddEmpty = CreateFlatButton("➕ \"\"", Color.FromArgb(55, 65, 81), new Point(propX + 72, slotBtnY), new Size(66, 27));
            btnAddEmpty.Click += (s, e) => AddItemToClassPool("");
            topSlotsPanel.Controls.Add(btnAddEmpty);

            var btnRemovePool = CreateFlatButton("🗑️ Убрать", Color.FromArgb(153, 27, 27), new Point(propX + 142, slotBtnY), new Size(78, 27));
            btnRemovePool.Click += (s, e) => RemoveSelectedFromClassPool();
            topSlotsPanel.Controls.Add(btnRemovePool);

            // ---- Column C (Right): Master Catalog Search & Add ----
            int catX = propX + 228;
            var lblCatalog = new Label
            {
                Text = "Каталог (DWDN+Native):",
                Location = new Point(catX, poolY),
                Size = new Size(150, 18),
                ForeColor = Color.FromArgb(56, 189, 248),
                Font = new Font("Segoe UI", 8.2f, FontStyle.Bold)
            };
            topSlotsPanel.Controls.Add(lblCatalog);

            _chkOnlyCustomDwdn = new CheckBox
            {
                Text = "Только DWDN",
                Location = new Point(catX + 150, poolY - 1),
                Size = new Size(100, 20),
                Checked = false,
                ForeColor = Color.FromArgb(250, 204, 21),
                Font = new Font("Segoe UI", 7.8f)
            };
            _chkOnlyCustomDwdn.CheckedChanged += (s, e) => RefreshCatalogList();
            topSlotsPanel.Controls.Add(_chkOnlyCustomDwdn);

            _txtCatalogSearch = new TextBox
            {
                Location = new Point(catX, poolY + 20),
                Size = new Size(156, 23),
                BackColor = Color.FromArgb(28, 33, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "🔍 Поиск ID/имени..."
            };
            _txtCatalogSearch.TextChanged += (s, e) => RefreshCatalogList();
            topSlotsPanel.Controls.Add(_txtCatalogSearch);

            var btnAddCatalogItem = CreateFlatButton("⬅️ Добавить", Color.FromArgb(22, 101, 52), new Point(catX + 160, poolY + 19), new Size(90, 25));
            btnAddCatalogItem.Click += (s, e) => AddSelectedCatalogItemToClass();
            topSlotsPanel.Controls.Add(btnAddCatalogItem);

            _chkFilterBySlotType = new CheckBox
            {
                Text = "Безопасный фильтр по типу слота",
                Location = new Point(catX, poolY + 46),
                Size = new Size(250, 19),
                Checked = true,
                ForeColor = Color.FromArgb(74, 222, 128),
                Font = new Font("Segoe UI", 8.0f)
            };
            _chkFilterBySlotType.CheckedChanged += (s, e) => RefreshCatalogList();
            topSlotsPanel.Controls.Add(_chkFilterBySlotType);

            _lstCatalog = new ListBox
            {
                Location = new Point(catX, poolY + 67),
                Size = new Size(250, 175),
                BackColor = Color.FromArgb(13, 16, 22),
                ForeColor = Color.FromArgb(203, 213, 225),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.3f)
            };
            _lstCatalog.DoubleClick += (s, e) => AddSelectedCatalogItemToClass();
            _lstCatalog.SelectedIndexChanged += (s, e) =>
            {
                if (_lstCatalog.SelectedItem is CatalogListEntry entry)
                {
                    ShowCatalogItemDetails(entry.Item.Id);
                    _previewSelectedItems[_activeSlotCategory] = entry.Item.Id;
                    _viewport?.Invalidate();
                }
            };
            topSlotsPanel.Controls.Add(_lstCatalog);

            _lblSelectedCatalogInfo = new Label
            {
                Location = new Point(catX, slotBtnY + 3),
                Size = new Size(250, 24),
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Consolas", 7.8f),
                Text = "Кликните предмет для 3D-превью..."
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
            parent.Controls.Add(CreateSectionHeader("5. СОХРАНЕНИЕ XML, ВАЛИДАЦИЯ И БЭКАПЫ", ref y));

            var infoLbl = new Label
            {
                Text = "Все слоты, категории и предметы хранятся в ModuleData/dwdn_loadouts.xml.\nПересборка DLL не требуется! Перед каждым сохранением создаётся бэкап.",
                Location = new Point(10, y),
                Size = new Size(428, 36),
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8.3f)
            };
            parent.Controls.Add(infoLbl);
            y += 40;

            var btnSaveXmlRight = CreateFlatButton("💾 СОХРАНИТЬ dwdn_loadouts.xml (Мгновенно, с бэкапом)", Color.FromArgb(22, 101, 52), new Point(10, y), new Size(428, 34));
            btnSaveXmlRight.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnSaveXmlRight.Click += (s, e) => SaveLoadoutsXmlToMod();
            parent.Controls.Add(btnSaveXmlRight);
            y += 39;

            var btnRollbackRight = CreateFlatButton("↩️ Откатить бэкап", Color.FromArgb(180, 83, 9), new Point(10, y), new Size(155, 27));
            btnRollbackRight.Click += (s, e) => RollbackLoadoutsFromBackup();
            parent.Controls.Add(btnRollbackRight);

            var btnSyncStats = CreateFlatButton("⚙️ Обновить статы (mpcharacters/divisions)", Color.FromArgb(37, 99, 235), new Point(170, y), new Size(268, 27));
            btnSyncStats.Click += (s, e) => SyncStatsToGameXmls();
            parent.Controls.Add(btnSyncStats);
            y += 33;

            _tabCode = new TabControl
            {
                Location = new Point(10, y),
                Size = new Size(428, 725),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var tabLoadouts = new TabPage("1. dwdn_loadouts.xml");
            _rtbLoadoutsXml = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(12, 14, 20),
                ForeColor = Color.FromArgb(134, 239, 172),
                Font = new Font("Consolas", 8.4f),
                ReadOnly = true,
                WordWrap = false
            };
            tabLoadouts.Controls.Add(_rtbLoadoutsXml);

            var tabXml = new TabPage("2. Валидация + XML статов");
            _rtbXml = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(12, 14, 20),
                ForeColor = Color.FromArgb(125, 211, 252),
                Font = new Font("Consolas", 8.4f),
                ReadOnly = true,
                WordWrap = false
            };
            tabXml.Controls.Add(_rtbXml);

            _tabCode.TabPages.Add(tabLoadouts);
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

                RefreshCategoryDropdown();
                _lblStatus.Text = $"✅ Загружено из XML: герцогств {_duchies.Count} | классов {_duchies.Sum(d => d.Classes.Count)} | каталог {_catalog.Count} | 3D-мешей {_meshIndex.Count}";
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
                string stateTag = c.IsEnabled ? "" : "[ВЫКЛ] ";
                _lstClasses.Items.Add($"{iconShort} {stateTag}{c.Name,-18} [{c.Id}] ({c.Slots.Count} сл.)");
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
            _chkClassEnabled.Checked = cls.IsEnabled;
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

            cls.SyncPoolsFromSlots();
            RefreshClassSlotsList(keepSelection: false);
            if (cls.Slots.Count > 0)
            {
                _lstClassSlots.SelectedIndex = 0;
                SelectClassSlot(cls.Slots[0]);
            }
            else
            {
                _selectedSlot = null;
                RefreshAllowedPoolList();
                RefreshCatalogList();
            }
            RefreshCodePreview();
            _viewport?.Invalidate();
        }

        private void RefreshCategoryDropdown()
        {
            string current = _cmbSlotCategory.Text;
            var cats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Броня", "Вооружение", "Артиллерия", "Спецсредства", "Верховая езда"
            };
            foreach (var d in _duchies)
                foreach (var c in d.Classes)
                    foreach (var s in c.Slots)
                        if (!string.IsNullOrWhiteSpace(s.Category))
                            cats.Add(s.Category.Trim());

            bool prevSuppress = _suppressEvents;
            _suppressEvents = true;
            _cmbSlotCategory.Items.Clear();
            foreach (var cat in cats) _cmbSlotCategory.Items.Add(cat);
            _cmbSlotCategory.Text = current;
            _suppressEvents = prevSuppress;
        }

        private void RefreshClassSlotsList(bool keepSelection)
        {
            int prevIdx = _lstClassSlots.SelectedIndex;
            bool prevSuppress = _suppressEvents;
            _suppressEvents = true;
            _lstClassSlots.Items.Clear();
            if (_selectedClass != null)
            {
                foreach (var s in _selectedClass.Slots)
                {
                    string vis = s.Visible ? "" : "[скрыт] ";
                    string targetShort = SlotModel.TargetToXmlName(s.Target);
                    _lstClassSlots.Items.Add($"{vis}[{s.Category}] {s.Label} ({targetShort}) • {s.Items.Count}");
                }
            }
            if (keepSelection && prevIdx >= 0 && prevIdx < _lstClassSlots.Items.Count)
                _lstClassSlots.SelectedIndex = prevIdx;
            _suppressEvents = prevSuppress;
        }

        private void SelectClassSlot(SlotModel slot)
        {
            _selectedSlot = slot;
            _activeSlotCategory = slot.Target;

            _suppressEvents = true;
            int targetIdx = Array.FindIndex(SlotTabs, t => t.Cat == slot.Target);
            if (targetIdx >= 0) _cmbSlotTarget.SelectedIndex = targetIdx;
            RefreshCategoryDropdown();
            _cmbSlotCategory.Text = slot.Category;
            _txtSlotLabel.Text = slot.Label;
            _chkSlotVisible.Checked = slot.Visible;
            _chkSlotAllowEmpty.Checked = slot.AllowEmpty || slot.Items.Contains("");
            _suppressEvents = false;

            RefreshAllowedPoolList();
            RefreshCatalogList();
        }

        private void AddNewSlotToClass()
        {
            if (_selectedClass == null) return;

            // Pick first unused physical target if available
            var used = new HashSet<SlotCategory>(_selectedClass.Slots.Select(s => s.Target));
            SlotCategory nextTarget = SlotCategory.Weapon0;
            foreach (SlotCategory cat in Enum.GetValues(typeof(SlotCategory)))
            {
                if (!used.Contains(cat)) { nextTarget = cat; break; }
            }

            var newSlot = new SlotModel
            {
                Target = nextTarget,
                Category = SlotModel.DefaultCategoryFor(nextTarget),
                Label = SlotModel.DefaultLabelFor(nextTarget),
                Visible = true,
                AllowEmpty = true
            };
            _selectedClass.Slots.Add(newSlot);
            _selectedClass.SyncPoolsFromSlots();
            RefreshClassSlotsList(keepSelection: false);
            _lstClassSlots.SelectedIndex = _selectedClass.Slots.Count - 1;
            SelectClassSlot(newSlot);
            UpdateClassListDisplay();
            RefreshCodePreview();
        }

        private void RemoveSelectedSlotFromClass()
        {
            if (_selectedClass == null || _selectedSlot == null) return;
            int idx = _selectedClass.Slots.IndexOf(_selectedSlot);
            if (idx < 0) return;

            _selectedClass.Slots.RemoveAt(idx);
            _selectedClass.SyncPoolsFromSlots();
            RefreshClassSlotsList(keepSelection: false);
            if (_selectedClass.Slots.Count > 0)
            {
                int nextIdx = Math.Clamp(idx, 0, _selectedClass.Slots.Count - 1);
                _lstClassSlots.SelectedIndex = nextIdx;
                SelectClassSlot(_selectedClass.Slots[nextIdx]);
            }
            else
            {
                _selectedSlot = null;
                RefreshAllowedPoolList();
            }
            UpdateClassListDisplay();
            RefreshCodePreview();
            _viewport?.Invalidate();
        }

        private void MoveSelectedSlot(int delta)
        {
            if (_selectedClass == null || _selectedSlot == null) return;
            int idx = _selectedClass.Slots.IndexOf(_selectedSlot);
            int target = idx + delta;
            if (idx >= 0 && target >= 0 && target < _selectedClass.Slots.Count)
            {
                var s = _selectedClass.Slots[idx];
                _selectedClass.Slots.RemoveAt(idx);
                _selectedClass.Slots.Insert(target, s);
                RefreshClassSlotsList(keepSelection: false);
                _lstClassSlots.SelectedIndex = target;
                RefreshCodePreview();
            }
        }

        private void RefreshAllowedPoolList()
        {
            _suppressEvents = true;
            _lstAllowedPool.Items.Clear();
            if (_selectedClass != null && _selectedSlot != null)
            {
                var pool = _selectedSlot.Items;
                for (int i = 0; i < pool.Count; i++)
                {
                    string id = pool[i];
                    string prefix = i == 0 ? "⭐ " : $"[{i + 1}] ";
                    if (string.IsNullOrEmpty(id))
                    {
                        _lstAllowedPool.Items.Add($"{prefix}\"\" (Пустой слот)");
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
            RefreshClassSlotsList(keepSelection: true);
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
            bool filterType = _chkFilterBySlotType.Checked;

            _lstCatalog.BeginUpdate();
            _lstCatalog.Items.Clear();

            var matches = _catalog.Values
                .Where(it => !string.IsNullOrEmpty(it.Id))
                .Where(it => !filterType || MatchesCategory(it, _activeSlotCategory))
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
            if (_selectedClass == null || _selectedSlot == null) return;
            var pool = _selectedSlot.Items;
            if (!pool.Contains(itemId))
            {
                pool.Add(itemId);
                if (string.IsNullOrEmpty(itemId))
                {
                    _selectedSlot.AllowEmpty = true;
                    _suppressEvents = true;
                    _chkSlotAllowEmpty.Checked = true;
                    _suppressEvents = false;
                }
                else
                {
                    _previewSelectedItems[_selectedSlot.Target] = itemId;
                }
                _selectedClass.SyncPoolsFromSlots();
                RefreshAllowedPoolList();
                RefreshCodePreview();
                _viewport.Invalidate();
            }
        }

        private void RemoveSelectedFromClassPool()
        {
            if (_selectedClass == null || _selectedSlot == null) return;
            var pool = _selectedSlot.Items;
            int idx = _lstAllowedPool.SelectedIndex;
            if (idx >= 0 && idx < pool.Count)
            {
                string removed = pool[idx];
                pool.RemoveAt(idx);
                if (string.IsNullOrEmpty(removed))
                {
                    _selectedSlot.AllowEmpty = false;
                    _suppressEvents = true;
                    _chkSlotAllowEmpty.Checked = false;
                    _suppressEvents = false;
                }
                _selectedClass.SyncPoolsFromSlots();
                _previewSelectedItems[_selectedSlot.Target] = pool.FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? "";
                RefreshAllowedPoolList();
                RefreshCodePreview();
                _viewport.Invalidate();
            }
        }

        private void MakeAllowedItemDefault()
        {
            if (_selectedClass == null || _selectedSlot == null) return;
            var pool = _selectedSlot.Items;
            int idx = _lstAllowedPool.SelectedIndex;
            if (idx > 0 && idx < pool.Count)
            {
                string item = pool[idx];
                pool.RemoveAt(idx);
                pool.Insert(0, item);
                _selectedClass.SyncPoolsFromSlots();
                if (!string.IsNullOrEmpty(item))
                    _previewSelectedItems[_selectedSlot.Target] = item;
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
            cls.Slots.Add(new SlotModel { Target = SlotCategory.HeadArmor, Category = "Броня", Label = "Шлем", AllowEmpty = true, Items = new() { "Banobranny_helmet", "" } });
            cls.Slots.Add(new SlotModel { Target = SlotCategory.BodyArmor, Category = "Броня", Label = "Доспех", Items = new() { "Banobranny_surcoat" } });
            cls.Slots.Add(new SlotModel { Target = SlotCategory.Legs, Category = "Броня", Label = "Обувь", Items = new() { "banobranyi_leg" } });
            cls.Slots.Add(new SlotModel { Target = SlotCategory.Gloves, Category = "Броня", Label = "Перчатки", AllowEmpty = true, Items = new() { "crossbowman_mailgoves", "" } });
            cls.Slots.Add(new SlotModel { Target = SlotCategory.Weapon0, Category = "Вооружение", Label = "Оружие", Items = new() { "dwdn_falchion" } });
            cls.Slots.Add(new SlotModel { Target = SlotCategory.Weapon1, Category = "Вооружение", Label = "Щит", AllowEmpty = true, Items = new() { "bannpbran_shield1", "" } });
            cls.SyncPoolsFromSlots();

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
            _rtbLoadoutsXml.Text = StudioDataLoader.GenerateLoadoutsXml(_duchies);

            var issues = StudioDataLoader.ValidateLoadouts(_duchies, _catalog);
            var sb = new System.Text.StringBuilder();
            if (issues.Count == 0)
            {
                sb.AppendLine("<!-- ✅ ВАЛИДАЦИЯ ПРОЙДЕНА: 0 ошибок, 0 предупреждений -->");
            }
            else
            {
                int errs = issues.Count(i => i.IsError);
                int warns = issues.Count - errs;
                sb.AppendLine($"<!-- Валидация: {errs} ошибок, {warns} предупреждений -->");
                foreach (var iss in issues)
                {
                    sb.AppendLine($"<!-- {(iss.IsError ? "[ОШИБКА]" : "[ПРЕДУПР.]")} {iss.Message} -->");
                }
            }
            sb.AppendLine();

            if (_selectedClass != null)
            {
                _selectedClass.SyncPoolsFromSlots();
                sb.Append(StudioDataLoader.GenerateClassXmlSnippets(_selectedClass));
            }
            _rtbXml.Text = sb.ToString();
        }

        private void SaveLoadoutsXmlToMod()
        {
            var issues = StudioDataLoader.ValidateLoadouts(_duchies, _catalog);
            var errors = issues.Where(i => i.IsError).ToList();
            if (errors.Count > 0)
            {
                string errText = string.Join("\n• ", errors.Select(e => e.Message));
                MessageBox.Show(
                    $"Сохранение заблокировано из-за критических ошибок:\n\n• {errText}\n\nИсправьте их и повторите попытку.",
                    "❌ Ошибка валидации",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                _tabCode.SelectedIndex = 1;
                RefreshCodePreview();
                return;
            }

            try
            {
                string xmlPath = Path.Combine(_modRoot, "ModuleData", "dwdn_loadouts.xml");
                string backupFile = StudioDataLoader.SaveLoadoutsXmlAtomic(xmlPath, _backupDir, _duchies);

                string serverSyncNote = "";
                string? srvModRoot = StudioDataLoader.ResolveDedicatedServerModRoot(_modRoot);
                if (!string.IsNullOrEmpty(srvModRoot))
                {
                    string srvXml = Path.Combine(srvModRoot, "ModuleData", "dwdn_loadouts.xml");
                    Directory.CreateDirectory(Path.GetDirectoryName(srvXml)!);
                    File.Copy(xmlPath, srvXml, overwrite: true);
                    serverSyncNote = $"\n• Синхронизировано на сервер: {srvXml}";
                }

                RefreshCodePreview();
                _lblStatus.Text = $"✅ Сохранено в dwdn_loadouts.xml (без пересборки DLL)! Бэкап: {backupFile}";
                MessageBox.Show(
                    $"Конфигурация слотов, категорий и снаряжения сохранена!\n\n" +
                    $"• Файл: {xmlPath}{serverSyncNote}\n" +
                    $"• Бэкап: {(string.IsNullOrEmpty(backupFile) ? "(первое сохранение)" : backupFile)}\n\n" +
                    $"Перекомпиляция DLL не требуется — изменения заработают при следующем запуске игры/сервера.",
                    "💾 Сохранено (dwdn_loadouts.xml)",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения XML: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RollbackLoadoutsFromBackup()
        {
            string? latestBackup = StudioDataLoader.GetLatestBackupPath(_backupDir);
            if (string.IsNullOrEmpty(latestBackup))
            {
                MessageBox.Show($"Резервные копии в '{_backupDir}' пока не найдены.", "Откат бэкапа", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Восстановить ModuleData/dwdn_loadouts.xml из последней резервной копии?\n\n{latestBackup}",
                "↩️ Откат из резервной копии",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                string xmlPath = Path.Combine(_modRoot, "ModuleData", "dwdn_loadouts.xml");
                File.Copy(latestBackup, xmlPath, overwrite: true);

                string? srvModRoot = StudioDataLoader.ResolveDedicatedServerModRoot(_modRoot);
                if (!string.IsNullOrEmpty(srvModRoot))
                {
                    string srvXml = Path.Combine(srvModRoot, "ModuleData", "dwdn_loadouts.xml");
                    if (Directory.Exists(Path.GetDirectoryName(srvXml)))
                        File.Copy(xmlPath, srvXml, overwrite: true);
                }

                LoadModData();
                _lblStatus.Text = $"↩️ Восстановлено из бэкапа: {Path.GetFileName(latestBackup)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка отката: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SyncStatsToGameXmls()
        {
            var confirm = MessageBox.Show(
                "Обновить характеристики классов (цена, HP, скорость, навыки) в:\n" +
                "1. ModuleData/mpclassdivisions.xml\n" +
                "2. ModuleData/mpcharacters.xml\n\n" +
                "Перед записью будут созданы резервные копии .bak.",
                "⚙️ Синхронизация статов в XML",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                string backupSub = Path.Combine(_backupDir, "stats_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(backupSub);

                string divTarget = Path.Combine(_modRoot, "ModuleData", "mpclassdivisions.xml");
                string charTarget = Path.Combine(_modRoot, "ModuleData", "mpcharacters.xml");

                if (File.Exists(divTarget)) File.Copy(divTarget, Path.Combine(backupSub, "mpclassdivisions.xml.bak"), true);
                if (File.Exists(charTarget)) File.Copy(charTarget, Path.Combine(backupSub, "mpcharacters.xml.bak"), true);

                foreach (var c in _duchies.SelectMany(d => d.Classes)) c.SyncPoolsFromSlots();
                StudioDataLoader.PatchAndSaveMpClassDivisionsXml(divTarget, _duchies);
                StudioDataLoader.PatchAndSaveMpCharactersXml(charTarget, _duchies);

                _lblStatus.Text = $"⚙️ Обновлены mpclassdivisions.xml и mpcharacters.xml. Бэкап: {backupSub}";
                MessageBox.Show($"Готово!\nБэкап сохранён в:\n{backupSub}", "Статы обновлены", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления XML статов: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
