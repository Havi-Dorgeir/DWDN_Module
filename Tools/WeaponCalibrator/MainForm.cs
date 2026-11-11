using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace WeaponCalibrator;

public class DoubleBufferedPanel : Panel
{
    public DoubleBufferedPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
}

public class MainForm : Form
{
    private readonly string _modPath;
    private readonly string _outputDir;
    private readonly Dictionary<string, MeshGeometryInfo> _geometries;
    private readonly Dictionary<string, CraftingTemplateDef> _templates;
    private readonly Dictionary<string, CraftingPieceDef> _originalPieces;
    private Dictionary<string, CraftingPieceDef> _workingPieces;
    private readonly List<CraftedWeaponItemDef> _originalWeapons;
    private List<CraftedWeaponItemDef> _workingWeapons;

    private CraftedWeaponItemDef _currentWeapon;
    private WeaponSimulationResult _currentSim;
    private bool _suppressEvents;

    // UI Controls
    private ComboBox _cmbWeaponPreset;
    private ComboBox _cmbTemplate;
    private readonly Dictionary<PieceType, PartEditorRow> _partRows = new();
    private DoubleBufferedPanel _viewport;
    private TextBox _txtXmlOutput;
    private Label _lblDiagnostics;
    private CheckBox _chkShowWireframe;
    private CheckBox _chkShowBBox;
    private CheckBox _chkShowPivots;

    // 3D Camera State
    private float _camYaw = 0.35f;
    private float _camPitch = 0.20f;
    private float _camZoom = 3.2f;
    private float _camPanX = 0f;
    private float _camPanY = 70f; // Center around Z = +40..60 cm
    private Point _lastMouse;
    private bool _isOrbiting;
    private bool _isPanning;

    private class PartEditorRow
    {
        public PieceType PieceType;
        public ComboBox CmbPiece;
        public NumericUpDown NumScale;
        public NumericUpDown NumLength;
        public NumericUpDown NumPieceOffset;
        public NumericUpDown NumPrevOffset;
        public NumericUpDown NumNextOffset;
        public Label LblWorldBounds;
    }

    public MainForm(
        string modPath,
        string outputDir,
        Dictionary<string, MeshGeometryInfo> geometries,
        Dictionary<string, CraftingTemplateDef> templates,
        Dictionary<string, CraftingPieceDef> pieces,
        List<CraftedWeaponItemDef> weapons)
    {
        _modPath = modPath;
        _outputDir = outputDir;
        _geometries = geometries;
        _templates = templates;
        _originalPieces = pieces.ToDictionary(k => k.Key, v => v.Value.Clone(), StringComparer.OrdinalIgnoreCase);
        _workingPieces = pieces.ToDictionary(k => k.Key, v => v.Value.Clone(), StringComparer.OrdinalIgnoreCase);
        _originalWeapons = weapons.Where(w => w.Pieces.Values.Any(p => pieces.ContainsKey(p.PieceId))).Select(w => w.Clone()).ToList();
        _workingWeapons = _originalWeapons.Select(w => w.Clone()).ToList();

        Text = "⚙️ OMNISSIAH 3D WEAPON AUTO-CALIBRATOR STUDIO — Mount & Blade II: Bannerlord (DWDN)";
        Size = new Size(1560, 920);
        MinimumSize = new Size(1280, 760);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(13, 17, 23);
        ForeColor = Color.FromArgb(201, 209, 217);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        BuildLayout();
        LoadInitialWeapon();
    }

    private void BuildLayout()
    {
        // Left Sidebar: Controls & Part Sliders (500 px)
        var leftPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 510,
            BackColor = Color.FromArgb(22, 27, 34),
            Padding = new Padding(12),
            AutoScroll = true
        };

        // Right/Bottom Panel: XML Output & Actions (400 px on right)
        var rightPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 420,
            BackColor = Color.FromArgb(22, 27, 34),
            Padding = new Padding(12)
        };

        // Center Panel: 3D Interactive Viewport + Seam Status Header
        var centerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(13, 17, 23),
            Padding = new Padding(8)
        };

        BuildLeftPanel(leftPanel);
        BuildRightPanel(rightPanel);
        BuildCenterViewport(centerPanel);

        Controls.Add(centerPanel);
        Controls.Add(rightPanel);
        Controls.Add(leftPanel);
    }

    private void BuildLeftPanel(Panel parent)
    {
        int y = 10;

        var lblTitle = new Label
        {
            Text = "🛠️ КОНСТРУКТОР И КАЛИБРОВКА ОРУЖИЯ",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(88, 166, 255),
            Location = new Point(12, y),
            AutoSize = true
        };
        parent.Controls.Add(lblTitle);
        y += 32;

        // Preset selector
        parent.Controls.Add(new Label
        {
            Text = "Оружие (CraftedItem):",
            Location = new Point(12, y + 4),
            Width = 150,
            ForeColor = Color.FromArgb(139, 148, 158)
        });
        _cmbWeaponPreset = new ComboBox
        {
            Location = new Point(168, y),
            Width = 315,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(13, 17, 23),
            ForeColor = Color.White
        };
        foreach (var w in _workingWeapons)
            _cmbWeaponPreset.Items.Add($"{w.Id} ({w.Name})");
        _cmbWeaponPreset.SelectedIndexChanged += OnWeaponPresetChanged;
        parent.Controls.Add(_cmbWeaponPreset);
        y += 34;

        // Template selector
        parent.Controls.Add(new Label
        {
            Text = "Шаблон (Template):",
            Location = new Point(12, y + 4),
            Width = 150,
            ForeColor = Color.FromArgb(139, 148, 158)
        });
        _cmbTemplate = new ComboBox
        {
            Location = new Point(168, y),
            Width = 315,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(13, 17, 23),
            ForeColor = Color.White
        };
        foreach (var t in _templates.Keys)
            _cmbTemplate.Items.Add(t);
        _cmbTemplate.SelectedIndexChanged += (_, _) =>
        {
            if (_suppressEvents || _currentWeapon == null) return;
            _currentWeapon.TemplateId = _cmbTemplate.SelectedItem?.ToString() ?? "OneHandedSword";
            RecomputeAndRefresh();
        };
        parent.Controls.Add(_cmbTemplate);
        y += 38;

        // Action Buttons: AUTO-CALIBRATE & RESET
        var btnCalibrate = new Button
        {
            Text = "⚡ АВТО-КАЛИБРОВКА ДЕТАЛЕЙ (Стык 0.00 см)",
            Location = new Point(12, y),
            Size = new Size(472, 40),
            BackColor = Color.FromArgb(35, 134, 54),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnCalibrate.FlatAppearance.BorderColor = Color.FromArgb(46, 160, 67);
        btnCalibrate.Click += (_, _) => RunAutoCalibrateCurrentWeapon();
        parent.Controls.Add(btnCalibrate);
        y += 46;

        var btnReset = new Button
        {
            Text = "🔄 Сбросить к исходному XML (Показать ДО калибровки)",
            Location = new Point(12, y),
            Size = new Size(472, 32),
            BackColor = Color.FromArgb(48, 54, 61),
            ForeColor = Color.FromArgb(201, 209, 217),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnReset.FlatAppearance.BorderColor = Color.FromArgb(110, 118, 129);
        btnReset.Click += (_, _) => ResetCurrentWeaponToOriginal();
        parent.Controls.Add(btnReset);
        y += 42;

        // 4 Part Editor Cards: Blade, Guard, Handle, Pommel
        PieceType[] order = { PieceType.Blade, PieceType.Guard, PieceType.Handle, PieceType.Pommel };
        var partColors = new Dictionary<PieceType, Color>
        {
            { PieceType.Blade,  Color.FromArgb(88, 166, 255) },
            { PieceType.Guard,  Color.FromArgb(255, 211, 61) },
            { PieceType.Handle, Color.FromArgb(240, 136, 62) },
            { PieceType.Pommel, Color.FromArgb(210, 168, 255) }
        };
        var partTitles = new Dictionary<PieceType, string>
        {
            { PieceType.Blade,  "🗡️ BLADE (Клинок / Наконечник / Ударная часть)" },
            { PieceType.Guard,  "🛡️ GUARD (Гарда / Перекрестье / Крылья копья)" },
            { PieceType.Handle, "✊ HANDLE (Рукоять / Древко — точка хвата Z=0)" },
            { PieceType.Pommel, "💎 POMMEL (Навершие / Яблоко)" }
        };

        foreach (var pt in order)
        {
            var card = CreatePartCard(pt, partTitles[pt], partColors[pt], new Point(12, y));
            parent.Controls.Add(card);
            y += card.Height + 10;
        }
    }

    private Panel CreatePartCard(PieceType pt, string title, Color accentColor, Point location)
    {
        var card = new Panel
        {
            Location = location,
            Size = new Size(472, 152),
            BackColor = Color.FromArgb(13, 17, 23),
            BorderStyle = BorderStyle.FixedSingle
        };

        var header = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = accentColor,
            Location = new Point(8, 6),
            AutoSize = true
        };
        card.Controls.Add(header);

        var row = new PartEditorRow { PieceType = pt };

        card.Controls.Add(new Label
        {
            Text = "Деталь:",
            Location = new Point(8, 32),
            Width = 58,
            ForeColor = Color.FromArgb(139, 148, 158)
        });

        row.CmbPiece = new ComboBox
        {
            Location = new Point(68, 29),
            Width = 245,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(22, 27, 34),
            ForeColor = Color.White
        };
        row.CmbPiece.Items.Add("(Нет детали)");
        foreach (var p in _workingPieces.Values.Where(x => x.PieceType == pt).OrderBy(x => x.Id))
        {
            row.CmbPiece.Items.Add(p.Id);
        }
        row.CmbPiece.SelectedIndexChanged += (_, _) => OnPartRowEdited(row);
        card.Controls.Add(row.CmbPiece);

        card.Controls.Add(new Label
        {
            Text = "Scale %:",
            Location = new Point(322, 32),
            Width = 58,
            ForeColor = Color.FromArgb(139, 148, 158)
        });
        row.NumScale = CreateSpinner(new Point(382, 29), 76, 10, 300, 0, 5, () => OnPartRowEdited(row));
        card.Controls.Add(row.NumScale);

        // Numeric spinners for Length, piece_offset, prev_offset, next_offset
        int sy = 62;
        card.Controls.Add(new Label { Text = "Length (см):", Location = new Point(8, sy + 3), Width = 80, ForeColor = Color.FromArgb(139, 148, 158) });
        row.NumLength = CreateSpinner(new Point(90, sy), 82, 0.1m, 600m, 2, 0.5m, () => OnPartRowEdited(row));
        card.Controls.Add(row.NumLength);

        card.Controls.Add(new Label { Text = "piece_off:", Location = new Point(190, sy + 3), Width = 72, ForeColor = Color.FromArgb(139, 148, 158) });
        row.NumPieceOffset = CreateSpinner(new Point(266, sy), 82, -300m, 300m, 2, 0.5m, () => OnPartRowEdited(row));
        card.Controls.Add(row.NumPieceOffset);

        int sy2 = 92;
        card.Controls.Add(new Label { Text = "prev (низ):", Location = new Point(8, sy2 + 3), Width = 80, ForeColor = Color.FromArgb(139, 148, 158) });
        row.NumPrevOffset = CreateSpinner(new Point(90, sy2), 82, -300m, 300m, 2, 0.5m, () => OnPartRowEdited(row));
        card.Controls.Add(row.NumPrevOffset);

        card.Controls.Add(new Label { Text = "next (верх):", Location = new Point(190, sy2 + 3), Width = 74, ForeColor = Color.FromArgb(139, 148, 158) });
        row.NumNextOffset = CreateSpinner(new Point(266, sy2), 82, -300m, 300m, 2, 0.5m, () => OnPartRowEdited(row));
        card.Controls.Add(row.NumNextOffset);

        row.LblWorldBounds = new Label
        {
            Text = "World Z: —",
            Location = new Point(8, 124),
            Width = 455,
            ForeColor = Color.FromArgb(126, 231, 135),
            Font = new Font("Consolas", 8.5f)
        };
        card.Controls.Add(row.LblWorldBounds);

        _partRows[pt] = row;
        return card;
    }

    private NumericUpDown CreateSpinner(Point loc, int width, decimal min, decimal max, int decimals, decimal inc, Action onChanged)
    {
        var num = new NumericUpDown
        {
            Location = loc,
            Width = width,
            Minimum = min,
            Maximum = max,
            DecimalPlaces = decimals,
            Increment = inc,
            BackColor = Color.FromArgb(22, 27, 34),
            ForeColor = Color.White
        };
        num.ValueChanged += (_, _) =>
        {
            if (!_suppressEvents) onChanged();
        };
        return num;
    }

    private void BuildCenterViewport(Panel parent)
    {
        // Top Camera Toolbar + Diagnostics Header
        var topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 142,
            BackColor = Color.FromArgb(22, 27, 34),
            Padding = new Padding(10)
        };

        var btnFront = CreateSmallButton("Вид Спереди", new Point(10, 8), (_, _) => { _camYaw = 0f; _camPitch = 0f; _viewport.Invalidate(); });
        var btnSide = CreateSmallButton("Вид Сбоку", new Point(118, 8), (_, _) => { _camYaw = 1.5708f; _camPitch = 0f; _viewport.Invalidate(); });
        var btnIso = CreateSmallButton("3D Изометрия", new Point(226, 8), (_, _) => { _camYaw = 0.45f; _camPitch = 0.25f; _viewport.Invalidate(); });
        var btnFit = CreateSmallButton("🔍 Авто-Фокус", new Point(334, 8), (_, _) => { AutoFocusCamera(); _viewport.Invalidate(); });

        _chkShowWireframe = new CheckBox
        {
            Text = "3D Сетка (Wireframe)",
            Checked = true,
            AutoSize = true,
            Location = new Point(12, 40),
            ForeColor = Color.FromArgb(201, 209, 217)
        };
        _chkShowWireframe.CheckedChanged += (_, _) => _viewport.Invalidate();

        _chkShowBBox = new CheckBox
        {
            Text = "Габариты & Муфты",
            Checked = true,
            AutoSize = true,
            Location = new Point(180, 40),
            ForeColor = Color.FromArgb(201, 209, 217)
        };
        _chkShowBBox.CheckedChanged += (_, _) => _viewport.Invalidate();

        _chkShowPivots = new CheckBox
        {
            Text = "Пивоты & Ось Z",
            Checked = true,
            AutoSize = true,
            Location = new Point(345, 40),
            ForeColor = Color.FromArgb(201, 209, 217)
        };
        _chkShowPivots.CheckedChanged += (_, _) => _viewport.Invalidate();

        _lblDiagnostics = new Label
        {
            Location = new Point(10, 66),
            Size = new Size(920, 72),
            Font = new Font("Consolas", 9.0f, FontStyle.Bold),
            ForeColor = Color.FromArgb(126, 231, 135),
            Text = "Готов к калибровке..."
        };

        topBar.Controls.AddRange(new Control[]
        {
            btnFront, btnSide, btnIso, btnFit,
            _chkShowWireframe, _chkShowBBox, _chkShowPivots,
            _lblDiagnostics
        });

        _viewport = new DoubleBufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(9, 13, 19),
            Cursor = Cursors.Cross
        };
        _viewport.Paint += OnViewportPaint;
        _viewport.MouseDown += OnViewportMouseDown;
        _viewport.MouseMove += OnViewportMouseMove;
        _viewport.MouseUp += OnViewportMouseUp;
        _viewport.MouseWheel += OnViewportMouseWheel;

        parent.Controls.Add(_viewport);
        parent.Controls.Add(topBar);
    }

    private Button CreateSmallButton(string text, Point loc, EventHandler onClick)
    {
        var b = new Button
        {
            Text = text,
            Location = loc,
            Size = new Size(102, 28),
            BackColor = Color.FromArgb(33, 38, 45),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 8.5f)
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(68, 76, 86);
        b.Click += onClick;
        return b;
    }

    private void BuildRightPanel(Panel parent)
    {
        var lblXml = new Label
        {
            Text = "📄 ГОТОВЫЙ XML КОД (CraftingPieces & CraftedItem)",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(88, 166, 255),
            Dock = DockStyle.Top,
            Height = 28
        };

        var bottomButtons = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 132
        };

        var btnCopy = new Button
        {
            Text = "📋 Копировать XML этого оружия в буфер обмена",
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = Color.FromArgb(31, 111, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnCopy.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_txtXmlOutput.Text))
            {
                Clipboard.SetText(_txtXmlOutput.Text);
                MessageBox.Show("XML-код откалиброванного оружия скопирован в буфер обмена!", "Омниссия благословляет", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        var btnSaveOut = new Button
        {
            Text = "💾 Калибровать ВСЕ оружия и сохранить в D:\\output",
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = Color.FromArgb(35, 134, 54),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnSaveOut.Click += (_, _) =>
        {
            foreach (var w in _workingWeapons)
            {
                if (_templates.TryGetValue(w.TemplateId, out var t))
                    Program.AutoCalibrateWeapon(w, t, _workingPieces, _geometries);
            }
            SyncWeaponToControls();
            RecomputeAndRefresh();
            string outXml = Path.Combine(_outputDir, "calibrated_crafting_pieces.xml");
            Program.SaveCalibratedPiecesXml(Path.Combine(_modPath, @"ModuleData\crafting_pieces.xml"), _workingPieces, outXml);
            MessageBox.Show($"Все оружия откалиброваны и сохранены в:\n{outXml}", "Сохранено", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var btnApplyMod = new Button
        {
            Text = "🚀 Записать калибровку прямо в мод (DWDN_TRY)",
            Dock = DockStyle.Bottom,
            Height = 38,
            BackColor = Color.FromArgb(158, 106, 3),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnApplyMod.Click += (_, _) =>
        {
            string targetXml = Path.Combine(_modPath, @"ModuleData\crafting_pieces.xml");
            string backupXml = Path.Combine(_outputDir, $"crafting_pieces_backup_{DateTime.Now:yyyyMMdd_HHmmss}.xml");
            File.Copy(targetXml, backupXml, true);
            Program.SaveCalibratedPiecesXml(targetXml, _workingPieces, targetXml);
            MessageBox.Show($"Калибровка записана в:\n{targetXml}\n\nРезервная копия сохранена в:\n{backupXml}", "Мод обновлён", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        bottomButtons.Controls.Add(btnApplyMod);
        bottomButtons.Controls.Add(btnSaveOut);
        bottomButtons.Controls.Add(btnCopy);

        _txtXmlOutput = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            BackColor = Color.FromArgb(1, 4, 9),
            ForeColor = Color.FromArgb(126, 231, 135),
            Font = new Font("Consolas", 9.2f),
            BorderStyle = BorderStyle.FixedSingle
        };

        parent.Controls.Add(_txtXmlOutput);
        parent.Controls.Add(bottomButtons);
        parent.Controls.Add(lblXml);
    }

    private void LoadInitialWeapon()
    {
        if (_cmbWeaponPreset.Items.Count > 0)
        {
            _cmbWeaponPreset.SelectedIndex = 0;
            // Automatically run calibration on initial load so the user immediately sees the 0.00 cm calibrated sword!
            RunAutoCalibrateCurrentWeapon();
        }
    }

    private void OnWeaponPresetChanged(object sender, EventArgs e)
    {
        int idx = _cmbWeaponPreset.SelectedIndex;
        if (idx < 0 || idx >= _workingWeapons.Count) return;

        _currentWeapon = _workingWeapons[idx];
        SyncWeaponToControls();
        RecomputeAndRefresh();
        AutoFocusCamera();
        _viewport?.Invalidate();
    }

    private void SyncWeaponToControls()
    {
        if (_currentWeapon == null) return;
        _suppressEvents = true;

        if (_cmbTemplate.Items.Contains(_currentWeapon.TemplateId))
            _cmbTemplate.SelectedItem = _currentWeapon.TemplateId;
        else if (_cmbTemplate.Items.Count > 0)
            _cmbTemplate.SelectedIndex = 0;

        foreach (var kv in _partRows)
        {
            var pt = kv.Key;
            var row = kv.Value;

            if (_currentWeapon.Pieces.TryGetValue(pt, out var used) &&
                !string.IsNullOrEmpty(used.PieceId) &&
                _workingPieces.TryGetValue(used.PieceId, out var pDef))
            {
                if (!row.CmbPiece.Items.Contains(used.PieceId))
                    row.CmbPiece.Items.Add(used.PieceId);
                row.CmbPiece.SelectedItem = used.PieceId;
                row.NumScale.Value = Math.Clamp(used.ScalePercent, 10, 300);
                row.NumLength.Value = Math.Clamp((decimal)pDef.LengthCm, 0.1m, 600m);
                row.NumPieceOffset.Value = Math.Clamp((decimal)pDef.PieceOffsetCm, -300m, 300m);
                row.NumPrevOffset.Value = Math.Clamp((decimal)pDef.PreviousPieceOffsetCm, -300m, 300m);
                row.NumNextOffset.Value = Math.Clamp((decimal)pDef.NextPieceOffsetCm, -300m, 300m);
                row.NumScale.Enabled = row.NumLength.Enabled = row.NumPieceOffset.Enabled = row.NumPrevOffset.Enabled = row.NumNextOffset.Enabled = true;
            }
            else
            {
                row.CmbPiece.SelectedIndex = 0; // "(Нет детали)"
                row.NumScale.Value = 100;
                row.NumLength.Value = 10;
                row.NumPieceOffset.Value = 0;
                row.NumPrevOffset.Value = 0;
                row.NumNextOffset.Value = 0;
                row.NumScale.Enabled = row.NumLength.Enabled = row.NumPieceOffset.Enabled = row.NumPrevOffset.Enabled = row.NumNextOffset.Enabled = false;
                row.LblWorldBounds.Text = "World Z: (деталь не используется)";
            }
        }

        _suppressEvents = false;
    }

    private void OnPartRowEdited(PartEditorRow row)
    {
        if (_suppressEvents || _currentWeapon == null) return;

        string selectedId = row.CmbPiece.SelectedIndex <= 0 ? "" : row.CmbPiece.SelectedItem?.ToString() ?? "";
        if (string.IsNullOrEmpty(selectedId))
        {
            _currentWeapon.Pieces.Remove(row.PieceType);
            SyncWeaponToControls();
            RecomputeAndRefresh();
            return;
        }

        int scale = (int)row.NumScale.Value;
        bool pieceChanged = !_currentWeapon.Pieces.TryGetValue(row.PieceType, out var prev) || prev.PieceId != selectedId;
        _currentWeapon.Pieces[row.PieceType] = (selectedId, scale);

        if (_workingPieces.TryGetValue(selectedId, out var pDef))
        {
            if (pieceChanged)
            {
                SyncWeaponToControls();
            }
            else
            {
                pDef.LengthCm = (float)row.NumLength.Value;
                pDef.PieceOffsetCm = (float)row.NumPieceOffset.Value;
                pDef.PreviousPieceOffsetCm = (float)row.NumPrevOffset.Value;
                pDef.NextPieceOffsetCm = (float)row.NumNextOffset.Value;
            }
        }

        RecomputeAndRefresh();
    }

    private void RunAutoCalibrateCurrentWeapon()
    {
        if (_currentWeapon == null) return;
        if (!_templates.TryGetValue(_currentWeapon.TemplateId, out var tmpl))
            tmpl = _templates.Values.First();

        Program.AutoCalibrateWeapon(_currentWeapon, tmpl, _workingPieces, _geometries);
        SyncWeaponToControls();
        RecomputeAndRefresh();
        AutoFocusCamera();
        _viewport?.Invalidate();
    }

    private void ResetCurrentWeaponToOriginal()
    {
        if (_currentWeapon == null) return;
        foreach (var kv in _currentWeapon.Pieces.ToList())
        {
            if (_originalPieces.TryGetValue(kv.Value.PieceId, out var origPiece))
            {
                _workingPieces[kv.Value.PieceId] = origPiece.Clone();
            }
        }
        SyncWeaponToControls();
        RecomputeAndRefresh();
        _viewport?.Invalidate();
    }

    private void RecomputeAndRefresh()
    {
        if (_currentWeapon == null) return;
        if (!_templates.TryGetValue(_currentWeapon.TemplateId, out var tmpl))
            tmpl = _templates.Values.First();

        _currentSim = Program.SimulateWeapon(_currentWeapon, tmpl, _workingPieces, _geometries);

        // Update each PartEditorRow world bounds label
        foreach (var kv in _partRows)
        {
            if (_currentSim.Placements.TryGetValue(kv.Key, out var pl))
            {
                string meshInfo = pl.Geometry != null
                    ? $"Mesh Z:[{pl.Geometry.MinZCm:F1}..{pl.Geometry.MaxZCm:F1}]"
                    : "No TPAC";
                kv.Value.LblWorldBounds.Text = $"Pivot={pl.PivotZCm:+0.0;-0.0;0.0}см | World Z:[{pl.WorldMinZCm:F2} .. {pl.WorldMaxZCm:F2}]см ({meshInfo})";
            }
            else
            {
                kv.Value.LblWorldBounds.Text = "World Z: (не используется)";
            }
        }

        // Update Top Diagnostics Banner
        var diagSb = new StringBuilder();
        diagSb.AppendLine($"ОРУЖИЕ: {_currentWeapon.Id} | Длина от хвата (Reach): {_currentSim.CraftedWeaponLengthCm:F1} см | Полная длина: {_currentSim.TotalLengthCm:F1} см");
        foreach (var d in _currentSim.Diagnostics)
        {
            diagSb.AppendLine("  • " + d);
        }
        if (_lblDiagnostics != null)
        {
            _lblDiagnostics.Text = diagSb.ToString();
            bool hasMismatchOrBigGap = _currentSim.Diagnostics.Any(d => d.Contains("MISMATCH") || (d.Contains("gap =") && !d.Contains(" 0.00 cm") && !d.Contains("-0.01 cm") && !d.Contains("+0.01 cm")));
            _lblDiagnostics.ForeColor = hasMismatchOrBigGap ? Color.FromArgb(255, 190, 80) : Color.FromArgb(126, 231, 135);
        }

        // Update XML Output Box
        var xmlSb = new StringBuilder();
        xmlSb.AppendLine($"<!-- ==================== 1. вставить в ModuleData/crafting_pieces.xml ==================== -->");
        foreach (var pl in _currentSim.Placements.Values)
        {
            var p = pl.PieceDef;
            xmlSb.AppendLine($"<CraftingPiece id=\"{p.Id}\" name=\"{p.Name}\" piece_type=\"{p.PieceType}\" mesh=\"{p.MeshName}\" length=\"{p.LengthCm.ToString("0.##", CultureInfo.InvariantCulture)}\" weight=\"{p.Weight.ToString("0.##", CultureInfo.InvariantCulture)}\">");
            xmlSb.AppendLine($"    <BuildData piece_offset=\"{p.PieceOffsetCm.ToString("0.##", CultureInfo.InvariantCulture)}\" previous_piece_offset=\"{p.PreviousPieceOffsetCm.ToString("0.##", CultureInfo.InvariantCulture)}\" next_piece_offset=\"{p.NextPieceOffsetCm.ToString("0.##", CultureInfo.InvariantCulture)}\" />");
            xmlSb.AppendLine($"</CraftingPiece>");
            xmlSb.AppendLine();
        }

        xmlSb.AppendLine($"<!-- ==================== 2. вставить в ModuleData/mpitems/dwdn_weapon.xml ==================== -->");
        xmlSb.AppendLine($"<CraftedItem id=\"{_currentWeapon.Id}\" name=\"{_currentWeapon.Name}\" crafting_template=\"{_currentWeapon.TemplateId}\" multiplayer_item=\"true\">");
        xmlSb.AppendLine($"    <Pieces>");
        foreach (var pt in new[] { PieceType.Blade, PieceType.Guard, PieceType.Handle, PieceType.Pommel })
        {
            if (_currentWeapon.Pieces.TryGetValue(pt, out var used) && !string.IsNullOrEmpty(used.PieceId))
            {
                xmlSb.AppendLine($"        <Piece id=\"{used.PieceId}\" Type=\"{pt}\" scale_factor=\"{used.ScalePercent}\" />");
            }
        }
        xmlSb.AppendLine($"    </Pieces>");
        xmlSb.AppendLine($"</CraftedItem>");

        if (_txtXmlOutput != null)
            _txtXmlOutput.Text = xmlSb.ToString();

        _viewport?.Invalidate();
    }

    private void AutoFocusCamera()
    {
        if (_currentSim == null || _currentSim.Placements.Count == 0) return;
        float minZ = _currentSim.Placements.Values.Min(p => p.WorldMinZCm);
        float maxZ = _currentSim.Placements.Values.Max(p => p.WorldMaxZCm);
        float spanZ = Math.Max(40f, maxZ - minZ);
        _camPanX = 0f;
        _camPanY = 0.5f * (minZ + maxZ);
        int h = _viewport != null && _viewport.Height > 100 ? _viewport.Height : 680;
        _camZoom = Math.Clamp((h - 120f) / spanZ, 0.8f, 8.0f);
    }

    private void OnViewportMouseDown(object sender, MouseEventArgs e)
    {
        _lastMouse = e.Location;
        if (e.Button == MouseButtons.Left) _isOrbiting = true;
        if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle) _isPanning = true;
    }

    private void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        int dx = e.X - _lastMouse.X;
        int dy = e.Y - _lastMouse.Y;
        _lastMouse = e.Location;

        if (_isOrbiting)
        {
            _camYaw += dx * 0.012f;
            _camPitch = Math.Clamp(_camPitch + dy * 0.012f, -1.4f, 1.4f);
            _viewport.Invalidate();
        }
        else if (_isPanning)
        {
            _camPanX += dx / _camZoom;
            _camPanY += dy / _camZoom;
            _viewport.Invalidate();
        }
    }

    private void OnViewportMouseUp(object sender, MouseEventArgs e)
    {
        _isOrbiting = false;
        _isPanning = false;
    }

    private void OnViewportMouseWheel(object sender, MouseEventArgs e)
    {
        float factor = e.Delta > 0 ? 1.15f : 0.87f;
        _camZoom = Math.Clamp(_camZoom * factor, 0.4f, 28.0f);
        _viewport.Invalidate();
    }

    // 3D Math projection helper: World (X, Y, Z_up) in cm -> Screen (SX, SY, Depth)
    private (float sx, float sy, float depth) Project3D(float wx, float wy, float wz, float cx, float cy)
    {
        // Shift Z by _camPanY so rotation orbits around the center of the weapon
        float zRel = wz - _camPanY;
        float xRel = wx;
        float yRel = wy;

        // Rotate around Z (Yaw)
        float cosY = MathF.Cos(_camYaw);
        float sinY = MathF.Sin(_camYaw);
        float x1 = xRel * cosY - yRel * sinY;
        float y1 = xRel * sinY + yRel * cosY;
        float z1 = zRel;

        // Rotate around X (Pitch)
        float cosP = MathF.Cos(_camPitch);
        float sinP = MathF.Sin(_camPitch);
        float y2 = y1 * cosP - z1 * sinP;
        float z2 = y1 * sinP + z1 * cosP;

        float sx = cx + (x1 + _camPanX) * _camZoom;
        float sy = cy - z2 * _camZoom;
        return (sx, sy, y2);
    }

    private struct RenderTriangle
    {
        public PointF P0, P1, P2;
        public float Depth;
        public Color FillColor;
        public Color WireColor;
    }

    private void OnViewportPaint(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(9, 13, 19));

        float cx = _viewport.Width * 0.5f;
        float cy = _viewport.Height * 0.5f;

        // 1. Draw Background Grid & Z-Axis Ruler (in cm)
        using var gridPen = new Pen(Color.FromArgb(24, 32, 44), 1f);
        using var axisPen = new Pen(Color.FromArgb(55, 70, 90), 1.5f);
        using var rulerFont = new Font("Consolas", 8f);
        using var rulerBrush = new SolidBrush(Color.FromArgb(110, 125, 145));

        for (int zCm = -240; zCm <= 260; zCm += 20)
        {
            var pLeft = Project3D(-35f, 0f, zCm, cx, cy);
            var pRight = Project3D(+35f, 0f, zCm, cx, cy);
            g.DrawLine(gridPen, pLeft.sx, pLeft.sy, pRight.sx, pRight.sy);
            if (_chkShowPivots.Checked)
            {
                var pTick = Project3D(-28f, 0f, zCm, cx, cy);
                g.DrawString($"{zCm:+0;-0;0}см", rulerFont, rulerBrush, pTick.sx - 44, pTick.sy - 6);
            }
        }

        var zBot = Project3D(0f, 0f, -250f, cx, cy);
        var zTop = Project3D(0f, 0f, +260f, cx, cy);
        g.DrawLine(axisPen, zBot.sx, zBot.sy, zTop.sx, zTop.sy);

        if (_currentSim == null) return;

        var pieceBaseColors = new Dictionary<PieceType, Color>
        {
            { PieceType.Blade,  Color.FromArgb(130, 195, 255) },
            { PieceType.Guard,  Color.FromArgb(255, 210, 80) },
            { PieceType.Handle, Color.FromArgb(235, 140, 75) },
            { PieceType.Pommel, Color.FromArgb(205, 160, 255) }
        };

        // 2. Collect & Project 3D Triangles from all Weapon Parts
        var renderTris = new List<RenderTriangle>(4096);
        foreach (var pl in _currentSim.Placements.Values)
        {
            Color baseCol = pieceBaseColors[pl.PieceType];
            float zScale = pl.ScaleFactor;
            float xyScale = pl.PieceDef.FullScale ? zScale : 1.0f;
            float pivZ = pl.PivotZCm;

            if (pl.Geometry != null && pl.Geometry.VerticesCm.Length > 0 && pl.Geometry.Triangles.Length > 0)
            {
                var verts = pl.Geometry.VerticesCm;
                var tris = pl.Geometry.Triangles;
                int step = tris.Length > 3500 ? 2 : 1; // Keep 60 FPS even on dense meshes

                for (int i = 0; i < tris.Length; i += step)
                {
                    var (ia, ib, ic) = tris[i];
                    var va = verts[ia];
                    var vb = verts[ib];
                    var vc = verts[ic];

                    float ax = va.X * xyScale, ay = va.Y * xyScale, az = pivZ + va.Z * zScale;
                    float bx = vb.X * xyScale, by = vb.Y * xyScale, bz = pivZ + vb.Z * zScale;
                    float cx3 = vc.X * xyScale, cy3 = vc.Y * xyScale, cz = pivZ + vc.Z * zScale;

                    // Compute face normal for 3D Lambertian shading
                    float ux = bx - ax, uy = by - ay, uz = bz - az;
                    float vx = cx3 - ax, vy = cy3 - ay, vz = cz - az;
                    float nx = uy * vz - uz * vy;
                    float ny = uz * vx - ux * vz;
                    float nz = ux * vy - uy * vx;
                    float nLen = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
                    float light = 0.65f;
                    if (nLen > 1e-5f)
                    {
                        nx /= nLen; ny /= nLen; nz /= nLen;
                        float dot = MathF.Abs(nx * 0.45f - ny * 0.75f + nz * 0.48f);
                        light = 0.35f + 0.65f * Math.Clamp(dot, 0f, 1f);
                    }

                    var pa = Project3D(ax, ay, az, cx, cy);
                    var pb = Project3D(bx, by, bz, cx, cy);
                    var pc = Project3D(cx3, cy3, cz, cx, cy);

                    int r = (int)Math.Clamp(baseCol.R * light, 0, 255);
                    int gr = (int)Math.Clamp(baseCol.G * light, 0, 255);
                    int bl = (int)Math.Clamp(baseCol.B * light, 0, 255);

                    renderTris.Add(new RenderTriangle
                    {
                        P0 = new PointF(pa.sx, pa.sy),
                        P1 = new PointF(pb.sx, pb.sy),
                        P2 = new PointF(pc.sx, pc.sy),
                        Depth = (pa.depth + pb.depth + pc.depth) * 0.3333f,
                        FillColor = Color.FromArgb(225, r, gr, bl),
                        WireColor = Color.FromArgb(70, baseCol.R, baseCol.G, baseCol.B)
                    });
                }
            }
        }

        // Painter's Algorithm sort (back to front)
        renderTris.Sort((a, b) => b.Depth.CompareTo(a.Depth));

        bool drawWire = _chkShowWireframe.Checked;
        PointF[] pts = new PointF[3];
        foreach (var tri in renderTris)
        {
            pts[0] = tri.P0;
            pts[1] = tri.P1;
            pts[2] = tri.P2;
            using var br = new SolidBrush(tri.FillColor);
            g.FillPolygon(br, pts);
            if (drawWire)
            {
                using var wp = new Pen(tri.WireColor, 0.6f);
                g.DrawPolygon(wp, pts);
            }
        }

        // 3. Draw 3D Bounding Boxes & Central Guard Collar Rings
        if (_chkShowBBox.Checked)
        {
            foreach (var pl in _currentSim.Placements.Values)
            {
                Color col = pieceBaseColors[pl.PieceType];
                DrawWireBox3D(g, pl.WorldMinXCm, pl.WorldMaxXCm, -1.5f, 1.5f, pl.WorldMinZCm, pl.WorldMaxZCm, col, cx, cy, dashed: true);

                // Highlight Central Collar on curved guards (e.g. bergen_guard)
                if (pl.PieceType == PieceType.Guard && pl.Geometry != null && pl.Geometry.HasCoreBounds &&
                    (pl.Geometry.CoreMaxZCm - pl.Geometry.CoreMinZCm) < pl.Geometry.LengthZCm * 0.7f)
                {
                    float coreBot = pl.PivotZCm + pl.Geometry.CoreMinZCm * pl.ScaleFactor;
                    float coreTop = pl.PivotZCm + pl.Geometry.CoreMaxZCm * pl.ScaleFactor;
                    DrawWireBox3D(g, -2.2f, +2.2f, -2.0f, +2.0f, coreBot, coreTop, Color.FromArgb(46, 230, 110), cx, cy, dashed: false);
                }
            }
        }

        // 4. Draw Hand Grip Z=0 Ring & Piece Pivot Spheres
        if (_chkShowPivots.Checked)
        {
            var gripLeft = Project3D(-14f, 0f, 0f, cx, cy);
            var gripRight = Project3D(+14f, 0f, 0f, cx, cy);
            using var gripPen = new Pen(Color.FromArgb(248, 81, 73), 2.0f) { DashStyle = DashStyle.Dash };
            g.DrawLine(gripPen, gripLeft.sx, gripLeft.sy, gripRight.sx, gripRight.sy);
            using var gripBrush = new SolidBrush(Color.FromArgb(248, 81, 73));
            g.DrawString("✋ ХВАТ РУКИ (Z = 0.0 см)", new Font("Segoe UI", 8.5f, FontStyle.Bold), gripBrush, gripLeft.sx - 155, gripLeft.sy - 8);

            foreach (var pl in _currentSim.Placements.Values)
            {
                Color col = pieceBaseColors[pl.PieceType];
                var piv = Project3D(0f, 0f, pl.PivotZCm, cx, cy);
                using var fillBr = new SolidBrush(col);
                using var outlinePen = new Pen(Color.White, 1.8f);
                g.FillEllipse(fillBr, piv.sx - 5, piv.sy - 5, 10, 10);
                g.DrawEllipse(outlinePen, piv.sx - 5, piv.sy - 5, 10, 10);

                var labelPos = Project3D(Math.Max(8f, pl.WorldMaxXCm + 3f), 0f, 0.5f * (pl.WorldMinZCm + pl.WorldMaxZCm), cx, cy);
                g.DrawString($"{pl.PieceType}: {pl.PieceId} [{pl.WorldMinZCm:F1}..{pl.WorldMaxZCm:F1}см]",
                    new Font("Segoe UI", 8.5f, FontStyle.Bold), fillBr, labelPos.sx + 4, labelPos.sy - 7);
            }
        }

        // 5. Overlay Controls Hint at Bottom-Left of Viewport
        using var hintFont = new Font("Segoe UI", 8.5f);
        using var hintBrush = new SolidBrush(Color.FromArgb(139, 148, 158));
        g.DrawString("🖱️ ЛКМ: Вращение 3D-камеры  |  ПКМ/СКМ: Панорама (Сдвиг)  |  Колесо: Зум (Масштаб)",
            hintFont, hintBrush, 12, _viewport.Height - 24);
    }

    private void DrawWireBox3D(
        Graphics g,
        float minX, float maxX,
        float minY, float maxY,
        float minZ, float maxZ,
        Color color, float cx, float cy, bool dashed)
    {
        using var pen = new Pen(Color.FromArgb(dashed ? 130 : 230, color), dashed ? 1.0f : 2.0f);
        if (dashed) pen.DashStyle = DashStyle.Dot;

        var b0 = Project3D(minX, minY, minZ, cx, cy);
        var b1 = Project3D(maxX, minY, minZ, cx, cy);
        var b2 = Project3D(maxX, maxY, minZ, cx, cy);
        var b3 = Project3D(minX, maxY, minZ, cx, cy);

        var t0 = Project3D(minX, minY, maxZ, cx, cy);
        var t1 = Project3D(maxX, minY, maxZ, cx, cy);
        var t2 = Project3D(maxX, maxY, maxZ, cx, cy);
        var t3 = Project3D(minX, maxY, maxZ, cx, cy);

        g.DrawLine(pen, b0.sx, b0.sy, b1.sx, b1.sy);
        g.DrawLine(pen, b1.sx, b1.sy, b2.sx, b2.sy);
        g.DrawLine(pen, b2.sx, b2.sy, b3.sx, b3.sy);
        g.DrawLine(pen, b3.sx, b3.sy, b0.sx, b0.sy);

        g.DrawLine(pen, t0.sx, t0.sy, t1.sx, t1.sy);
        g.DrawLine(pen, t1.sx, t1.sy, t2.sx, t2.sy);
        g.DrawLine(pen, t2.sx, t2.sy, t3.sx, t3.sy);
        g.DrawLine(pen, t3.sx, t3.sy, t0.sx, t0.sy);

        g.DrawLine(pen, b0.sx, b0.sy, t0.sx, t0.sy);
        g.DrawLine(pen, b1.sx, b1.sy, t1.sx, t1.sy);
        g.DrawLine(pen, b2.sx, b2.sy, t2.sx, t2.sy);
        g.DrawLine(pen, b3.sx, b3.sy, t3.sx, t3.sy);
    }

    public void SaveWindowScreenshot(string path)
    {
        using var bmp = new Bitmap(Width, Height);
        DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
        bmp.Save(path, ImageFormat.Png);
    }
}
