using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DWDN_MechanicsTester
{
    public class MainForm : Form
    {
        private readonly TestEngine _engine = new();
        private Button _btnRunAll = null!;
        private Button _btnExportJson = null!;
        private ProgressBar _progressBar = null!;
        private TreeView _treeView = null!;
        private RichTextBox _logBox = null!;
        private Label _lblStatus = null!;
        private Label _lblSummary = null!;
        private TestRunReport? _lastReport;

        public MainForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "DWDN Mechanics & Logic Test Harness (Pre-Flight Engine Simulator)";
            Size = new Size(1100, 720);
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(28, 28, 34);
            ForeColor = Color.FromArgb(230, 230, 235);
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            // Header Panel
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Color.FromArgb(20, 20, 25),
                Padding = new Padding(15, 10, 15, 10)
            };

            var lblTitle = new Label
            {
                Text = "⚡ ПОЛИГОН ПРЕДПУСКОВОГО ТЕСТИРОВАНИЯ МЕХАНИК DWDN",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(243, 156, 18),
                AutoSize = true,
                Location = new Point(15, 10)
            };

            var lblSubtitle = new Label
            {
                Text = "Автономная валидация сетевых пакетов, походной артиллерии, тактических аур и ABI Bannerlord без запуска игры",
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(170, 170, 180),
                AutoSize = true,
                Location = new Point(16, 38)
            };

            _btnRunAll = new Button
            {
                Text = "⚡ ЗАПУСТИТЬ ВСЕ ТЕСТЫ (F5)",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                BackColor = Color.FromArgb(41, 128, 185),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(230, 42),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(headerPanel.Width - 410, 14),
                Cursor = Cursors.Hand
            };
            _btnRunAll.FlatAppearance.BorderSize = 0;
            _btnRunAll.Click += (s, e) => RunTests();

            _btnExportJson = new Button
            {
                Text = "📄 ЭКСПОРТ JSON",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = Color.FromArgb(52, 73, 94),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(150, 42),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(headerPanel.Width - 170, 14),
                Cursor = Cursors.Hand
            };
            _btnExportJson.FlatAppearance.BorderSize = 0;
            _btnExportJson.Click += (s, e) => ExportJson();

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblSubtitle);
            headerPanel.Controls.Add(_btnRunAll);
            headerPanel.Controls.Add(_btnExportJson);

            // Progress Bar
            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 6,
                Style = ProgressBarStyle.Continuous,
                Value = 0
            };

            // Status Bar (Footer)
            var footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 35,
                BackColor = Color.FromArgb(20, 20, 25),
                Padding = new Padding(15, 6, 15, 6)
            };

            _lblStatus = new Label
            {
                Text = "Готов к запуску священных тестов",
                ForeColor = Color.FromArgb(46, 204, 113),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                AutoSize = true,
                Dock = DockStyle.Left
            };

            _lblSummary = new Label
            {
                Text = "Тесты: 0 | Пройдено: 0 | Сбоев: 0 | Время: 0 мс",
                ForeColor = Color.FromArgb(180, 180, 190),
                AutoSize = true,
                Dock = DockStyle.Right
            };

            footerPanel.Controls.Add(_lblStatus);
            footerPanel.Controls.Add(_lblSummary);

            // Split Container (Tree vs Log)
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 440,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(35, 35, 42)
            };

            // TreeView
            _treeView = new TreeView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 24, 29),
                ForeColor = Color.FromArgb(220, 220, 230),
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 10f),
                ItemHeight = 24,
                ShowLines = true,
                ShowPlusMinus = true
            };
            _treeView.AfterSelect += (s, e) =>
            {
                if (e.Node?.Tag is TestCaseResult tc)
                {
                    AppendLog($"\n>>> ДИАГНОСТИКА ТЕСТА: {tc.Name}\nСтатус: {tc.Status}\nСообщение: {tc.Message}\nДлительность: {tc.DurationMs:F2} мс\n" + (string.IsNullOrEmpty(tc.StackTrace) ? "" : $"Стек:\n{tc.StackTrace}\n"), tc.Status == TestStatus.Passed ? Color.LightGreen : Color.OrangeRed);
                }
            };

            // Log Box
            _logBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 18, 22),
                ForeColor = Color.FromArgb(210, 210, 220),
                Font = new Font("Consolas", 9.5f),
                BorderStyle = BorderStyle.None,
                ReadOnly = true
            };

            split.Panel1.Controls.Add(_treeView);
            split.Panel2.Controls.Add(_logBox);

            Controls.Add(split);
            Controls.Add(_progressBar);
            Controls.Add(footerPanel);
            Controls.Add(headerPanel);

            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F5) RunTests();
            };
        }

        private void RunTests()
        {
            _btnRunAll.Enabled = false;
            _btnExportJson.Enabled = false;
            _progressBar.Value = 25;
            _logBox.Clear();
            _treeView.Nodes.Clear();

            _lblStatus.Text = "Выполняется обряд тестирования...";
            _lblStatus.ForeColor = Color.FromArgb(241, 196, 15);

            Action<string> logger = msg =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => AppendLog(msg + Environment.NewLine, Color.FromArgb(220, 220, 230))));
                }
                else
                {
                    AppendLog(msg + Environment.NewLine, Color.FromArgb(220, 220, 230));
                }
            };

            // Фоновый запуск, чтобы UI не зависал
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var report = _engine.RunAllSuites(logger);
                    Invoke(new Action(() => UpdateResults(report)));
                }
                catch (Exception ex)
                {
                    Invoke(new Action(() =>
                    {
                        AppendLog($"\n[ФАТАЛЬНЫЙ СБОЙ ДВИЖКА ТЕСТОВ]: {ex}\n", Color.Red);
                        _lblStatus.Text = "Критический сбой теста!";
                        _lblStatus.ForeColor = Color.Red;
                    }));
                }
                finally
                {
                    Invoke(new Action(() =>
                    {
                        _btnRunAll.Enabled = true;
                        _btnExportJson.Enabled = true;
                        _progressBar.Value = 100;
                    }));
                }
            });
        }

        private void UpdateResults(TestRunReport report)
        {
            _lastReport = report;
            _treeView.BeginUpdate();
            _treeView.Nodes.Clear();

            foreach (var suite in report.Suites)
            {
                string icon = suite.Failed == 0 ? "✅" : "❌";
                var suiteNode = new TreeNode($"{icon} {suite.SuiteName} ({suite.Passed}/{suite.Total})")
                {
                    ForeColor = suite.Failed == 0 ? Color.FromArgb(46, 204, 113) : Color.FromArgb(231, 76, 60)
                };

                foreach (var tc in suite.Results)
                {
                    string tcIcon = tc.Status == TestStatus.Passed ? "✓" : "✗";
                    var tcNode = new TreeNode($"{tcIcon} {tc.Name} [{tc.DurationMs:F1}ms]")
                    {
                        Tag = tc,
                        ForeColor = tc.Status == TestStatus.Passed ? Color.FromArgb(200, 220, 200) : Color.FromArgb(255, 100, 100)
                    };
                    suiteNode.Nodes.Add(tcNode);
                }

                _treeView.Nodes.Add(suiteNode);
                suiteNode.Expand();
            }

            _treeView.EndUpdate();

            if (report.TotalFailed == 0)
            {
                _lblStatus.Text = "✅ ВСЕ СВЯЩЕННЫЕ МЕХАНИКИ ИСПРАВНЫ (СЛАВА ОМНИССИИ)";
                _lblStatus.ForeColor = Color.FromArgb(46, 204, 113);
            }
            else
            {
                _lblStatus.Text = $"❌ ОБНАРУЖЕН СКРАПКОД: {report.TotalFailed} ТЕСТОВ ПРОВАЛЕНО";
                _lblStatus.ForeColor = Color.FromArgb(231, 76, 60);
            }

            _lblSummary.Text = $"Тесты: {report.TotalTests} | Успешно: {report.TotalPassed} | Сбоев: {report.TotalFailed} | Время: {report.TotalDurationMs:F1} мс";
        }

        private void AppendLog(string text, Color color)
        {
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.SelectionLength = 0;
            _logBox.SelectionColor = color;
            _logBox.AppendText(text);
            _logBox.ScrollToCaret();
        }

        private void ExportJson()
        {
            if (_lastReport == null)
            {
                MessageBox.Show("Сначала выполните запуск тестов!", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string json = _engine.ToJson(_lastReport);
            string outPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_report.json");
            File.WriteAllText(outPath, json);

            MessageBox.Show($"Отчет успешно экспортирован:\n{outPath}", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
