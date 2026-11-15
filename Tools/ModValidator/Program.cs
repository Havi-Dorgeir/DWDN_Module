#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ModValidator
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
            Application.Run(new ValidatorForm());
        }
    }

    public class ValidatorForm : Form
    {
        private string _modRoot = @"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\DWDN_TRY";
        private string _gameModulesRoot = @"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules";
        private string _outDir = @"d:\banAImod\ModValidator\output";

        private ValidationReport _currentReport = new();
        private string _selectedCategory = "ALL";
        private int _severityFilter = 1; // 1 = Errors + Warnings (default), 2 = Errors only, 3 = Warnings only, 0 = All including Pass
        private string _searchQuery = "";

        // KPI Labels
        private Label _lblKpiErrors = null!;
        private Label _lblKpiWarnings = null!;
        private Label _lblKpiPassed = null!;
        private Label _lblKpiFixable = null!;
        private Label _lblKpiSpeed = null!;
        private Label _lblVerdictBanner = null!;

        // Left Panel Controls
        private ListBox _lstCategories = null!;
        private Button[] _filterButtons = new Button[4];
        private TextBox _txtSearch = null!;

        // Center ListView
        private ListView _lvIssues = null!;

        // Bottom Inspector Controls
        private Label _lblDetailHeader = null!;
        private RichTextBox _rtbDetailBody = null!;
        private Button _btnFixSelected = null!;
        private Button _btnOpenFile = null!;
        private Label _lblStatus = null!;

        private ValidationIssue? _selectedIssue;

        private void ResolvePaths()
        {
            if (!Directory.Exists(_modRoot))
            {
                DirectoryInfo dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (dir != null)
                {
                    if (dir.Name.Equals("DWDN_TRY", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(dir.FullName, "SubModule.xml")))
                    {
                        _modRoot = dir.FullName;
                        break;
                    }
                    dir = dir.Parent;
                }
            }
            if (!Directory.Exists(_gameModulesRoot))
            {
                DirectoryInfo modDir = new DirectoryInfo(_modRoot);
                if (modDir.Parent != null)
                {
                    _gameModulesRoot = modDir.Parent.FullName;
                }
            }
        }

        public ValidatorForm()
        {
            ResolvePaths();
            this.Text = "DWDN Pre-Flight Mod Validator v1.0 — Мгновенный предпусковой валидатор и авто-исправитель мода";
            this.Size = new Size(1660, 960);
            this.MinimumSize = new Size(1340, 820);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(16, 19, 26);
            this.ForeColor = Color.FromArgb(230, 235, 245);
            this.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            BuildUI();
            RunScanNow();

            if (!string.IsNullOrEmpty(Program.ScreenshotPath))
            {
                var timer = new System.Windows.Forms.Timer { Interval = 900 };
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
            // 1. Top Header Bar
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.FromArgb(12, 14, 20)
            };

            var lblTitle = new Label
            {
                Text = "🛡️ DWDN PRE-FLIGHT MOD VALIDATOR — МГНОВЕННЫЙ ПРЕДПУСКОВОЙ АНАЛИЗАТОР И АВТО-ФИКСЕР",
                Font = new Font("Segoe UI", 11.2f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248),
                Location = new Point(14, 12),
                AutoSize = true
            };
            header.Controls.Add(lblTitle);

            var headerBtns = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 810,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 8, 12, 0),
                BackColor = Color.Transparent
            };

            var btnAutoFixAll = CreateFlatButton("⚡ АВТО-ИСПРАВИТЬ ВСЕ ПРОБЛЕМЫ", Color.FromArgb(217, 119, 6), new Size(245, 31));
            btnAutoFixAll.Margin = new Padding(6, 0, 0, 0);
            btnAutoFixAll.Click += (s, e) => ExecuteAutoFixAll();
            headerBtns.Controls.Add(btnAutoFixAll);

            var btnBuildDll = CreateFlatButton("🔨 Пересобрать DWDN_Script.dll", Color.FromArgb(30, 64, 175), new Size(210, 31));
            btnBuildDll.Margin = new Padding(6, 0, 0, 0);
            btnBuildDll.Click += (s, e) => RebuildModDlls();
            headerBtns.Controls.Add(btnBuildDll);

            var btnSaveReport = CreateFlatButton("📄 Экспорт отчёта", Color.FromArgb(22, 101, 52), new Size(145, 31));
            btnSaveReport.Margin = new Padding(6, 0, 0, 0);
            btnSaveReport.Click += (s, e) => ExportMarkdownReport();
            headerBtns.Controls.Add(btnSaveReport);

            var btnRescan = CreateFlatButton("🔍 Пересканировать", Color.FromArgb(37, 99, 235), new Size(145, 31));
            btnRescan.Margin = new Padding(6, 0, 0, 0);
            btnRescan.Click += (s, e) => RunScanNow();
            headerBtns.Controls.Add(btnRescan);

            header.Controls.Add(headerBtns);

            // 2. KPI Dashboard Bar (under Header)
            var kpiPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 76,
                BackColor = Color.FromArgb(20, 24, 34),
                Padding = new Padding(12, 8, 12, 8)
            };

            _lblKpiErrors = CreateKpiCard(kpiPanel, "🔴 КРИТИЧЕСКИХ ОШИБОК (КРАШ)", "0", Color.FromArgb(239, 68, 68), 12, 235);
            _lblKpiWarnings = CreateKpiCard(kpiPanel, "🟡 ПРЕДУПРЕЖДЕНИЙ (СКРАПКОД)", "0", Color.FromArgb(250, 204, 21), 257, 235);
            _lblKpiPassed = CreateKpiCard(kpiPanel, "🟢 ПРОЙДЕНО ПРОВЕРОК (ОК)", "0", Color.FromArgb(52, 211, 153), 502, 225);
            _lblKpiFixable = CreateKpiCard(kpiPanel, "⚡ ДОСТУПНО АВТО-ФИКСОВ", "0", Color.FromArgb(251, 146, 60), 737, 215);
            _lblKpiSpeed = CreateKpiCard(kpiPanel, "⏱️ ВРЕМЯ И ОХВАТ СКАНА", "0 мс", Color.FromArgb(56, 189, 248), 962, 260);

            _lblVerdictBanner = new Label
            {
                Location = new Point(1232, 8),
                Size = new Size(398, 60),
                BackColor = Color.FromArgb(26, 32, 46),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 10, 0),
                Text = "Готов к проверке..."
            };
            kpiPanel.Controls.Add(_lblVerdictBanner);

            // 3. Bottom Status Bar
            var statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                BackColor = Color.FromArgb(12, 14, 20)
            };
            _lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                ForeColor = Color.FromArgb(148, 163, 184),
                Text = "Готов."
            };
            statusPanel.Controls.Add(_lblStatus);

            // 4. Left Panel (10 Diagnostic Suites & Filters)
            var leftPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 370,
                BackColor = Color.FromArgb(22, 26, 36),
                Padding = new Padding(10)
            };
            BuildLeftPanel(leftPanel);

            // 5. Center + Bottom Inspector Panel
            var mainContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(16, 19, 26),
                Padding = new Padding(8)
            };
            BuildCenterAndBottomPanels(mainContent);

            this.Controls.Add(mainContent);
            this.Controls.Add(leftPanel);
            this.Controls.Add(kpiPanel);
            this.Controls.Add(header);
            this.Controls.Add(statusPanel);
        }

        private void BuildLeftPanel(Panel parent)
        {
            int y = 6;
            parent.Controls.Add(CreateSectionHeader("1. ФИЛЬТР ВАЖНОСТИ И ПОИСК", ref y));

            var filterConfigs = new (int Mode, string Text, Color Col)[]
            {
                (1, "⚠️ Проблемы (Ошибки + Предупр.)", Color.FromArgb(217, 119, 6)),
                (2, "🔴 Только Критичные Ошибки", Color.FromArgb(185, 28, 28)),
                (3, "🟡 Только Предупреждения", Color.FromArgb(161, 98, 7)),
                (0, "🟢 Все проверки (включая ОК)", Color.FromArgb(21, 128, 61))
            };

            for (int i = 0; i < filterConfigs.Length; i++)
            {
                var cfg = filterConfigs[i];
                var btn = CreateFlatButton(cfg.Text, Color.FromArgb(30, 36, 48), new Size(348, 27));
                btn.Location = new Point(10, y);
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.BorderColor = cfg.Col;
                btn.Click += (s, e) =>
                {
                    _severityFilter = cfg.Mode;
                    UpdateFilterButtonStyles();
                    RefreshIssuesListView();
                };
                _filterButtons[i] = btn;
                parent.Controls.Add(btn);
                y += 30;
            }
            y += 4;

            _txtSearch = new TextBox
            {
                Location = new Point(10, y),
                Size = new Size(348, 25),
                BackColor = Color.FromArgb(28, 34, 46),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "🔍 Поиск по ID предмета, классу, файлу или коду..."
            };
            _txtSearch.TextChanged += (s, e) =>
            {
                _searchQuery = _txtSearch.Text.Trim();
                RefreshIssuesListView();
            };
            parent.Controls.Add(_txtSearch);
            y += 33;

            parent.Controls.Add(CreateSectionHeader("2. ДИАГНОСТИЧЕСКИЕ КОНТУРЫ (10 СИСТЕМ)", ref y));

            _lstCategories = new ListBox
            {
                Location = new Point(10, y),
                Size = new Size(348, 520),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.FromArgb(13, 16, 22),
                ForeColor = Color.FromArgb(226, 232, 240),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9.0f)
            };
            _lstCategories.SelectedIndexChanged += (s, e) =>
            {
                if (_lstCategories.SelectedIndex <= 0)
                    _selectedCategory = "ALL";
                else if (_lstCategories.SelectedIndex - 1 < ValidatorEngine.Categories.Length)
                    _selectedCategory = ValidatorEngine.Categories[_lstCategories.SelectedIndex - 1];
                RefreshIssuesListView();
            };
            parent.Controls.Add(_lstCategories);

            UpdateFilterButtonStyles();
        }

        private void BuildCenterAndBottomPanels(Panel parent)
        {
            // Bottom Inspector Panel
            var bottomInspector = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 250,
                BackColor = Color.FromArgb(22, 26, 36),
                Padding = new Padding(10)
            };

            _lblDetailHeader = new Label
            {
                Text = "ИНСПЕКТОР ДИАГНОСТИКИ — выберите строку в таблице выше для подробного разбора и авто-фикса",
                Location = new Point(10, 8),
                Size = new Size(760, 24),
                Font = new Font("Segoe UI", 9.8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248)
            };
            bottomInspector.Controls.Add(_lblDetailHeader);

            var inspectorBtnBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = Color.Transparent
            };

            _btnFixSelected = CreateFlatButton("🔧 АВТО-ИСПРАВИТЬ ВЫБРАННУЮ ПРОБЛЕМУ", Color.FromArgb(217, 119, 6), new Size(300, 30));
            _btnFixSelected.Enabled = false;
            _btnFixSelected.Click += (s, e) => ExecuteAutoFixSingle();
            inspectorBtnBar.Controls.Add(_btnFixSelected);

            _btnOpenFile = CreateFlatButton("📂 Открыть файл проблемы", Color.FromArgb(37, 99, 235), new Size(205, 30));
            _btnOpenFile.Margin = new Padding(0, 0, 8, 0);
            _btnOpenFile.Enabled = false;
            _btnOpenFile.Click += (s, e) => OpenSelectedIssueFile();
            inspectorBtnBar.Controls.Add(_btnOpenFile);

            bottomInspector.Controls.Add(inspectorBtnBar);

            _rtbDetailBody = new RichTextBox
            {
                Location = new Point(10, 44),
                Size = new Size(1245, 195),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.FromArgb(12, 15, 21),
                ForeColor = Color.FromArgb(226, 232, 240),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9.5f),
                ReadOnly = true
            };
            bottomInspector.Controls.Add(_rtbDetailBody);

            // Center ListView
            _lvIssues = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(13, 16, 22),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.0f)
            };
            _lvIssues.Columns.Add("Статус", 105);
            _lvIssues.Columns.Add("Код", 130);
            _lvIssues.Columns.Add("Категория", 215);
            _lvIssues.Columns.Add("Объект / ID", 220);
            _lvIssues.Columns.Add("Файл", 175);
            _lvIssues.Columns.Add("Описание проверки / проблемы", 420);
            _lvIssues.Columns.Add("Авто-фикс", 110);

            _lvIssues.SelectedIndexChanged += (s, e) =>
            {
                if (_lvIssues.SelectedItems.Count > 0 && _lvIssues.SelectedItems[0].Tag is ValidationIssue iss)
                    ShowIssueDetails(iss);
            };
            _lvIssues.DoubleClick += (s, e) => OpenSelectedIssueFile();

            parent.Controls.Add(_lvIssues);
            parent.Controls.Add(bottomInspector);
        }

        private void RunScanNow()
        {
            _lblStatus.Text = "⏳ Сканирование всех XML, C#, 3D-мешей TPAC/FBX и пулов снаряжения DWDN_TRY...";
            Application.DoEvents();

            _currentReport = ValidatorEngine.RunFullScan(_modRoot, _gameModulesRoot);

            // Auto-save Markdown report to output/
            try
            {
                Directory.CreateDirectory(_outDir);
                File.WriteAllText(Path.Combine(_outDir, "PREFLIGHT_VALIDATION_REPORT.md"),
                    ValidatorEngine.GenerateMarkdownReport(_currentReport));
            }
            catch { }

            // Update KPI cards
            _lblKpiErrors.Text = _currentReport.ErrorCount.ToString();
            _lblKpiWarnings.Text = _currentReport.WarningCount.ToString();
            _lblKpiPassed.Text = _currentReport.PassCount.ToString();
            _lblKpiFixable.Text = _currentReport.FixableCount.ToString();
            _lblKpiSpeed.Text = $"{_currentReport.ElapsedMs} мс ({_currentReport.TotalItemsScanned} предм.)";

            if (_currentReport.ErrorCount > 0)
            {
                _lblVerdictBanner.BackColor = Color.FromArgb(127, 29, 29);
                _lblVerdictBanner.ForeColor = Color.FromArgb(254, 202, 202);
                _lblVerdictBanner.Text = $"🚨 ВНИМАНИЕ: НАЙДЕНО ОШИБОК: {_currentReport.ErrorCount} | ПРЕДУПРЕЖДЕНИЙ: {_currentReport.WarningCount}\nРекомендуется нажать «⚡ АВТО-ИСПРАВИТЬ ВСЕ ПРОБЛЕМЫ» перед запуском игры!";
            }
            else if (_currentReport.WarningCount > 0)
            {
                _lblVerdictBanner.BackColor = Color.FromArgb(113, 63, 18);
                _lblVerdictBanner.ForeColor = Color.FromArgb(254, 240, 138);
                _lblVerdictBanner.Text = $"⚠️ КРИТИЧЕСКИХ ОШИБОК НЕТ | ПРЕДУПРЕЖДЕНИЙ: {_currentReport.WarningCount}\nМод запустится, но доступен авто-фикс ({_currentReport.FixableCount} шт.).";
            }
            else
            {
                _lblVerdictBanner.BackColor = Color.FromArgb(20, 83, 45);
                _lblVerdictBanner.ForeColor = Color.FromArgb(187, 247, 208);
                _lblVerdictBanner.Text = "✅ ПОЛНАЯ ГОТОВНОСТЬ К ЗАПУСКУ (0 ОШИБОК, 0 ПРЕДУПРЕЖДЕНИЙ)\nВсе классы, предметы, 3D-меши и DLL синхронизированы!";
            }

            // Populate left categories list with counts
            int prevCatIdx = _lstCategories.SelectedIndex;
            _lstCategories.BeginUpdate();
            _lstCategories.Items.Clear();
            _lstCategories.Items.Add($"[*] ВСЕ СИСТЕМЫ (🔴{_currentReport.ErrorCount} 🟡{_currentReport.WarningCount} 🟢{_currentReport.PassCount})");
            foreach (var cat in ValidatorEngine.Categories)
            {
                int eCnt = _currentReport.Issues.Count(i => i.Category == cat && i.Severity == DiagSeverity.Error);
                int wCnt = _currentReport.Issues.Count(i => i.Category == cat && i.Severity == DiagSeverity.Warning);
                int pCnt = _currentReport.Issues.Count(i => i.Category == cat && i.Severity == DiagSeverity.Pass);
                string badge = eCnt > 0 ? $"🔴{eCnt} " : (wCnt > 0 ? $"🟡{wCnt} " : "🟢OK ");
                _lstCategories.Items.Add($"{badge}{cat} ({eCnt}/{wCnt}/{pCnt})");
            }
            _lstCategories.EndUpdate();

            // If there are no errors/warnings, automatically switch filter to "All (including Pass)" so table isn't empty
            if (_currentReport.ErrorCount == 0 && _currentReport.WarningCount == 0 && _severityFilter == 1)
            {
                _severityFilter = 0;
                UpdateFilterButtonStyles();
            }

            if (prevCatIdx >= 0 && prevCatIdx < _lstCategories.Items.Count)
                _lstCategories.SelectedIndex = prevCatIdx;
            else
                _lstCategories.SelectedIndex = 0;

            RefreshIssuesListView();

            _lblStatus.Text =
                $"✅ Сканирование завершено за {_currentReport.ElapsedMs} мс | Классов: {_currentReport.TotalClassesScanned} | " +
                $"Предметов: {_currentReport.TotalItemsScanned} | Деталей: {_currentReport.TotalPiecesScanned} | " +
                $"3D-мешей: {_currentReport.TotalMeshesScanned} | XML: {_currentReport.TotalXmlFilesScanned} | C#: {_currentReport.TotalCsFilesScanned}";
        }

        private void RefreshIssuesListView()
        {
            _lvIssues.BeginUpdate();
            _lvIssues.Items.Clear();

            var filtered = _currentReport.Issues
                .Where(i => _selectedCategory == "ALL" || i.Category == _selectedCategory)
                .Where(i => _severityFilter switch
                {
                    1 => i.Severity != DiagSeverity.Pass,
                    2 => i.Severity == DiagSeverity.Error,
                    3 => i.Severity == DiagSeverity.Warning,
                    _ => true
                })
                .Where(i => string.IsNullOrEmpty(_searchQuery) ||
                            i.Code.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                            i.TargetId.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                            i.Summary.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                            i.FilePath.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(i => i.Severity)
                .ThenBy(i => i.Category)
                .ToList();

            foreach (var iss in filtered)
            {
                string statusText = iss.Severity switch
                {
                    DiagSeverity.Error => "🔴 КРИТИЧНО",
                    DiagSeverity.Warning => "🟡 ВНИМАНИЕ",
                    _ => "🟢 ОК"
                };

                var lvi = new ListViewItem(statusText);
                lvi.SubItems.Add(iss.Code);
                lvi.SubItems.Add(iss.Category);
                lvi.SubItems.Add(iss.TargetId);
                lvi.SubItems.Add(Path.GetFileName(iss.FilePath) + (iss.LineNumber > 0 ? $":{iss.LineNumber}" : ""));
                lvi.SubItems.Add(iss.Summary);
                lvi.SubItems.Add(iss.CanAutoFix && iss.Severity != DiagSeverity.Pass ? "⚡ Доступен" : "—");
                lvi.Tag = iss;

                if (iss.Severity == DiagSeverity.Error)
                {
                    lvi.BackColor = Color.FromArgb(48, 18, 22);
                    lvi.ForeColor = Color.FromArgb(254, 202, 202);
                }
                else if (iss.Severity == DiagSeverity.Warning)
                {
                    lvi.BackColor = Color.FromArgb(45, 34, 15);
                    lvi.ForeColor = Color.FromArgb(254, 240, 138);
                }
                else
                {
                    lvi.BackColor = Color.FromArgb(14, 24, 20);
                    lvi.ForeColor = Color.FromArgb(167, 243, 208);
                }

                _lvIssues.Items.Add(lvi);
            }

            _lvIssues.EndUpdate();

            if (_lvIssues.Items.Count > 0)
            {
                _lvIssues.Items[0].Selected = true;
            }
            else
            {
                _selectedIssue = null;
                _btnFixSelected.Enabled = false;
                _btnOpenFile.Enabled = false;
                _rtbDetailBody.Text = "По выбранному фильтру записей нет.";
            }
        }

        private void ShowIssueDetails(ValidationIssue iss)
        {
            _selectedIssue = iss;
            _btnOpenFile.Enabled = !string.IsNullOrEmpty(iss.FilePath) && (File.Exists(iss.FilePath) || Directory.Exists(iss.FilePath));
            _btnFixSelected.Enabled = iss.CanAutoFix && iss.Severity != DiagSeverity.Pass;
            _btnFixSelected.BackColor = _btnFixSelected.Enabled ? Color.FromArgb(217, 119, 6) : Color.FromArgb(55, 65, 81);

            string sevTitle = iss.Severity switch
            {
                DiagSeverity.Error => "🔴 КРИТИЧЕСКАЯ ОШИБКА",
                DiagSeverity.Warning => "🟡 ПРЕДУПРЕЖДЕНИЕ",
                _ => "🟢 ПРОВЕРКА ПРОЙДЕНА"
            };

            _lblDetailHeader.Text = $"{sevTitle} [{iss.Code}] — {iss.TargetId}";
            _rtbDetailBody.Text =
                $"📌 ОПИСАНИЕ:   {iss.Summary}\n" +
                $"📂 ФАЙЛ:       {iss.FilePath}" + (iss.LineNumber > 0 ? $" (Строка {iss.LineNumber})" : "") + "\n" +
                $"🔍 ДЕТАЛИ:     {iss.Details}\n" +
                (!string.IsNullOrEmpty(iss.ImpactExplanation) ? $"💥 ВЛИЯНИЕ:    {iss.ImpactExplanation}\n" : "") +
                (!string.IsNullOrEmpty(iss.AutoFixDescription) ? $"⚡ АВТО-ФИКС:  {iss.AutoFixDescription}" : "");
        }

        private void ExecuteAutoFixSingle()
        {
            if (_selectedIssue == null || !_selectedIssue.CanAutoFix) return;
            try
            {
                _selectedIssue.AutoFixAction?.Invoke();
                _lblStatus.Text = $"⚡ Авто-исправление выполнено для: {_selectedIssue.TargetId}";
                RunScanNow();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка авто-исправления: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExecuteAutoFixAll()
        {
            var fixables = _currentReport.Issues.Where(i => i.CanAutoFix && i.Severity != DiagSeverity.Pass).ToList();
            if (fixables.Count == 0)
            {
                MessageBox.Show("Нет проблем, требующих автоматического исправления!", "Авто-исправление", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var listText = string.Join("\n", fixables.Select(f => $"• [{f.Code}] {f.TargetId}: {f.AutoFixDescription}"));
            var confirm = MessageBox.Show(
                $"Выполнить автоматическое исправление всех доступных проблем ({fixables.Count} шт.)?\n\n{listText}",
                "⚡ Пакетное авто-исправление мода",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            int fixedCount = 0;
            foreach (var issue in fixables)
            {
                try
                {
                    issue.AutoFixAction?.Invoke();
                    fixedCount++;
                }
                catch { }
            }

            RunScanNow();
            MessageBox.Show(
                $"Автоматически исправлено проблем: {fixedCount} из {fixables.Count}!\nОтчёт валидации обновлён.",
                "⚡ Авто-исправление завершено",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void RebuildModDlls()
        {
            string csproj = Path.Combine(_modRoot, "ModuleData", "scripts", "DWDN_Script", "DWDN_Script.csproj");
            if (!File.Exists(csproj))
            {
                MessageBox.Show($"Не найден проект: {csproj}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _lblStatus.Text = "🔨 Компиляция DWDN_Script.dll и DWDN_Script.Server.dll...";
            Application.DoEvents();
            ValidatorEngine.CompileModScripts(csproj);
            RunScanNow();
            MessageBox.Show("Сборка DWDN_Script.dll (Client) и DWDN_Script.Server.dll (Server) успешно завершена!", "Сборка завершена", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ExportMarkdownReport()
        {
            try
            {
                Directory.CreateDirectory(_outDir);
                string path = Path.Combine(_outDir, "PREFLIGHT_VALIDATION_REPORT.md");
                File.WriteAllText(path, ValidatorEngine.GenerateMarkdownReport(_currentReport));
                _lblStatus.Text = $"📄 Отчёт сохранён: {path}";
                Process.Start(new ProcessStartInfo { FileName = "notepad.exe", Arguments = $"\"{path}\"", UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения отчёта: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenSelectedIssueFile()
        {
            if (_selectedIssue == null || string.IsNullOrEmpty(_selectedIssue.FilePath)) return;
            if (File.Exists(_selectedIssue.FilePath))
            {
                Process.Start(new ProcessStartInfo { FileName = "notepad.exe", Arguments = $"\"{_selectedIssue.FilePath}\"", UseShellExecute = true });
            }
            else if (Directory.Exists(_selectedIssue.FilePath))
            {
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{_selectedIssue.FilePath}\"", UseShellExecute = true });
            }
        }

        private void UpdateFilterButtonStyles()
        {
            int[] modes = { 1, 2, 3, 0 };
            for (int i = 0; i < _filterButtons.Length; i++)
            {
                bool active = _severityFilter == modes[i];
                _filterButtons[i].BackColor = active ? Color.FromArgb(37, 99, 235) : Color.FromArgb(28, 34, 46);
            }
        }

        private static Label CreateKpiCard(Control parent, string title, string initialValue, Color accent, int x, int w)
        {
            var card = new Panel
            {
                Location = new Point(x, 8),
                Size = new Size(w, 60),
                BackColor = Color.FromArgb(26, 31, 44)
            };

            var topStripe = new Panel
            {
                Dock = DockStyle.Top,
                Height = 3,
                BackColor = accent
            };
            card.Controls.Add(topStripe);

            var lblTitle = new Label
            {
                Text = title,
                Location = new Point(8, 7),
                Size = new Size(w - 14, 16),
                Font = new Font("Segoe UI", 7.8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184)
            };
            card.Controls.Add(lblTitle);

            var lblVal = new Label
            {
                Text = initialValue,
                Location = new Point(8, 24),
                Size = new Size(w - 14, 30),
                Font = new Font("Segoe UI", 14.0f, FontStyle.Bold),
                ForeColor = accent
            };
            card.Controls.Add(lblVal);

            parent.Controls.Add(card);
            return lblVal;
        }

        private static Label CreateSectionHeader(string text, ref int y)
        {
            var lbl = new Label
            {
                Text = text,
                Location = new Point(8, y),
                Size = new Size(350, 20),
                Font = new Font("Segoe UI", 8.6f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248),
                BackColor = Color.FromArgb(28, 34, 48),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0)
            };
            y += 24;
            return lbl;
        }

        private static Button CreateFlatButton(string text, Color backColor, Size size)
        {
            var btn = new Button
            {
                Text = text,
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
    }
}
