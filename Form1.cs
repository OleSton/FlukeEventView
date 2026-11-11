using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FlukeEventView
{
    public enum AppLang
    {
        Ru = 0,
        En = 1
    }

    public partial class Form1 : Form
    {
        private AppLang _lang = AppLang.Ru;

        // Коэффициент масштабирования шрифта (1.0f = 100%, диапазон 0.80f .. 1.60f)
        private float _fontScale = 1.0f;
        private readonly Dictionary<Control, Font> _baseFonts = new Dictionary<Control, Font>();

        private MenuStrip _mainMenu;
        private ToolStripMenuItem _mnuFile, _mnuFileOpen, _mnuFileSave, _mnuFileExport, _mnuFileSaveChart, _mnuFileExit;
        private ToolStripMenuItem _mnuView, _mnuViewStatus, _mnuViewEvents, _mnuViewGraphs, _mnuViewTransient, _mnuViewLog;
        private ToolStripMenuItem _mnuViewFont, _mnuFontInc, _mnuFontDec, _mnuFontReset;
        private ToolStripMenuItem _mnuComm, _mnuCommScan, _mnuCommReload, _mnuCommDownload, _mnuCommSendSetup;
        private ToolStripMenuItem _mnuLang, _mnuLangRu, _mnuLangEn;
        private ToolStripMenuItem _mnuHelp, _mnuHelpAbout;

        private ContextMenuStrip _ctxGridMenu;
        private ToolStripMenuItem _ctxViewTransient;

        private ContextMenuStrip _ctxChartMenu;
        private ToolStripMenuItem _ctxSaveChartImg;

        private ToolStrip _toolBar;
        private ToolStripButton _tsbOpen, _tsbSave, _tsbExport, _tsbRefreshPorts;
        private ToolStripButton _tsbFontDec, _tsbFontReset, _tsbFontInc;
        private ToolStripLabel _tslPort, _tslLang;
        private ComboBox _cmbLang;

        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _lblStatus;
        private ToolStripStatusLabel _lblPortStatus;
        private ToolStripProgressBar _progressBar;
        private ToolTip _toolTip;

        // Главные вкладки
        private TabControl _mainTabs;
        private TabPage _tabStatus;
        private TabPage _tabEvents;
        private TabPage _tabCharts;
        private TabPage _tabLog;

        private SplitContainer _splitStatus;
        private TableLayoutPanel _tblHn, _tblNg, _tblFreq, _tblReal;

        // Группы на вкладке Status
        private GroupBox _grpHn, _grpNg, _grpFreq, _grpOpt, _grpDesc;
        private GroupBox _grpRealTime, _grpRecInfo, _grpButtons;

        // Подписи параметров на левой панели
        private Label _lblHnSwellTitle;
        private Label _lblHnSagTitle;
        private Label _lblHnTransTitle;
        private Label _lblNgSwellTitle;
        private Label _lblNgTransTitle;
        private Label _lblNomFreqTitle;
        private Label _lblMaxFreqTitle;
        private Label _lblMinFreqTitle;

        // Поля ввода уставок
        private NumericUpDown _numHnSwell;
        private NumericUpDown _numHnSag;
        private NumericUpDown _numHnTransient;
        private NumericUpDown _numNgSwell;
        private NumericUpDown _numNgTransient;
        private NumericUpDown _numMaxFreq;
        private NumericUpDown _numMinFreq;
        private RadioButton _rb50Hz, _rb60Hz;
        private RadioButton _rbStopFull, _rbFifo;
        private CheckBox _chkFlashLed;
        private TextBox _txtSiteDesc;

        // Правая колонка: Панель "RealTime" и панель "Recorder Info"
        private Label _lblRealLineTitle, _lblRealGndTitle, _lblRealFreqTitle;
        private Label _lblRealLineVolt;
        private Label _lblRealGndVolt;
        private Label _lblRealFreq;

        private Label _lblInfoSerial;
        private Label _lblInfoEventsCount;
        private Label _lblInfoFromDate;
        private Label _lblInfoToDate;
        private Label _lblInfoFirmware;
        private Label _lblInfoMode;

        // Кнопки управления
        private ComboBox _cmbPorts;
        private Button _btnScanToggle;
        private Button _btnReloadSetup;
        private Button _btnDownloadEvents;
        private Button _btnSendSetupClear;
        private Button _btnDefaultSetup;
        private Button _btnReloadInputsRight;

        // Таблица событий (Events Tab), Графики и Лог
        private DataGridView _gridEvents;
        private Panel _pnlCharts;
        private Label _lblChartStyle;
        private ComboBox _cmbChartType;
        private Button _btnSaveChartImg;
        private Label _lblChartHint;
        private TextBox _txtCommLog;

        // Выбранное событие для отрисовки одиночного импульса
        private EventRecord _selectedTransientEvent = null;

        // Состояние протокола и фонового сканирования
        private SerialPort _serialPort;
        private readonly object _portLock = new object();
        private volatile bool _isScanning = false;
        private volatile bool _isBusyWithCommand = false;
        private bool _initialSetupLoadedFromDevice = false;
        private bool _eventsDownloadedForCurrentSession = false;
        private bool _suppressValidation = false;
        private System.Windows.Forms.Timer _scanTimer;

        // Изначально объект пустой (HasValidData = false), никаких статичных серийных номеров нет
        private SiteData _currentSite = new SiteData();

        public Form1()
        {
            this.KeyPreview = true;
            BuildEventViewInterface();
            CaptureBaseFonts(this);
            InitScanTimer();
            RefreshComPorts();
            ApplyLocalization();
            LogMessage(T("Fluke EventView V2.16 инициализирован.", "Fluke EventView V2.16 initialized."));
            LogMessage(T("Выберите COM-порт и нажмите 'Старт опроса' для подключения к VR101.", "Select COM port and click 'Start Scanning' to connect to VR101."));
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            AdjustStatusSplitterForScale();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                ChangeFontScale(e.Delta > 0 ? 0.10f : -0.10f);
                return;
            }
            base.OnMouseWheel(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control)
            {
                if (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add)
                {
                    ChangeFontScale(0.10f);
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract)
                {
                    ChangeFontScale(-0.10f);
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0)
                {
                    ResetFontScale();
                    e.Handled = true;
                }
            }
            base.OnKeyDown(e);
        }

        private string T(string ru, string en) => _lang == AppLang.Ru ? ru : en;

        private void InitScanTimer()
        {
            _scanTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            _scanTimer.Tick += async (s, e) => await PollRecorderStatusAsync();
        }

        private void BuildEventViewInterface()
        {
            this.Text = "Fluke EventView EV101";
            this.Size = new Size(1420, 820);
            this.MinimumSize = new Size(1240, 740);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("MS Sans Serif", 8.25f);

            _toolTip = new ToolTip
            {
                AutoPopDelay = 15000,
                InitialDelay = 250,
                ReshowDelay = 100,
                ShowAlways = true,
                ToolTipIcon = ToolTipIcon.Info
            };

            // 1. Главное меню
            _mainMenu = new MenuStrip();

            _mnuFile = new ToolStripMenuItem();
            _mnuFileOpen = new ToolStripMenuItem(null, null, OnOpenVerFile);
            _mnuFileSave = new ToolStripMenuItem(null, null, OnSaveVerFile);
            _mnuFileExport = new ToolStripMenuItem(null, null, OnExportCsv);
            _mnuFileSaveChart = new ToolStripMenuItem(null, null, (s, e) => SaveCurrentChartToFile());
            _mnuFileExit = new ToolStripMenuItem(null, null, (s, e) => Close());
            _mnuFile.DropDownItems.AddRange(new ToolStripItem[]
            {
                _mnuFileOpen, _mnuFileSave, _mnuFileExport,
                new ToolStripSeparator(), _mnuFileSaveChart,
                new ToolStripSeparator(), _mnuFileExit
            });

            _mnuView = new ToolStripMenuItem();
            _mnuViewStatus = new ToolStripMenuItem(null, null, (s, e) => _mainTabs.SelectedTab = _tabStatus);
            _mnuViewEvents = new ToolStripMenuItem(null, null, (s, e) => _mainTabs.SelectedTab = _tabEvents);
            _mnuViewGraphs = new ToolStripMenuItem(null, null, (s, e) =>
            {
                if (_cmbChartType.SelectedIndex == 2) _cmbChartType.SelectedIndex = 0;
                _mainTabs.SelectedTab = _tabCharts;
            });
            _mnuViewTransient = new ToolStripMenuItem(null, null, (s, e) => OpenSingleTransientGraphFromSelection());
            _mnuViewLog = new ToolStripMenuItem(null, null, (s, e) => _mainTabs.SelectedTab = _tabLog);

            _mnuViewFont = new ToolStripMenuItem();
            _mnuFontInc = new ToolStripMenuItem(null, null, (s, e) => ChangeFontScale(0.10f));
            _mnuFontDec = new ToolStripMenuItem(null, null, (s, e) => ChangeFontScale(-0.10f));
            _mnuFontReset = new ToolStripMenuItem(null, null, (s, e) => ResetFontScale());
            _mnuViewFont.DropDownItems.AddRange(new ToolStripItem[] { _mnuFontInc, _mnuFontDec, new ToolStripSeparator(), _mnuFontReset });

            _mnuView.DropDownItems.AddRange(new ToolStripItem[]
            {
                _mnuViewStatus, _mnuViewEvents, new ToolStripSeparator(),
                _mnuViewGraphs, _mnuViewTransient, new ToolStripSeparator(),
                _mnuViewFont, new ToolStripSeparator(), _mnuViewLog
            });

            _mnuComm = new ToolStripMenuItem();
            _mnuCommScan = new ToolStripMenuItem(null, null, (s, e) => ToggleScanning());
            _mnuCommReload = new ToolStripMenuItem(null, null, async (s, e) => await ReloadSetupFromDeviceAsync());
            _mnuCommDownload = new ToolStripMenuItem(null, null, async (s, e) => await DownloadEventsFromDeviceAsync());
            _mnuCommSendSetup = new ToolStripMenuItem(null, null, (s, e) => SendSetupAndClearRecorder());
            _mnuComm.DropDownItems.AddRange(new ToolStripItem[] { _mnuCommScan, _mnuCommReload, _mnuCommDownload, new ToolStripSeparator(), _mnuCommSendSetup });

            _mnuLang = new ToolStripMenuItem();
            _mnuLangRu = new ToolStripMenuItem("Русский (RU)", null, (s, e) => SetLanguage(AppLang.Ru));
            _mnuLangEn = new ToolStripMenuItem("English (EN)", null, (s, e) => SetLanguage(AppLang.En));
            _mnuLang.DropDownItems.AddRange(new ToolStripItem[] { _mnuLangRu, _mnuLangEn });

            _mnuHelp = new ToolStripMenuItem();
            _mnuHelpAbout = new ToolStripMenuItem(null, null, (s, e) =>
                MessageBox.Show(
                    T("FLUKE EventView EV101 для Windows\nПрограмма мониторинга и настройки регистратора Fluke VR101 / VR101S.\nПротокол: 9600, N, 8, 1 (Lite-Link).",
                      "FLUKE EventView EV101 for Windows\nFor use with the Fluke Voltage Event Recorder VR101 / VR101S.\nProtocol: 9600, N, 8, 1 (Lite-Link)."),
                    T("О программе EventView EV101", "About EventView EV101"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information));
            _mnuHelp.DropDownItems.Add(_mnuHelpAbout);

            _mainMenu.Items.AddRange(new ToolStripItem[] { _mnuFile, _mnuView, _mnuComm, _mnuLang, _mnuHelp });

            // 2. Панель инструментов (Toolbar)
            _toolBar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(4) };
            _tsbOpen = new ToolStripButton("📂", null, OnOpenVerFile);
            _tsbSave = new ToolStripButton("💾", null, OnSaveVerFile);
            _tsbExport = new ToolStripButton("📊", null, OnExportCsv);
            _toolBar.Items.AddRange(new ToolStripItem[] { _tsbOpen, _tsbSave, _tsbExport, new ToolStripSeparator() });

            _tslPort = new ToolStripLabel();
            _toolBar.Items.Add(_tslPort);

            _cmbPorts = new ComboBox { Width = 85, DropDownStyle = ComboBoxStyle.DropDownList };
            _toolBar.Items.Add(new ToolStripControlHost(_cmbPorts));

            _tsbRefreshPorts = new ToolStripButton("🔄", null, (s, e) => RefreshComPorts());
            _toolBar.Items.Add(_tsbRefreshPorts);
            _toolBar.Items.Add(new ToolStripSeparator());

            _btnScanToggle = new Button { Width = 145, Height = 26, BackColor = Color.LightGray, Font = new Font(this.Font, FontStyle.Bold) };
            _btnScanToggle.Click += (s, e) => ToggleScanning();
            _toolBar.Items.Add(new ToolStripControlHost(_btnScanToggle));

            _btnReloadSetup = new Button { Width = 140, Height = 26 };
            _btnReloadSetup.Click += async (s, e) => await ReloadSetupFromDeviceAsync();
            _toolBar.Items.Add(new ToolStripControlHost(_btnReloadSetup));

            _btnDownloadEvents = new Button { Width = 165, Height = 26, BackColor = Color.LightGreen, Font = new Font(this.Font, FontStyle.Bold) };
            _btnDownloadEvents.Click += async (s, e) =>
            {
                _mainTabs.SelectedTab = _tabEvents;
                await DownloadEventsFromDeviceAsync();
            };
            _toolBar.Items.Add(new ToolStripControlHost(_btnDownloadEvents));

            // Кнопки управления размером шрифта (A-, 100%, A+)
            _toolBar.Items.Add(new ToolStripSeparator());
            _tsbFontDec = new ToolStripButton("🔉 A-", null, (s, e) => ChangeFontScale(-0.10f)) { Font = new Font("MS Sans Serif", 8.5f, FontStyle.Bold) };
            _tsbFontReset = new ToolStripButton("100%", null, (s, e) => ResetFontScale());
            _tsbFontInc = new ToolStripButton("🔊 A+", null, (s, e) => ChangeFontScale(0.10f)) { Font = new Font("MS Sans Serif", 8.5f, FontStyle.Bold) };
            _toolBar.Items.AddRange(new ToolStripItem[] { _tsbFontDec, _tsbFontReset, _tsbFontInc });

            // Переключатель языка
            _toolBar.Items.Add(new ToolStripSeparator());
            _tslLang = new ToolStripLabel("🌐");
            _toolBar.Items.Add(_tslLang);

            _cmbLang = new ComboBox { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbLang.Items.AddRange(new object[] { "Русский (RU)", "English (EN)" });
            _cmbLang.SelectedIndex = 0;
            _cmbLang.SelectedIndexChanged += (s, e) =>
            {
                SetLanguage(_cmbLang.SelectedIndex == 0 ? AppLang.Ru : AppLang.En);
            };
            _toolBar.Items.Add(new ToolStripControlHost(_cmbLang));

            // 3. Статус-бар
            _statusStrip = new StatusStrip();
            _lblStatus = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _progressBar = new ToolStripProgressBar { Width = 160, Visible = false };
            _lblPortStatus = new ToolStripStatusLabel { BorderSides = ToolStripStatusLabelBorderSides.Left };
            _statusStrip.Items.AddRange(new ToolStripItem[] { _lblStatus, _progressBar, _lblPortStatus });

            // 4. Вкладки Site Report Window
            _mainTabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("MS Sans Serif", 9f, FontStyle.Regular) };
            _tabStatus = new TabPage();
            _tabEvents = new TabPage();
            _tabCharts = new TabPage();
            _tabLog = new TabPage();

            BuildStatusTab(_tabStatus);
            BuildEventsTab(_tabEvents);
            BuildChartsTab(_tabCharts);
            BuildLogTab(_tabLog);

            _mainTabs.TabPages.AddRange(new TabPage[] { _tabStatus, _tabEvents, _tabCharts, _tabLog });

            _mainTabs.SelectedIndexChanged += async (s, e) =>
            {
                if (_mainTabs.SelectedTab == _tabEvents && _isScanning && !_eventsDownloadedForCurrentSession)
                {
                    await DownloadEventsFromDeviceAsync();
                }
                else if (_mainTabs.SelectedTab == _tabCharts)
                {
                    _pnlCharts.Invalidate();
                }
            };

            this.Controls.Add(_mainTabs);
            this.Controls.Add(_toolBar);
            this.Controls.Add(_mainMenu);
            this.Controls.Add(_statusStrip);
            this.MainMenuStrip = _mainMenu;
        }

        private void BuildStatusTab(TabPage page)
        {
            _splitStatus = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 740,
                FixedPanel = FixedPanel.Panel1,
                Padding = new Padding(6)
            };

            var pnlLeft = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(6) };

            // 1. Hot to Neutral Thresholds (H-N)
            _grpHn = new GroupBox { Dock = DockStyle.Top, Height = 132, Padding = new Padding(10) };
            _tblHn = CreateSettingsTable(3);
            _numHnSwell = CreateValidatedNum(100, 300, 256, 2, 0);
            _numHnSag = CreateValidatedNum(60, 240, 210, 2, 0);
            _numHnTransient = CreateValidatedNum(80, 2500, 200, 10, 0);

            _lblHnSwellTitle = AddRowWithLabelRef(_tblHn, 0, "", _numHnSwell);
            _lblHnSagTitle = AddRowWithLabelRef(_tblHn, 1, "", _numHnSag);
            _lblHnTransTitle = AddRowWithLabelRef(_tblHn, 2, "", _numHnTransient);
            _grpHn.Controls.Add(_tblHn);

            // 2. Neutral to Ground Thresholds (N-G)
            _grpNg = new GroupBox { Dock = DockStyle.Top, Height = 100, Padding = new Padding(10) };
            _tblNg = CreateSettingsTable(2);
            _numNgSwell = CreateValidatedNum(1, 250, 10, 1, 0);
            _numNgTransient = CreateValidatedNum(80, 2500, 100, 10, 0);

            _lblNgSwellTitle = AddRowWithLabelRef(_tblNg, 0, "", _numNgSwell);
            _lblNgTransTitle = AddRowWithLabelRef(_tblNg, 1, "", _numNgTransient);
            _grpNg.Controls.Add(_tblNg);

            // 3. Frequency Thresholds
            _grpFreq = new GroupBox { Dock = DockStyle.Top, Height = 132, Padding = new Padding(10) };
            _tblFreq = CreateSettingsTable(3);

            var pnlHz = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
            _rb50Hz = new RadioButton { Text = "50 Hz", Checked = true, AutoSize = true, Margin = new Padding(0, 2, 16, 0) };
            _rb60Hz = new RadioButton { Text = "60 Hz", AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
            _rb50Hz.CheckedChanged += (s, e) =>
            {
                if (_suppressValidation) return;
                ConfigureHardwareLimitsAndTooltips(_currentSite.Is220V, _rb50Hz.Checked, _currentSite.NgScale025);
                if (_rb50Hz.Checked && _numMinFreq.Value > 55m)
                {
                    SetSafeNum(_numMinFreq, 48.8m);
                    SetSafeNum(_numMaxFreq, 51.2m);
                }
                else if (_rb60Hz.Checked && _numMinFreq.Value < 55m)
                {
                    SetSafeNum(_numMinFreq, 58.8m);
                    SetSafeNum(_numMaxFreq, 61.2m);
                }
            };
            pnlHz.Controls.AddRange(new Control[] { _rb50Hz, _rb60Hz });

            _numMaxFreq = CreateValidatedNum(50.1m, 55.0m, 51.2m, 0.1m, 1);
            _numMinFreq = CreateValidatedNum(45.0m, 49.9m, 48.8m, 0.1m, 1);

            _lblNomFreqTitle = AddRowWithLabelRef(_tblFreq, 0, "", pnlHz);
            _lblMaxFreqTitle = AddRowWithLabelRef(_tblFreq, 1, "", _numMaxFreq);
            _lblMinFreqTitle = AddRowWithLabelRef(_tblFreq, 2, "", _numMinFreq);
            _grpFreq.Controls.Add(_tblFreq);

            // 4. Options
            _grpOpt = new GroupBox { Dock = DockStyle.Top, Height = 112, Padding = new Padding(10) };
            var pnlOpt = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            _rbStopFull = new RadioButton { AutoSize = true };
            _rbFifo = new RadioButton { Checked = true, AutoSize = true };
            _chkFlashLed = new CheckBox { Checked = true, AutoSize = true };
            pnlOpt.Controls.AddRange(new Control[] { _rbStopFull, _rbFifo, _chkFlashLed });
            _grpOpt.Controls.Add(pnlOpt);

            // 5. Site Description (по умолчанию пусто до чтения прибора или ввода пользователем)
            _grpDesc = new GroupBox { Dock = DockStyle.Top, Height = 72, Padding = new Padding(10) };
            _txtSiteDesc = new TextBox { Text = "", MaxLength = 30, Dock = DockStyle.Top };
            _grpDesc.Controls.Add(_txtSiteDesc);

            pnlLeft.Controls.Add(_grpDesc);
            pnlLeft.Controls.Add(_grpOpt);
            pnlLeft.Controls.Add(_grpFreq);
            pnlLeft.Controls.Add(_grpNg);
            pnlLeft.Controls.Add(_grpHn);
            _splitStatus.Panel1.Controls.Add(pnlLeft);

            // --- ПРАВАЯ ПАНЕЛЬ: REALTIME, RECORDER INFO И КНОПКИ ---
            var pnlRight = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(6) };

            // 1. Панель "RealTime"
            _grpRealTime = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 148,
                Padding = new Padding(10),
                ForeColor = Color.DarkRed
            };
            _tblReal = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
            _tblReal.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            _tblReal.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int r = 0; r < 3; r++) _tblReal.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3f));

            var rtFont = new Font("Consolas", 12f, FontStyle.Bold);
            _lblRealLineVolt = new Label { Text = "---", AutoSize = true, Font = rtFont, ForeColor = Color.DarkGreen, Anchor = AnchorStyles.Left };
            _lblRealGndVolt = new Label { Text = "---", AutoSize = true, Font = rtFont, ForeColor = Color.Teal, Anchor = AnchorStyles.Left };
            _lblRealFreq = new Label { Text = "---", AutoSize = true, Font = rtFont, ForeColor = Color.DarkBlue, Anchor = AnchorStyles.Left };

            _lblRealLineTitle = AddRowWithLabelRef(_tblReal, 0, "", _lblRealLineVolt);
            _lblRealGndTitle = AddRowWithLabelRef(_tblReal, 1, "", _lblRealGndVolt);
            _lblRealFreqTitle = AddRowWithLabelRef(_tblReal, 2, "", _lblRealFreq);
            _grpRealTime.Controls.Add(_tblReal);

            // 2. Панель "Recorder Info"
            _grpRecInfo = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 205,
                Padding = new Padding(12),
                ForeColor = Color.Red
            };
            var pnlInfoFlow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
            var infoFont = new Font("MS Sans Serif", 9.5f, FontStyle.Regular);
            var boldFont = new Font("MS Sans Serif", 10f, FontStyle.Bold);

            _lblInfoSerial = new Label { AutoSize = true, Font = boldFont, ForeColor = Color.Black };
            _lblInfoEventsCount = new Label { AutoSize = true, Font = boldFont, ForeColor = Color.DarkBlue };
            _lblInfoFromDate = new Label { AutoSize = true, Font = infoFont, ForeColor = Color.Black };
            _lblInfoToDate = new Label { AutoSize = true, Font = infoFont, ForeColor = Color.Black };
            _lblInfoFirmware = new Label { AutoSize = true, Font = infoFont, ForeColor = Color.Black };
            _lblInfoMode = new Label { AutoSize = true, Font = infoFont, ForeColor = Color.DimGray };

            pnlInfoFlow.Controls.Add(_lblInfoSerial, 0, 0);
            pnlInfoFlow.Controls.Add(_lblInfoEventsCount, 0, 1);
            pnlInfoFlow.Controls.Add(_lblInfoFromDate, 0, 2);
            pnlInfoFlow.Controls.Add(_lblInfoToDate, 0, 3);
            pnlInfoFlow.Controls.Add(_lblInfoFirmware, 0, 4);
            pnlInfoFlow.Controls.Add(_lblInfoMode, 0, 5);
            _grpRecInfo.Controls.Add(pnlInfoFlow);

            // 3. Кнопки управления уставками
            _grpButtons = new GroupBox { Dock = DockStyle.Top, Height = 175, Padding = new Padding(12) };
            var tblBtns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            tblBtns.RowStyles.Add(new RowStyle(SizeType.Percent, 36));
            tblBtns.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
            tblBtns.RowStyles.Add(new RowStyle(SizeType.Percent, 32));

            _btnSendSetupClear = new Button
            {
                Dock = DockStyle.Fill,
                Font = new Font("MS Sans Serif", 9.5f, FontStyle.Bold),
                BackColor = Color.FromArgb(255, 225, 225)
            };
            _btnSendSetupClear.Click += (s, e) => SendSetupAndClearRecorder();

            _btnReloadInputsRight = new Button { Dock = DockStyle.Fill };
            _btnReloadInputsRight.Click += async (s, e) => await ReloadSetupFromDeviceAsync();

            _btnDefaultSetup = new Button { Dock = DockStyle.Fill };
            _btnDefaultSetup.Click += (s, e) => ApplyDefaultSetupToInputs();

            tblBtns.Controls.Add(_btnSendSetupClear, 0, 0);
            tblBtns.Controls.Add(_btnReloadInputsRight, 0, 1);
            tblBtns.Controls.Add(_btnDefaultSetup, 0, 2);
            _grpButtons.Controls.Add(tblBtns);

            pnlRight.Controls.Add(_grpButtons);
            pnlRight.Controls.Add(_grpRecInfo);
            pnlRight.Controls.Add(_grpRealTime);
            _splitStatus.Panel2.Controls.Add(pnlRight);

            page.Controls.Add(_splitStatus);
        }

        private static TableLayoutPanel CreateSettingsTable(int rowCount)
        {
            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = rowCount
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 450));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < rowCount; i++)
                tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rowCount));
            return tbl;
        }

        private void BuildEventsTab(TabPage page)
        {
            _gridEvents = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                Font = new Font("MS Sans Serif", 9.5f)
            };
            _gridEvents.Columns.Add("EventIdx", "Event");
            _gridEvents.Columns[0].FillWeight = 45;
            _gridEvents.Columns.Add("StartTime", "Start Time");
            _gridEvents.Columns[1].FillWeight = 120;
            _gridEvents.Columns.Add("EventName", "Event");
            _gridEvents.Columns[2].FillWeight = 120;
            _gridEvents.Columns.Add("Extreme", "Extreme");
            _gridEvents.Columns[3].FillWeight = 85;
            _gridEvents.Columns.Add("End", "End");
            _gridEvents.Columns[4].FillWeight = 100;
            _gridEvents.Columns.Add("RawHex", "Raw 8 Bytes (HEX)");
            _gridEvents.Columns[5].FillWeight = 110;

            _ctxGridMenu = new ContextMenuStrip();
            _ctxViewTransient = new ToolStripMenuItem(null, null, (s, e) => OpenSingleTransientGraphFromSelection());
            _ctxGridMenu.Items.Add(_ctxViewTransient);
            _gridEvents.ContextMenuStrip = _ctxGridMenu;

            _gridEvents.SelectionChanged += (s, e) =>
            {
                var ev = GetSelectedEventRecord();
                if (ev != null && ev.IsSingleTransient)
                {
                    _selectedTransientEvent = ev;
                }
            };

            _gridEvents.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    OpenSingleTransientGraphFromSelection();
                }
            };

            page.Controls.Add(_gridEvents);
        }

        private void BuildChartsTab(TabPage page)
        {
            var pnlChartTop = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(6, 6, 6, 4),
                BackColor = Color.FromArgb(240, 244, 248),
                WrapContents = false,
                AutoScroll = true
            };

            _lblChartStyle = new Label
            {
                AutoSize = true,
                Margin = new Padding(4, 6, 6, 0),
                Font = new Font("MS Sans Serif", 8.5f, FontStyle.Bold)
            };

            _cmbChartType = new ComboBox
            {
                Width = 370,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 2, 12, 0)
            };
            _cmbChartType.Items.AddRange(new string[] { "1", "2", "3" });
            _cmbChartType.SelectedIndex = 0;
            _cmbChartType.SelectedIndexChanged += (s, e) => _pnlCharts.Invalidate();

            _btnSaveChartImg = new Button
            {
                Width = 195,
                Height = 27,
                BackColor = Color.FromArgb(225, 242, 255),
                Font = new Font("MS Sans Serif", 8.5f, FontStyle.Bold),
                Margin = new Padding(0, 1, 14, 0)
            };
            _btnSaveChartImg.Click += (s, e) => SaveCurrentChartToFile();

            _lblChartHint = new Label
            {
                AutoSize = true,
                Margin = new Padding(4, 6, 0, 0),
                ForeColor = Color.DarkSlateBlue
            };

            pnlChartTop.Controls.AddRange(new Control[] { _lblChartStyle, _cmbChartType, _btnSaveChartImg, _lblChartHint });

            _pnlCharts = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            typeof(Panel).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, _pnlCharts, new object[] { true });

            _ctxChartMenu = new ContextMenuStrip();
            _ctxSaveChartImg = new ToolStripMenuItem(null, null, (s, e) => SaveCurrentChartToFile());
            _ctxChartMenu.Items.Add(_ctxSaveChartImg);
            _pnlCharts.ContextMenuStrip = _ctxChartMenu;

            _pnlCharts.Paint += DrawChartsGraphics;
            _pnlCharts.Resize += (s, e) => _pnlCharts.Invalidate();

            page.Controls.Add(_pnlCharts);
            page.Controls.Add(pnlChartTop);
        }

        private void BuildLogTab(TabPage page)
        {
            _txtCommLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                BackColor = Color.FromArgb(20, 25, 30),
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 9.5f)
            };
            page.Controls.Add(_txtCommLog);
        }

        // =========================================================================
        // МАСШТАБИРОВАНИЕ ШРИФТА ПРИЛОЖЕНИЯ (A+, A-, 100%)
        // =========================================================================
        private void CaptureBaseFonts(Control root)
        {
            if (!_baseFonts.ContainsKey(root) && root.Font != null)
            {
                _baseFonts[root] = root.Font;
            }
            foreach (Control child in root.Controls)
            {
                CaptureBaseFonts(child);
            }
        }

        private void ChangeFontScale(float delta)
        {
            float target = (float)Math.Round(_fontScale + delta, 2);
            target = Math.Max(0.80f, Math.Min(1.60f, target));
            if (Math.Abs(target - _fontScale) < 0.01f) return;

            _fontScale = target;
            ApplyFontScale();
        }

        private void ResetFontScale()
        {
            if (Math.Abs(_fontScale - 1.0f) < 0.01f) return;
            _fontScale = 1.0f;
            ApplyFontScale();
        }

        private void ApplyFontScale()
        {
            this.SuspendLayout();
            try
            {
                foreach (var kvp in _baseFonts)
                {
                    Control ctrl = kvp.Key;
                    if (ctrl.IsDisposed) continue;
                    Font baseF = kvp.Value;
                    float newSize = Math.Max(7.0f, Math.Min(22.0f, baseF.Size * _fontScale));
                    ctrl.Font = new Font(baseF.FontFamily, newSize, baseF.Style);
                }

                if (_gridEvents != null)
                {
                    float gridFontSize = Math.Max(8.0f, 9.5f * _fontScale);
                    _gridEvents.Font = new Font("MS Sans Serif", gridFontSize, FontStyle.Regular);
                    _gridEvents.ColumnHeadersDefaultCellStyle.Font = new Font("MS Sans Serif", gridFontSize, FontStyle.Bold);
                    _gridEvents.RowTemplate.Height = (int)Math.Round(24 * _fontScale);
                    foreach (DataGridViewRow row in _gridEvents.Rows)
                    {
                        row.Height = _gridEvents.RowTemplate.Height;
                    }
                }

                int labelColWidth = (int)Math.Round(450 * Math.Max(1.0f, _fontScale * 0.96f));
                if (_tblHn != null) _tblHn.ColumnStyles[0].Width = labelColWidth;
                if (_tblNg != null) _tblNg.ColumnStyles[0].Width = labelColWidth;
                if (_tblFreq != null) _tblFreq.ColumnStyles[0].Width = labelColWidth;
                if (_tblReal != null) _tblReal.ColumnStyles[0].Width = (int)Math.Round(240 * Math.Max(1.0f, _fontScale));

                if (_grpHn != null) _grpHn.Height = (int)Math.Round(132 * Math.Max(1.0f, _fontScale * 0.95f));
                if (_grpNg != null) _grpNg.Height = (int)Math.Round(100 * Math.Max(1.0f, _fontScale * 0.95f));
                if (_grpFreq != null) _grpFreq.Height = (int)Math.Round(132 * Math.Max(1.0f, _fontScale * 0.95f));
                if (_grpOpt != null) _grpOpt.Height = (int)Math.Round(112 * Math.Max(1.0f, _fontScale * 0.98f));
                if (_grpDesc != null) _grpDesc.Height = (int)Math.Round(72 * Math.Max(1.0f, _fontScale * 0.98f));
                if (_grpRealTime != null) _grpRealTime.Height = (int)Math.Round(148 * Math.Max(1.0f, _fontScale * 0.95f));
                if (_grpRecInfo != null) _grpRecInfo.Height = (int)Math.Round(205 * Math.Max(1.0f, _fontScale));
                if (_grpButtons != null) _grpButtons.Height = (int)Math.Round(175 * Math.Max(1.0f, _fontScale * 0.95f));

                AdjustStatusSplitterForScale();

                int pct = (int)Math.Round(_fontScale * 100);
                _tsbFontReset.Text = $"{pct}%";
                _lblStatus.Text = T(
                    $"Масштаб шрифта интерфейса: {pct}% (Ctrl + Колесо мыши или кнопки A+ / A-).",
                    $"Interface font scale: {pct}% (Ctrl + Mouse Wheel or A+ / A- buttons).");

                _pnlCharts?.Invalidate();
            }
            finally
            {
                this.ResumeLayout(true);
            }
        }

        private void AdjustStatusSplitterForScale()
        {
            if (_splitStatus == null || _splitStatus.Width < 800) return;
            int targetLeftWidth = (int)Math.Round(740 * Math.Max(1.0f, _fontScale * 0.95f));
            targetLeftWidth = Math.Min(_splitStatus.Width - 380, Math.Max(680, targetLeftWidth));
            _splitStatus.SplitterDistance = targetLeftWidth;
        }

        // =========================================================================
        // СОХРАНЕНИЕ ГРАФИКА В ГРАФИЧЕСКИЙ ФАЙЛ (PNG / BMP / JPEG)
        // =========================================================================
        private void SaveCurrentChartToFile()
        {
            if (!_currentSite.HasValidData || _currentSite.Events.Count == 0)
            {
                MessageBox.Show(
                    T("Нет данных для сохранения! Сначала загрузите события из прибора или откройте файл .VER.",
                      "No events to plot/save! Download events from recorder or open a .VER file first."),
                    "Fluke EventView", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string modeSuffix = _cmbChartType.SelectedIndex == 0
                ? "Summary"
                : (_cmbChartType.SelectedIndex == 1 ? "Distribution" : $"Transient_Ev{_selectedTransientEvent?.Index ?? 1}");

            using (var sfd = new SaveFileDialog
            {
                Title = T("Сохранить график как изображение", "Save Graph as Image"),
                Filter = "PNG Image (*.png)|*.png|Windows Bitmap (*.bmp)|*.bmp|JPEG Image (*.jpg)|*.jpg",
                FileName = $"SN{_currentSite.SerialNumber}_{modeSuffix}.png"
            })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        int exportW = Math.Max(1280, _pnlCharts.Width);
                        int exportH = Math.Max(800, _pnlCharts.Height);

                        using (Bitmap bmp = new Bitmap(exportW, exportH, PixelFormat.Format24bppRgb))
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.Clear(Color.White);
                            RenderChartContent(g, new Rectangle(0, 0, exportW, exportH));

                            string ext = Path.GetExtension(sfd.FileName)?.ToLowerInvariant();
                            ImageFormat fmt = ImageFormat.Png;
                            if (ext == ".bmp") fmt = ImageFormat.Bmp;
                            else if (ext == ".jpg" || ext == ".jpeg") fmt = ImageFormat.Jpeg;

                            bmp.Save(sfd.FileName, fmt);
                        }

                        LogMessage($"Saved chart image to \"{sfd.FileName}\".");
                        _lblStatus.Text = T($"График успешно сохранен в файл: {sfd.FileName}", $"Graph image saved to: {sfd.FileName}");
                        MessageBox.Show(
                            T($"Изображение графика успешно сохранено:\n{sfd.FileName}",
                              $"Graph image successfully saved:\n{sfd.FileName}"),
                            "Fluke EventView", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, T("Ошибка сохранения изображения", "Save Image Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        // =========================================================================
        // ПОЛУЧЕНИЕ ВЫБРАННОГО СОБЫТИЯ И ПЕРЕХОД К ОСЦИЛЛОГРАММЕ ОДИНОЧНОГО ИМПУЛЬСА
        // =========================================================================
        private EventRecord GetSelectedEventRecord()
        {
            if (_gridEvents.SelectedRows.Count == 0) return null;
            return _gridEvents.SelectedRows[0].Tag as EventRecord;
        }

        private void OpenSingleTransientGraphFromSelection()
        {
            var ev = GetSelectedEventRecord();
            if (ev == null)
            {
                ev = _currentSite.Events.LastOrDefault(x => x.IsSingleTransient);
                if (ev == null)
                {
                    MessageBox.Show(
                        T("Сначала выберите событие одиночного импульса (1 H-N Transient или 1 N-G Transient) в таблице событий!",
                          "Select a single transient event (1 H-N Transient or 1 N-G Transient) in the Events table first!"),
                        "Fluke EventView", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            if (ev.IsOpenEvent)
            {
                MessageBox.Show(
                    T("Невозможно построить осциллограмму для незавершенного события (Open Event)!",
                      "Unable to Graph Open Events!"),
                    "Fluke EventView", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!ev.IsTransient)
            {
                MessageBox.Show(
                    T("Выбранная запись не является высоковольтным импульсом (Transient)!\nОсциллограмма строится только для импульсных помех.",
                      "This is not a true Transient event!\nTransient graph is only possible on a single transient event!"),
                    "Fluke EventView", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!ev.IsSingleTransient)
            {
                MessageBox.Show(
                    T($"Невозможно построить график для пачки из нескольких импульсов ({ev.TransientCount} шт.)!\nОсциллограмма доступна только для одиночного импульса (1 Transient).",
                      "Cannot graph multiple Transients!\nTransient graph is only possible on a single transient event!"),
                    "Fluke EventView", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _selectedTransientEvent = ev;
            _cmbChartType.SelectedIndex = 2;
            _mainTabs.SelectedTab = _tabCharts;
            _pnlCharts.Invalidate();
        }

        // =========================================================================
        // МУЛЬТИЯЗЫЧНАЯ ЛОКАЛИЗАЦИЯ (RU / EN)
        // =========================================================================
        private void SetLanguage(AppLang newLang)
        {
            if (_lang == newLang) return;
            _lang = newLang;
            if (_cmbLang.SelectedIndex != (int)_lang)
                _cmbLang.SelectedIndex = (int)_lang;

            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            _mnuLangRu.Checked = (_lang == AppLang.Ru);
            _mnuLangEn.Checked = (_lang == AppLang.En);

            this.Text = (_currentSite.HasValidData && _currentSite.SerialNumber > 0)
                ? $"Fluke EventView EV101 - [SN F{_currentSite.SerialNumber}]"
                : T("Fluke EventView EV101 - [Нет активного соединения с прибором]", "Fluke EventView EV101 - [No Active Device Session]");

            _toolTip.ToolTipTitle = T("Аппаратные ограничения VR101", "VR101 Hardware Limits");

            // Меню Файл
            _mnuFile.Text = T("&Файл", "&File");
            _mnuFileOpen.Text = T("&Открыть файл (.VER)...", "&Open Site File (.VER)...");
            _mnuFileSave.Text = T("&Сохранить файл (.VER)...", "&Save Site File (.VER)...");
            _mnuFileExport.Text = T("Экспорт таблицы в &CSV / ASCII...", "Save &As CSV / ASCII...");
            _mnuFileSaveChart.Text = T("🖼️ Сохранить &график как картинку (.PNG / .BMP)...", "🖼️ Save &Graph as Image (.PNG / .BMP)...");
            _mnuFileExit.Text = T("В&ыход", "E&xit");

            // Меню Вид
            _mnuView.Text = T("&Вид", "&View");
            _mnuViewStatus.Text = T("Вкладка &Статус и Настройки", "&Status Tab");
            _mnuViewEvents.Text = T("Вкладка &Таблица событий", "&Events Tab");
            _mnuViewGraphs.Text = T("&Сводные графики (Quick Summary / Distribution)...", "&Quick Summary / Graphs...");
            _mnuViewTransient.Text = T("Осциллограмма &одиночного импульса (Single Transient Graph)...", "View &Single Transient as a Graph...");
            _mnuViewFont.Text = T("🔍 Размер &шрифта интерфейса", "🔍 Interface &Font Size");
            _mnuFontInc.Text = T("Увеличить шрифт (A+ / Ctrl+Плюс)", "Increase Font Size (A+ / Ctrl+Plus)");
            _mnuFontDec.Text = T("Уменьшить шрифт (A- / Ctrl+Минус)", "Decrease Font Size (A- / Ctrl+Minus)");
            _mnuFontReset.Text = T("Сбросить масштаб (100% / Ctrl+0)", "Reset Font Size (100% / Ctrl+0)");
            _mnuViewLog.Text = T("Вкладка &Лог протокола", "&Message Log...");

            _ctxViewTransient.Text = T("⚡ Показать осциллограмму одиночного импульса (Single Transient Graph)", "⚡ View Single Transient as a Graph");
            _ctxSaveChartImg.Text = T("🖼️ Сохранить этот график в файл (.PNG / .BMP / .JPG)...", "🖼️ Save Graph Image to File (.PNG / .BMP / .JPG)...");

            // Меню Связь
            _mnuComm.Text = T("&Связь", "&Communicate");
            _mnuCommScan.Text = T("Вкл / Выкл &опрос прибора", "Toggle &Scanning (Connect / Disconnect)");
            _mnuCommReload.Text = T("&Прочитать уставки из прибора", "&Reload Setup from Recorder");
            _mnuCommDownload.Text = T("&Загрузить события сейчас", "&Download Events Now");
            _mnuCommSendSetup.Text = T("Записать уставки / &Очистить память...", "Send Setup / &Clear Recorder...");

            _mnuLang.Text = T("&Язык (Language)", "&Language");
            _mnuHelp.Text = T("&Справка", "&Help");
            _mnuHelpAbout.Text = T("&О программе EventView EV101...", "&About EventView EV101...");

            // Панель инструментов
            _tsbOpen.Text = T("📂 Открыть .VER", "📂 Open .VER");
            _tsbSave.Text = T("💾 Сохранить .VER", "💾 Save .VER");
            _tsbExport.Text = T("📊 Экспорт CSV", "📊 Export CSV");
            _tslPort.Text = T("Порт:", "COM Port:");
            _tsbRefreshPorts.ToolTipText = T("Обновить список COM-портов", "Refresh COM Ports");
            _btnReloadSetup.Text = T("🔄 Прочитать уставки", "🔄 Read Setup");
            _btnDownloadEvents.Text = T("📥 Загрузить события", "📥 Download Events");

            _tsbFontDec.ToolTipText = T("Уменьшить размер шрифта (Ctrl + Минус / Ctrl + Колесо мыши)", "Decrease font size (Ctrl + Minus / Ctrl + Mouse Wheel)");
            _tsbFontReset.ToolTipText = T("Сбросить размер шрифта к 100% (Ctrl + 0)", "Reset font size to 100% (Ctrl + 0)");
            _tsbFontInc.ToolTipText = T("Увеличить размер шрифта (Ctrl + Плюс / Ctrl + Колесо мыши)", "Increase font size (Ctrl + Plus / Ctrl + Mouse Wheel)");
            _tslLang.Text = T("🌐 Язык:", "🌐 Lang:");

            // Вкладки
            _tabStatus.Text = T("Статус и Уставки (Status)", "Status (Setup & RealTime)");
            _tabEvents.Text = T("События (Events)", "Events (SiteGrid)");
            _tabCharts.Text = T("Графики (Graphs)", "Graphs (Quick Summary)");
            _tabLog.Text = T("Лог обмена (Protocol Log)", "Protocol Log");

            // Группы и элементы на вкладке Status
            _grpHn.Text = T("Пороги Фаза — Нейтраль (Hot to Neutral Thresholds H-N)", "Hot to Neutral Thresholds (H-N)");
            _grpNg.Text = T("Пороги Нейтраль — Земля (Neutral to Ground Thresholds N-G)", "Neutral to Ground Thresholds (N-G)");
            _grpFreq.Text = T("Пороги частоты сети (Frequency Thresholds)", "Frequency Thresholds");
            _grpOpt.Text = T("Режим памяти и индикации (Options)", "Options");
            _grpDesc.Text = T("Описание точки контроля (до 30 символов ASCII)", "Site Description (up to 30 ASCII characters)");

            _lblNomFreqTitle.Text = T("Номинальная частота сети:", "Nominal Line Frequency:");
            _rbStopFull.Text = T("Остановить запись при заполнении буфера (Stop When Full)", "Stop Recording When Full");
            _rbFifo.Text = T("Перезаписывать старые события (кольцевой буфер FIFO)", "Overwrite Oldest Events When Full");
            _chkFlashLed.Text = T("Мигать светодиодом при регистрации событий (Flash LED)", "Flash when Events Captured");

            _toolTip.SetToolTip(_txtSiteDesc, T(
                "Описание объекта (до 30 символов латиницей/ASCII). Считывается и записывается в память прибора.",
                "Site description (up to 30 ASCII characters). Read from and stored in recorder memory."));

            // Правая панель Status
            _grpRealTime.Text = T("Текущие показания в реальном времени (RealTime)", "RealTime");
            _lblRealLineTitle.Text = T("Напряжение Фаза-Нейтраль (Vrms):", "Line Voltage (Vrms):");
            _lblRealGndTitle.Text = T("Напряжение Нейтраль-Земля (Vrms):", "Ground Voltage (Vrms):");
            _lblRealFreqTitle.Text = T("Частота сети (Hz):", "Line Frequency (Hz):");

            _grpRecInfo.Text = T("Информация о регистраторе (Recorder Info)", "Recorder Info");
            _grpButtons.Text = T("Команды управления регистратором", "Recorder Setup Commands");

            _btnSendSetupClear.Text = T("Записать уставки / Очистить память прибора", "Send Setup / Clear Recorder");
            _btnReloadInputsRight.Text = T("Прочитать текущие уставки из прибора", "Read Current Setup from Recorder");
            _btnDefaultSetup.Text = T("Заводские уставки по умолчанию (220V / 50Hz)", "Default Setup (220V / 50Hz)");

            // Таблица событий
            _gridEvents.Columns[0].HeaderText = T("№", "Event");
            _gridEvents.Columns[1].HeaderText = T("Время начала", "Start Time");
            _gridEvents.Columns[2].HeaderText = T("Тип события", "Event");
            _gridEvents.Columns[3].HeaderText = T("Экстремум", "Extreme");
            _gridEvents.Columns[4].HeaderText = T("Длительность / Фаза", "End");
            _gridEvents.Columns[5].HeaderText = T("Сырые 8 байт (HEX)", "Raw 8 Bytes (HEX)");

            // Графики
            _lblChartStyle.Text = T("Тип графика:", "Graph Style:");
            int selChart = _cmbChartType.SelectedIndex >= 0 ? _cmbChartType.SelectedIndex : 0;
            _cmbChartType.Items[0] = T("1. Сводная гистограмма (Quick Summary Bar Graph)", "1. Quick Summary (Bar Graph)");
            _cmbChartType.Items[1] = T("2. Распределение во времени (Event Distribution Graph)", "2. Event Distribution Graph");
            _cmbChartType.Items[2] = T("3. Осциллограмма одиночного импульса (Single Transient Graph)", "3. Single Transient as a Graph");
            _cmbChartType.SelectedIndex = selChart;

            _btnSaveChartImg.Text = T("🖼️ Сохранить картинку...", "🖼️ Save Graph Image...");
            _lblChartHint.Text = T(
                "💡 Двойной клик по строке '1 Импульс' в таблице строит синусоиду с пробоем.",
                "💡 Double-click any '1 Transient' row in Events table to plot its sinewave.");

            ConfigureHardwareLimitsAndTooltips(_currentSite.Is220V, _rb50Hz.Checked, _currentSite.NgScale025);
            UpdateControlStates();
            UpdateRealtimeAndInfoPanels(_currentSite);
            PopulateEventsGrid(_currentSite);
        }

        // =========================================================================
        // АППАРАТНЫЕ ОГРАНИЧЕНИЯ, ПОДСКАЗКИ И АВТООКРУГЛЕНИЕ ПРИ ПОТЕРЕ ФОКУСА
        // =========================================================================
        private NumericUpDown CreateValidatedNum(decimal min, decimal max, decimal val, decimal step, int decimals)
        {
            var num = new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Increment = step,
                DecimalPlaces = decimals,
                Value = val,
                Width = 140,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("MS Sans Serif", 9.5f, FontStyle.Bold)
            };

            num.Leave += (s, e) => SnapAndValidateNumericInput(num);
            num.Validated += (s, e) => SnapAndValidateNumericInput(num);
            num.ValueChanged += (s, e) =>
            {
                if (!_suppressValidation && !num.Focused)
                    SnapAndValidateNumericInput(num);
            };

            return num;
        }

        private void SnapAndValidateNumericInput(NumericUpDown num)
        {
            if (_suppressValidation) return;

            try
            {
                _suppressValidation = true;

                decimal rawVal = num.Value;
                string rawText = num.Text?.Trim().Replace(',', '.');
                if (!string.IsNullOrEmpty(rawText) &&
                    decimal.TryParse(rawText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed))
                {
                    rawVal = parsed;
                }

                decimal min = num.Minimum;
                decimal max = num.Maximum;
                decimal step = num.Increment > 0 ? num.Increment : 1m;

                decimal clamped = Math.Max(min, Math.Min(max, rawVal));
                decimal stepsCount = Math.Round((clamped - min) / step, 0, MidpointRounding.AwayFromZero);
                decimal snapped = min + (stepsCount * step);
                snapped = Math.Max(min, Math.Min(max, Math.Round(snapped, num.DecimalPlaces)));

                decimal minDiff = _currentSite.Is220V ? 6m : 3m;
                if (num == _numHnSwell && _numHnSag != null && snapped < _numHnSag.Value + minDiff)
                {
                    snapped = Math.Min(max, _numHnSag.Value + minDiff);
                }
                else if (num == _numHnSag && _numHnSwell != null && snapped > _numHnSwell.Value - minDiff)
                {
                    snapped = Math.Max(min, _numHnSwell.Value - minDiff);
                }

                num.Value = snapped;
                num.Text = snapped.ToString($"F{num.DecimalPlaces}", CultureInfo.CurrentCulture);

                if (rawVal != snapped && _lblStatus != null)
                {
                    _lblStatus.Text = T(
                        $"Значение скорректировано под аппаратный шаг прибора: {rawVal} -> {snapped} (диапазон {min}..{max}, шаг {step}).",
                        $"Value adjusted to hardware step: {rawVal} -> {snapped} (range {min}..{max}, step {step}).");
                }
            }
            finally
            {
                _suppressValidation = false;
            }
        }

        private void ConfigureHardwareLimitsAndTooltips(bool is220V, bool is50Hz, bool ngScale025)
        {
            _suppressValidation = true;
            try
            {
                decimal vStep = is220V ? 2m : 1m;
                decimal swellMin = is220V ? 100m : 50m;
                decimal swellMax = is220V ? 300m : 150m;
                decimal sagMin = is220V ? 60m : 30m;
                decimal sagMax = is220V ? 240m : 120m;

                UpdateNumBounds(_numHnSwell, swellMin, swellMax, vStep, 0);
                UpdateNumBounds(_numHnSag, sagMin, sagMax, vStep, 0);

                _lblHnSwellTitle.Text = T(
                    $"Перенапряжение Swell [{swellMin}..{swellMax} В, шаг {vStep} В]:",
                    $"Swell Voltage [{swellMin}..{swellMax} Vrms, step {vStep} V]:");
                _lblHnSagTitle.Text = T(
                    $"Провал напряжения Sag [{sagMin}..{sagMax} В, шаг {vStep} В]:",
                    $"Sag Voltage [{sagMin}..{sagMax} Vrms, step {vStep} V]:");

                string hnVoltTip = T(
                    $"Диапазон: {{0}}..{{1}} В (Vrms) | Шаг изменения: {vStep} В.\nВ памяти прибора хранится в 1 байте с делителем {vStep} (при потере фокуса округляется до кратного {vStep}).",
                    $"Range: {{0}}..{{1}} Vrms | Step: {vStep} V.\nStored in 1 byte with divisor {vStep} (automatically rounded to nearest {vStep} V on focus loss).");
                _toolTip.SetToolTip(_numHnSwell, string.Format(hnVoltTip, swellMin, swellMax));
                _toolTip.SetToolTip(_lblHnSwellTitle, string.Format(hnVoltTip, swellMin, swellMax));
                _toolTip.SetToolTip(_numHnSag, string.Format(hnVoltTip, sagMin, sagMax));
                _toolTip.SetToolTip(_lblHnSagTitle, string.Format(hnVoltTip, sagMin, sagMax));

                UpdateNumBounds(_numHnTransient, 80m, 2500m, 10m, 0);
                UpdateNumBounds(_numNgTransient, 80m, 2500m, 10m, 0);

                _lblHnTransTitle.Text = T(
                    "Отклонение импульса [80..2500 Вп, шаг 10 В]:",
                    "Transient Deviation [80..2500 Vp, step 10 V]:");
                _lblNgTransTitle.Text = T(
                    "Отклонение импульса [80..2500 Вп, шаг 10 В]:",
                    "Transient Deviation [80..2500 Vp, step 10 V]:");

                string trTip = T(
                    "Диапазон: 80..2500 Вп (Vp) | Шаг изменения: 10 В.\nВ протоколе прибора передается 1 байтом, деленным на 10 (например, 200 Vp = 0x14).",
                    "Range: 80..2500 Vp | Step: 10 V.\nTransmitted as 1 byte divided by 10 (e.g., 200 Vp = 0x14).");
                _toolTip.SetToolTip(_numHnTransient, trTip);
                _toolTip.SetToolTip(_lblHnTransTitle, trTip);
                _toolTip.SetToolTip(_numNgTransient, trTip);
                _toolTip.SetToolTip(_lblNgTransTitle, trTip);

                if (ngScale025)
                {
                    UpdateNumBounds(_numNgSwell, 0.25m, 60.0m, 0.25m, 2);
                    _lblNgSwellTitle.Text = T(
                        "Напряжение N-G Swell [0.25..60.0 В, шаг 0.25 В]:",
                        "Swell Voltage [0.25..60.0 Vrms, step 0.25 V]:");
                    _toolTip.SetToolTip(_numNgSwell, T(
                        "Диапазон: 0.25..60.0 В | Шаг изменения: 0.25 В (высокочувствительный режим N-G).",
                        "Range: 0.25..60.0 Vrms | Step: 0.25 V (high-sensitivity N-G mode)."));
                }
                else
                {
                    UpdateNumBounds(_numNgSwell, 1m, 250m, 1m, 0);
                    _lblNgSwellTitle.Text = T(
                        "Напряжение N-G Swell [1..250 В, шаг 1 В]:",
                        "Swell Voltage [1..250 Vrms, step 1 V]:");
                    _toolTip.SetToolTip(_numNgSwell, T(
                        "Диапазон: 1..250 В (Vrms) | Шаг изменения: 1 В (хранится в 1 байте).",
                        "Range: 1..250 Vrms | Step: 1 V (stored in 1 byte)."));
                }
                _toolTip.SetToolTip(_lblNgSwellTitle, _toolTip.GetToolTip(_numNgSwell));

                if (is50Hz)
                {
                    UpdateNumBounds(_numMaxFreq, 50.1m, 55.0m, 0.1m, 1);
                    UpdateNumBounds(_numMinFreq, 45.0m, 49.9m, 0.1m, 1);
                    _lblMaxFreqTitle.Text = T("Максимум частоты [50.1..55.0 Гц, шаг 0.1 Гц]:", "Maximum [50.1..55.0 Hz, step 0.1 Hz]:");
                    _lblMinFreqTitle.Text = T("Минимум частоты [45.0..49.9 Гц, шаг 0.1 Гц]:", "Minimum [45.0..49.9 Hz, step 0.1 Hz]:");
                }
                else
                {
                    UpdateNumBounds(_numMaxFreq, 60.1m, 65.0m, 0.1m, 1);
                    UpdateNumBounds(_numMinFreq, 55.0m, 59.9m, 0.1m, 1);
                    _lblMaxFreqTitle.Text = T("Максимум частоты [60.1..65.0 Гц, шаг 0.1 Гц]:", "Maximum [60.1..65.0 Hz, step 0.1 Hz]:");
                    _lblMinFreqTitle.Text = T("Минимум частоты [55.0..59.9 Гц, шаг 0.1 Hz]:", "Minimum [55.0..59.9 Hz, step 0.1 Hz]:");
                }

                string freqTip = T(
                    "Шаг изменения: 0.1 Гц.\nВнутри прибора пересчитывается в такты таймера 57600 Гц по формуле Word = Round(57600 / Freq).",
                    "Step: 0.1 Hz.\nConverted to 57600 Hz timer ticks via Word = Round(57600 / Freq).");
                _toolTip.SetToolTip(_numMaxFreq, freqTip);
                _toolTip.SetToolTip(_lblMaxFreqTitle, freqTip);
                _toolTip.SetToolTip(_numMinFreq, freqTip);
                _toolTip.SetToolTip(_lblMinFreqTitle, freqTip);
            }
            finally
            {
                _suppressValidation = false;
            }

            SnapAndValidateNumericInput(_numHnSwell);
            SnapAndValidateNumericInput(_numHnSag);
            SnapAndValidateNumericInput(_numHnTransient);
            SnapAndValidateNumericInput(_numNgSwell);
            SnapAndValidateNumericInput(_numNgTransient);
            SnapAndValidateNumericInput(_numMaxFreq);
            SnapAndValidateNumericInput(_numMinFreq);
        }

        private static void UpdateNumBounds(NumericUpDown num, decimal min, decimal max, decimal step, int decimals)
        {
            decimal cur = num.Value;
            num.DecimalPlaces = decimals;
            num.Increment = step;
            num.Minimum = min;
            num.Maximum = max;
            if (cur < min) cur = min;
            if (cur > max) cur = max;
            num.Value = cur;
        }

        private static Label AddRowWithLabelRef(TableLayoutPanel tbl, int row, string labelText, Control ctrl)
        {
            var lbl = new Label
            {
                Text = labelText,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                AutoSize = false,
                ForeColor = Color.Black
            };
            tbl.Controls.Add(lbl, 0, row);
            tbl.Controls.Add(ctrl, 1, row);
            return lbl;
        }

        private void SetSafeNum(NumericUpDown num, decimal val)
        {
            bool prev = _suppressValidation;
            _suppressValidation = true;
            try
            {
                if (val < num.Minimum) val = num.Minimum;
                if (val > num.Maximum) val = num.Maximum;
                num.Value = val;
            }
            finally
            {
                _suppressValidation = prev;
            }
            SnapAndValidateNumericInput(num);
        }

        private void RefreshComPorts()
        {
            _cmbPorts.Items.Clear();
            string[] ports = SerialPort.GetPortNames().OrderBy(p => p).ToArray();
            _cmbPorts.Items.AddRange(ports);
            if (ports.Length > 0) _cmbPorts.SelectedIndex = 0;
        }

        private void LogMessage(string msg)
        {
            if (InvokeRequired) { Invoke(new Action<string>(LogMessage), msg); return; }
            _txtCommLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
        }

        private void ApplyDefaultSetupToInputs()
        {
            _rb50Hz.Checked = true;
            ConfigureHardwareLimitsAndTooltips(true, true, _currentSite.NgScale025);
            SetSafeNum(_numHnSwell, 256);
            SetSafeNum(_numHnSag, 210);
            SetSafeNum(_numHnTransient, 200);
            SetSafeNum(_numNgSwell, 10);
            SetSafeNum(_numNgTransient, 100);
            SetSafeNum(_numMaxFreq, 51.2m);
            SetSafeNum(_numMinFreq, 48.8m);
            _rbFifo.Checked = true;
            _chkFlashLed.Checked = true;
            _lblStatus.Text = T(
                "Заводские уставки подставлены в поля ввода. Нажмите 'Записать уставки / Очистить память' для отправки в прибор.",
                "Default thresholds loaded into input fields. Click 'Send Setup / Clear Recorder' to apply.");
        }

        // =========================================================================
        // РЕЖИМ СКАНИРОВАНИЯ ПОРТА (Toggle Scanning: 00 FF FF 41 BE)
        // =========================================================================
        private void ToggleScanning()
        {
            if (_isScanning) StopScanningPort();
            else StartScanningPort();
            UpdateControlStates();
        }

        private void StartScanningPort()
        {
            if (_cmbPorts.SelectedItem == null)
            {
                MessageBox.Show(
                    T("Сначала выберите COM-порт!", "Open a COM port first before you begin to scan!"),
                    "EventView", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string portName = _cmbPorts.SelectedItem.ToString();
            try
            {
                _serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One)
                {
                    DtrEnable = true,
                    RtsEnable = true,
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };
                _serialPort.Open();
                _isScanning = true;
                _initialSetupLoadedFromDevice = false;
                _eventsDownloadedForCurrentSession = false;

                // До получения первого ответа от прибора очищаем данные прошлой сессии
                _currentSite = new SiteData();
                UpdateRealtimeAndInfoPanels(_currentSite);

                _scanTimer.Start();
                LogMessage($"Opened {portName}. Scanning started (00-FF-FF-41-BE). Waiting for VR101 response...");
            }
            catch (Exception ex)
            {
                LogMessage($"Unable to Open Port {portName}: {ex.Message}");
                MessageBox.Show(
                    T($"Не удалось открыть порт {portName}.\n{ex.Message}", $"Unable to Open Port {portName}.\n{ex.Message}"),
                    "EventView", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _isScanning = false;
            }
        }

        private void StopScanningPort()
        {
            _scanTimer.Stop();
            _isScanning = false;
            _initialSetupLoadedFromDevice = false;

            lock (_portLock)
            {
                try
                {
                    if (_serialPort != null)
                    {
                        if (_serialPort.IsOpen) _serialPort.Close();
                        _serialPort.Dispose();
                        _serialPort = null;
                    }
                }
                catch { }
            }

            // При разрыве связи сбрасываем живые показания в прочерки
            _lblRealLineVolt.Text = "---";
            _lblRealGndVolt.Text = "---";
            _lblRealFreq.Text = "---";
            LogMessage("Scanning stopped. Port closed.");
        }

        private void UpdateControlStates()
        {
            if (_isScanning)
            {
                _btnScanToggle.Text = T("⏹ Стоп опроса", "⏹ Stop Scanning");
                _btnScanToggle.BackColor = Color.LightCoral;
                _cmbPorts.Enabled = false;
                _btnReloadSetup.Enabled = true;
                _btnReloadInputsRight.Enabled = true;
                _btnDownloadEvents.Enabled = true;
                _btnSendSetupClear.Enabled = true;
                _lblPortStatus.Text = T($"Опрос: ВКЛ ({_serialPort?.PortName})", $"Scanning: ON ({_serialPort?.PortName})");
                _lblStatus.Text = T("Опрос активен. Ожидание ответа от регистратора VR101...", "Scanning active. Waiting for VR101 response...");
            }
            else
            {
                _btnScanToggle.Text = T("📡 Старт опроса", "📡 Start Scanning");
                _btnScanToggle.BackColor = Color.LightGray;
                _cmbPorts.Enabled = true;
                _btnReloadSetup.Enabled = false;
                _btnReloadInputsRight.Enabled = false;
                _btnDownloadEvents.Enabled = false;
                _btnSendSetupClear.Enabled = false;
                _lblPortStatus.Text = T("Опрос: ВЫКЛ", "Scanning: OFF");
                _lblStatus.Text = T("Опрос отключен. Подключите порт для чтения параметров устройства.", "Scanning OFF. Connect port to read device parameters.");
            }
        }

        private byte[] RequestDeviceHeader88()
        {
            lock (_portLock)
            {
                if (_serialPort == null || !_serialPort.IsOpen) return null;

                _serialPort.DiscardInBuffer();
                byte[] cmdA = new byte[] { 0x00, 0xFF, 0xFF, 0x41, 0xBE };
                _serialPort.Write(cmdA, 0, cmdA.Length);

                byte[] raw = ReadBytesWithTimeout(_serialPort, 94, 650);
                if (raw.Length < 88) return null;

                int startIdx = -1;
                for (int i = 0; i <= raw.Length - 88; i++)
                {
                    if (raw[i] == 0x41 && i + 1 < raw.Length && raw[i + 1] == 0xBE)
                    {
                        startIdx = i + 2;
                        break;
                    }
                }

                if (startIdx < 0 || raw.Length - startIdx < 88) return null;

                byte[] hdr88 = new byte[88];
                Array.Copy(raw, startIdx, hdr88, 0, 88);

                if (raw.Length >= startIdx + 89)
                {
                    byte rxCrc = raw[startIdx + 88];
                    int sum = 0;
                    for (int i = 0; i < 88; i++) sum = (sum + hdr88[i]) & 0xFF;
                    byte calcCrc = (byte)(0xFF - sum);
                    if (rxCrc != calcCrc)
                    {
                        LogMessage($"Header CRC mismatch: expected 0x{calcCrc:X2}, got 0x{rxCrc:X2}");
                        return null;
                    }
                }

                return hdr88;
            }
        }

        private async Task PollRecorderStatusAsync()
        {
            if (!_isScanning || _isBusyWithCommand) return;

            _isBusyWithCommand = true;
            try
            {
                byte[] hdr = await Task.Run(() => RequestDeviceHeader88());
                if (hdr != null && hdr.Length == 88)
                {
                    var liveSite = SiteData.ParseFromDeviceHeader(hdr, DateTime.Now);

                    // Если это первый ответ от прибора или сменился серийный номер подключенного устройства —
                    // загружаем все его аппаратные параметры и уставки в интерфейс
                    if (!_initialSetupLoadedFromDevice || _currentSite.SerialNumber != liveSite.SerialNumber)
                    {
                        _currentSite = liveSite;
                        PopulateSetupInputsFromSite(_currentSite);
                        _initialSetupLoadedFromDevice = true;
                        this.Text = $"Fluke EventView EV101 - [SN F{_currentSite.SerialNumber}]";
                        LogMessage($"Detected Recorder SN#F{_currentSite.SerialNumber} (FW v{_currentSite.FirmwareVersion}). Loaded device parameters and setup.");
                    }
                    else
                    {
                        _currentSite.HasValidData = true;
                        _currentSite.SerialNumber = liveSite.SerialNumber;
                        _currentSite.FirmwareVersion = liveSite.FirmwareVersion;
                        _currentSite.OptionsByte = liveSite.OptionsByte;
                        _currentSite.Is220V = liveSite.Is220V;
                        _currentSite.Is50Hz = liveSite.Is50Hz;
                        _currentSite.IsFifo = liveSite.IsFifo;
                        _currentSite.ElapsedTicks = liveSite.ElapsedTicks;
                        _currentSite.StartTime = liveSite.StartTime;
                        _currentSite.EndTime = liveSite.EndTime;
                        _currentSite.LiveHnVolt = liveSite.LiveHnVolt;
                        _currentSite.LiveNgVolt = liveSite.LiveNgVolt;
                        _currentSite.LiveFreq = liveSite.LiveFreq;
                        _currentSite.StoredEventCount = liveSite.StoredEventCount;
                        _currentSite.IsMemoryFull = liveSite.IsMemoryFull;
                        _currentSite.OpenEvents = liveSite.OpenEvents;
                        _currentSite.RawHeader88 = liveSite.RawHeader88;
                    }

                    UpdateRealtimeAndInfoPanels(_currentSite);
                    _lblStatus.Text = T(
                        $"Связь установлена: обновление данных в реальном времени (SN#F{_currentSite.SerialNumber}, прошивка v{_currentSite.FirmwareVersion})",
                        $"Connected: updating Realtime values of SN#F{_currentSite.SerialNumber} (FW v{_currentSite.FirmwareVersion})");
                }
            }
            catch { }
            finally
            {
                _isBusyWithCommand = false;
            }
        }

        private async Task ReloadSetupFromDeviceAsync()
        {
            if (!_isScanning) return;
            while (_isBusyWithCommand) await Task.Delay(30);
            _isBusyWithCommand = true;
            try
            {
                byte[] hdr = await Task.Run(() => RequestDeviceHeader88());
                if (hdr != null && hdr.Length == 88)
                {
                    _currentSite = SiteData.ParseFromDeviceHeader(hdr, DateTime.Now);
                    _initialSetupLoadedFromDevice = true;
                    this.Text = $"Fluke EventView EV101 - [SN F{_currentSite.SerialNumber}]";
                    PopulateSetupInputsFromSite(_currentSite);
                    UpdateRealtimeAndInfoPanels(_currentSite);
                    LogMessage($"Reloaded setup thresholds from Recorder SN#F{_currentSite.SerialNumber}.");
                    _lblStatus.Text = T(
                        $"Уставки и параметры успешно считаны из прибора SN#F{_currentSite.SerialNumber}.",
                        $"Setup reloaded from SN#F{_currentSite.SerialNumber}.");
                }
                else
                {
                    MessageBox.Show(
                        T("Нет ответа от регистратора. Проверьте подключение оптического кабеля Lite-Link.",
                          "No response from recorder. Check Optical Interface Cable."),
                        "EventView", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                _isBusyWithCommand = false;
            }
        }

        // =========================================================================
        // ВЫГРУЗКА СОБЫТИЙ НА ВКЛАДКЕ "EVENTS" (00 FF FF 42 [Block] [CRC])
        // =========================================================================
        private async Task DownloadEventsFromDeviceAsync()
        {
            if (!_isScanning || _serialPort == null || !_serialPort.IsOpen) return;

            while (_isBusyWithCommand) await Task.Delay(30);
            _isBusyWithCommand = true;
            _btnDownloadEvents.Enabled = false;
            _progressBar.Visible = true;
            _progressBar.Value = 0;

            try
            {
                byte[] header = await Task.Run(() => RequestDeviceHeader88());
                if (header == null || header.Length < 88)
                    throw new IOException(T("Нет ответа от оптического адаптера / прибора!", "No Optical Interface / Recorder response on port!"));

                var site = SiteData.ParseFromDeviceHeader(header, DateTime.Now);
                _initialSetupLoadedFromDevice = true;
                this.Text = $"Fluke EventView EV101 - [SN F{site.SerialNumber}]";

                if (site.StoredEventCount == 0)
                {
                    _currentSite = site;
                    _selectedTransientEvent = null;
                    _eventsDownloadedForCurrentSession = true;
                    PopulateSetupInputsFromSite(_currentSite);
                    UpdateRealtimeAndInfoPanels(_currentSite);
                    _gridEvents.Rows.Clear();
                    _pnlCharts.Invalidate();
                    _lblStatus.Text = T($"В памяти прибора SN#F{site.SerialNumber} нет записанных событий (0).", $"No Events Stored in recorder SN#F{site.SerialNumber}.");
                    LogMessage($"Recorder SN#F{site.SerialNumber} has 0 stored events. Events table cleared.");
                    return;
                }

                if (site.LiveHnVolt < 10.0)
                {
                    var batRes = MessageBox.Show(
                        T("Напряжение сети не обнаружено (прибор отключен от розетки). Выгрузка событий разряжает литиевую батарею. Продолжить?",
                          "No power detected on line. Downloading will reduce battery life. Continue anyways?"),
                        "Fluke EventView", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (batRes != DialogResult.Yes) return;
                }

                await Task.Run(() =>
                {
                    int totalEvents = site.StoredEventCount;
                    int blocksNeeded = (totalEvents + 31) / 32;
                    List<byte> eventBuffer = new List<byte>(blocksNeeded * 256);

                    for (int blk = 0; blk < blocksNeeded; blk++)
                    {
                        byte[] block256 = RequestEventBlock256((byte)blk);
                        if (block256 != null) eventBuffer.AddRange(block256);

                        int pct = ((blk + 1) * 100) / Math.Max(1, blocksNeeded);
                        Invoke(new Action(() =>
                        {
                            _progressBar.Value = Math.Min(100, pct);
                            _lblStatus.Text = T(
                                $"Загрузка данных из SN#F{site.SerialNumber}.... {pct}% (Блок {blk + 1}/{blocksNeeded})",
                                $"Downloading data from SN#F{site.SerialNumber}.... {pct}% (Block {blk + 1}/{blocksNeeded})");
                        }));
                    }

                    site.ParseAllEvents(eventBuffer.ToArray());
                    _currentSite = site;
                    _selectedTransientEvent = _currentSite.Events.LastOrDefault(x => x.IsSingleTransient);
                    _eventsDownloadedForCurrentSession = true;

                    Invoke(new Action(() =>
                    {
                        PopulateSetupInputsFromSite(_currentSite);
                        UpdateRealtimeAndInfoPanels(_currentSite);
                        PopulateEventsGrid(_currentSite);
                        _lblStatus.Text = T($"Загрузка событий из SN#F{_currentSite.SerialNumber} завершена.", "Downloading complete.");
                    }));
                });
            }
            catch (Exception ex)
            {
                LogMessage($"Download error: {ex.Message}");
                MessageBox.Show(ex.Message, T("Ошибка загрузки", "Download Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnDownloadEvents.Enabled = true;
                _progressBar.Visible = false;
                _isBusyWithCommand = false;
            }
        }

        private byte[] RequestEventBlock256(byte blockIndex)
        {
            lock (_portLock)
            {
                _serialPort.DiscardInBuffer();
                byte cmd = 0x42; // 'B'
                byte crc = (byte)(0xFF - ((cmd + blockIndex) & 0xFF));
                byte[] req = new byte[] { 0x00, 0xFF, 0xFF, cmd, blockIndex, crc };

                LogMessage($"Command Get Events (Block {blockIndex}): {BitConverter.ToString(req)}");
                _serialPort.Write(req, 0, req.Length);

                byte[] rx = ReadBytesWithTimeout(_serialPort, 263, 2200);
                LogMessage($"Block {blockIndex} response: {rx.Length} bytes.");

                int startIdx = 0;
                for (int i = 0; i < Math.Min(10, rx.Length - 2); i++)
                {
                    if (rx[i] == 0x42 && rx[i + 1] == blockIndex && rx[i + 2] == crc)
                    {
                        startIdx = i + 3;
                        break;
                    }
                }

                int avail = Math.Max(0, rx.Length - startIdx);
                int copyLen = Math.Min(256, avail);
                byte[] data256 = new byte[copyLen];
                if (copyLen > 0) Array.Copy(rx, startIdx, data256, 0, copyLen);
                return data256;
            }
        }

        // =========================================================================
        // ОТПРАВКА УСТАВОК И ОЧИСТКА ПАМЯТИ ("Send Setup / Clear Recorder")
        // =========================================================================
        private async void SendSetupAndClearRecorder()
        {
            if (!_isScanning || _serialPort == null || !_serialPort.IsOpen) return;

            SnapAndValidateNumericInput(_numHnSwell);
            SnapAndValidateNumericInput(_numHnSag);
            SnapAndValidateNumericInput(_numHnTransient);
            SnapAndValidateNumericInput(_numNgSwell);
            SnapAndValidateNumericInput(_numNgTransient);
            SnapAndValidateNumericInput(_numMaxFreq);
            SnapAndValidateNumericInput(_numMinFreq);

            decimal minDiff = _currentSite.Is220V ? 6m : 3m;
            if ((_numHnSwell.Value - _numHnSag.Value) < minDiff)
            {
                MessageBox.Show(
                    T($"Разница между порогами Swell и Sag должна быть не менее {minDiff} В.",
                      $"Surge and Sag thresholds need to be at least {minDiff} volts apart."),
                    "Fluke EventView", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show(
                T("Это действие удалит все события из памяти регистратора и запишет новые уставки! Продолжить?",
                  "This will clear all events from the recorder! Proceed?"),
                "Fluke EventView", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            while (_isBusyWithCommand) await Task.Delay(30);
            _isBusyWithCommand = true;

            try
            {
                bool is220 = _currentSite.Is220V;
                double vDiv = is220 ? 2.0 : 1.0;

                byte optByte = 0xE0;
                if (_rb50Hz.Checked) optByte |= 0x04;
                if (!_chkFlashLed.Checked) optByte |= 0x02;
                if (_rbFifo.Checked) optByte |= 0x01;

                ushort minFreqWord = (ushort)Math.Round(57600.0 / (double)_numMinFreq.Value);
                ushort maxFreqWord = (ushort)Math.Round(57600.0 / (double)_numMaxFreq.Value);

                byte sagByte = (byte)Math.Round((double)_numHnSag.Value / vDiv);
                byte swellByte = (byte)Math.Round((double)_numHnSwell.Value / vDiv);
                byte hnTrByte = (byte)Math.Round((double)_numHnTransient.Value / 10.0);

                byte ngSwellByte = _currentSite.NgScale025
                    ? (byte)Math.Round((double)_numNgSwell.Value * 4.0)
                    : (byte)Math.Round((double)_numNgSwell.Value);
                byte ngTrByte = (byte)Math.Round((double)_numNgTransient.Value / 10.0);

                byte constB19 = _currentSite.RawHeader88[19] != 0 ? _currentSite.RawHeader88[19] : (byte)0xB6;
                string desc30 = _txtSiteDesc.Text.PadRight(30, ' ').Substring(0, 30);

                List<byte> payload = new List<byte>
                {
                    0x58,                           // 'X'
                    optByte,                        // E0..E7
                    (byte)(minFreqWord >> 8),
                    (byte)(minFreqWord & 0xFF),
                    (byte)(maxFreqWord >> 8),
                    (byte)(maxFreqWord & 0xFF),
                    sagByte,
                    swellByte,
                    hnTrByte,
                    ngSwellByte,
                    ngTrByte,
                    0x03,                           // FixedDeadBand = 3
                    0x7D, 0x00,                     // Size = 32000 (0x7D00)
                    constB19
                };
                payload.AddRange(Encoding.ASCII.GetBytes(desc30));

                int sum = 0;
                foreach (byte b in payload) sum = (sum + b) & 0xFF;
                byte crc = (byte)(0xFF - sum);

                List<byte> fullPacket = new List<byte> { 0x00, 0xFF, 0xFF };
                fullPacket.AddRange(payload);
                fullPacket.Add(crc);
                byte[] rawBytes = fullPacket.ToArray();

                bool success = false;
                await Task.Run(() =>
                {
                    for (int tryNum = 1; tryNum <= 3; tryNum++)
                    {
                        Invoke(new Action(() => _lblStatus.Text = T($"Отправка уставок.... Попытка {tryNum}", $"Sending Setup....Try {tryNum}")));
                        lock (_portLock)
                        {
                            _serialPort.DiscardInBuffer();
                            LogMessage($"Sending Setup (Try {tryNum}): {BitConverter.ToString(rawBytes)}");
                            _serialPort.Write(rawBytes, 0, rawBytes.Length);
                            Thread.Sleep(550);

                            byte[] rx = ReadBytesWithTimeout(_serialPort, 64, 450);
                            LogMessage($"Setup Response: {BitConverter.ToString(rx)}");
                            if (rx.Length > 0)
                            {
                                success = true;
                                break;
                            }
                        }
                    }
                });

                byte[] verifyHdr = await Task.Run(() => RequestDeviceHeader88());
                if (verifyHdr != null && verifyHdr.Length == 88)
                {
                    _currentSite = SiteData.ParseFromDeviceHeader(verifyHdr, DateTime.Now);
                    _initialSetupLoadedFromDevice = true;
                    this.Text = $"Fluke EventView EV101 - [SN F{_currentSite.SerialNumber}]";
                    PopulateSetupInputsFromSite(_currentSite);
                    UpdateRealtimeAndInfoPanels(_currentSite);
                }

                _currentSite.Events.Clear();
                _currentSite.OpenEvents.Clear();
                _selectedTransientEvent = null;
                _gridEvents.Rows.Clear();
                _eventsDownloadedForCurrentSession = false;
                _pnlCharts.Invalidate();

                if (success)
                {
                    MessageBox.Show(
                        T("Настройка завершена! Память очищена, регистратор готов к мониторингу сети.",
                          "Setup complete! The recorder will monitor power quality when plugged into a power outlet."),
                        "Fluke EventView", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Send Setup Error: {ex.Message}");
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isBusyWithCommand = false;
            }
        }

        private static byte[] ReadBytesWithTimeout(SerialPort port, int targetCount, int timeoutMs)
        {
            List<byte> result = new List<byte>(targetCount);
            int start = Environment.TickCount;
            while (result.Count < targetCount && (Environment.TickCount - start) < timeoutMs)
            {
                int avail = port.BytesToRead;
                if (avail > 0)
                {
                    int toRead = Math.Min(avail, targetCount - result.Count);
                    byte[] buf = new byte[toRead];
                    int read = port.Read(buf, 0, toRead);
                    for (int i = 0; i < read; i++) result.Add(buf[i]);
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
            return result.ToArray();
        }

        // =========================================================================
        // РАЗДЕЛЬНОЕ ОБНОВЛЕНИЕ: 1) УСТАВКИ, 2) REALTIME & INFO, 3) ТАБЛИЦА EVENTS
        // =========================================================================
        private void PopulateSetupInputsFromSite(SiteData s)
        {
            if (!s.HasValidData) return;

            ConfigureHardwareLimitsAndTooltips(s.Is220V, s.Is50Hz, s.NgScale025);

            _suppressValidation = true;
            try
            {
                _txtSiteDesc.Text = s.Description;
                _rb50Hz.Checked = s.Is50Hz;
                _rb60Hz.Checked = !s.Is50Hz;
                _rbFifo.Checked = s.IsFifo;
                _rbStopFull.Checked = !s.IsFifo;
                _chkFlashLed.Checked = s.FlashLed;
            }
            finally
            {
                _suppressValidation = false;
            }

            SetSafeNum(_numHnSwell, s.HnSwell);
            SetSafeNum(_numHnSag, s.HnSag);
            SetSafeNum(_numHnTransient, s.HnImpulse);
            SetSafeNum(_numNgSwell, (decimal)s.NgSurge);
            SetSafeNum(_numNgTransient, s.NgImpulse);
            SetSafeNum(_numMaxFreq, (decimal)s.MaxFreq);
            SetSafeNum(_numMinFreq, (decimal)s.MinFreq);
        }

        /// <summary>
        /// Если сессии с устройством еще не было (s.HasValidData == false), выводит прочерки "---"
        /// во всех полях прибора. Как только устройство ответило — выводит реальные считанные данные.
        /// </summary>
        private void UpdateRealtimeAndInfoPanels(SiteData s)
        {
            if (s == null || !s.HasValidData)
            {
                _lblRealLineVolt.Text = "---";
                _lblRealGndVolt.Text = "---";
                _lblRealFreq.Text = "---";

                _lblInfoSerial.Text = T("Серийный номер прибора: ---", "Logger Serial Number: ---");
                _lblInfoEventsCount.Text = T("Нет данных (прибор не подключен)", "No Device Data (Not Connected)");
                _lblInfoFromDate.Text = T("      с     ---", "      from  ---");
                _lblInfoToDate.Text = T("      по   ---", "      to      ---");
                _lblInfoFirmware.Text = T("Версия прошивки VR101: ---", "VR101 Firmware Version: ---");
                _lblInfoMode.Text = T("Режим прибора: ---", "Device Mode: ---");
                return;
            }

            if (_isScanning)
            {
                _lblRealLineVolt.Text = $"{s.LiveHnVolt:0} Vrms";
                _lblRealGndVolt.Text = $"{s.LiveNgVolt:0.0} Vrms";
                _lblRealFreq.Text = $"{s.LiveFreq:00.0} Hz";
            }
            else
            {
                _lblRealLineVolt.Text = "---";
                _lblRealGndVolt.Text = "---";
                _lblRealFreq.Text = "---";
            }

            _lblInfoSerial.Text = T($"Серийный номер прибора: F{s.SerialNumber}", $"Logger Serial Number: F{s.SerialNumber}");
            if (s.IsMemoryFull && !s.IsFifo)
                _lblInfoEventsCount.Text = T($"ПАМЯТЬ ПОЛНА! (Записано событий: {s.StoredEventCount})", $"RECORDER FULL! ({s.StoredEventCount} Events Recorded:)");
            else if (s.StoredEventCount == 0)
                _lblInfoEventsCount.Text = T("Нет записанных событий (0).", "No Events Stored.");
            else
                _lblInfoEventsCount.Text = T($"Записано событий в памяти: {s.StoredEventCount}", $"{s.StoredEventCount} Events Recorded:");

            _lblInfoFromDate.Text = T($"      с     {s.StartTime:dd.MM.yyyy HH:mm:ss}", $"      from  {s.StartTime:dd.MM.yyyy hh:mm:ss tt}");
            _lblInfoToDate.Text = T($"      по   {s.EndTime:dd.MM.yyyy HH:mm:ss}.", $"      to      {s.EndTime:dd.MM.yyyy hh:mm:ss tt}.");
            _lblInfoFirmware.Text = s.FirmwareVersion > 0
                ? T($"Версия прошивки VR101: {s.FirmwareVersion}", $"VR101 Firmware Version {s.FirmwareVersion}")
                : T("Версия прошивки VR101: (из файла .VER)", "VR101 Firmware Version: (from .VER file)");
            _lblInfoMode.Text = T(
                $"Режим: {(s.Is220V ? "220В" : "110В")} / {(s.Is50Hz ? "50Гц" : "60Гц")} (Опции: 0x{s.OptionsByte:X2})",
                $"Mode: {(s.Is220V ? "220V" : "110V")} / {(s.Is50Hz ? "50Hz" : "60Hz")} (Opt: 0x{s.OptionsByte:X2})");
        }

        private void PopulateEventsGrid(SiteData s)
        {
            _gridEvents.Rows.Clear();

            if (s == null || !s.HasValidData || (s.StoredEventCount == 0 && s.Events.Count == 0))
            {
                _pnlCharts.Invalidate();
                return;
            }

            foreach (var openEv in s.OpenEvents)
            {
                int rIdx = _gridEvents.Rows.Add(
                    0,
                    openEv.Timestamp.ToString("dd.MM.yyyy HH:mm:ss"),
                    openEv.GetLocalizedEventName(_lang),
                    openEv.ExtremeText,
                    openEv.GetLocalizedEndText(_lang),
                    BitConverter.ToString(openEv.Raw)
                );
                _gridEvents.Rows[rIdx].Tag = openEv;
                _gridEvents.Rows[rIdx].Height = _gridEvents.RowTemplate.Height;
                _gridEvents.Rows[rIdx].DefaultCellStyle.BackColor = Color.FromArgb(215, 235, 255);
                _gridEvents.Rows[rIdx].DefaultCellStyle.Font = new Font(_gridEvents.Font, FontStyle.Bold);
            }

            for (int i = s.Events.Count - 1; i >= 0; i--)
            {
                var ev = s.Events[i];
                int rIdx = _gridEvents.Rows.Add(
                    ev.Index,
                    ev.Timestamp.ToString("dd.MM.yyyy HH:mm:ss"),
                    ev.GetLocalizedEventName(_lang),
                    ev.ExtremeText,
                    ev.GetLocalizedEndText(_lang),
                    BitConverter.ToString(ev.Raw)
                );
                _gridEvents.Rows[rIdx].Tag = ev;
                _gridEvents.Rows[rIdx].Height = _gridEvents.RowTemplate.Height;
                if (ev.IsSingleTransient)
                {
                    _gridEvents.Rows[rIdx].DefaultCellStyle.BackColor = Color.FromArgb(255, 250, 240);
                }
            }

            _pnlCharts.Invalidate();
        }

        // =========================================================================
        // ОТРИСОВКА 3 ШТАТНЫХ РЕЖИМОВ ГРАФИКОВ
        // =========================================================================
        private void DrawChartsGraphics(object sender, PaintEventArgs e)
        {
            RenderChartContent(e.Graphics, _pnlCharts.ClientRectangle);
        }

        private void RenderChartContent(Graphics g, Rectangle rect)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (!_currentSite.HasValidData || _currentSite.Events.Count == 0)
            {
                using (var fnt = new Font("MS Sans Serif", 10f * _fontScale))
                {
                    g.DrawString(
                        T("Нет данных для построения графика! Загрузите события из прибора или откройте файл .VER.",
                          "No Events to Plot! Download events from recorder or open a .VER file."),
                        fnt, Brushes.Gray, new Point(20, 20));
                }
                return;
            }

            if (_cmbChartType.SelectedIndex == 0)
            {
                DrawQuickSummaryBarChart(g, rect);
            }
            else if (_cmbChartType.SelectedIndex == 1)
            {
                DrawEventDistributionTimeline(g, rect);
            }
            else if (_cmbChartType.SelectedIndex == 2)
            {
                DrawSingleTransientSineWaveGraph(g, rect);
            }
        }

        private void DrawQuickSummaryBarChart(Graphics g, Rectangle rect)
        {
            using (var titleFont = new Font("MS Sans Serif", 11f * _fontScale, FontStyle.Bold))
            using (var labelFont = new Font("Arial", 8.5f * _fontScale))
            using (var valFont = new Font("Arial", 9.5f * _fontScale, FontStyle.Bold))
            {
                g.DrawString(
                    T($"Сводная гистограмма событий (Quick Summary) — SN F{_currentSite.SerialNumber}",
                      $"Quick Summary (Bar Graph) — SN F{_currentSite.SerialNumber}"),
                    titleFont, Brushes.Black, new Point(20, 15));

                var groups = _currentSite.Events.GroupBy(ev => ev.GetLocalizedEventName(_lang)).ToList();
                int startX = 70, startY = rect.Height - 65, colWidth = (int)(120 * _fontScale);
                int maxCount = Math.Max(1, groups.Max(gr => gr.Count()));

                g.DrawLine(Pens.Black, startX, startY, startX + groups.Count * (colWidth + 28), startY);
                g.DrawLine(Pens.Black, startX, 55, startX, startY);

                int idx = 0;
                foreach (var gr in groups)
                {
                    int barH = (int)((gr.Count() / (double)maxCount) * Math.Max(50, rect.Height - 150));
                    int x = startX + 15 + idx * (colWidth + 28);
                    int y = startY - barH;

                    g.FillRectangle(Brushes.SteelBlue, x, y, colWidth, barH);
                    g.DrawRectangle(Pens.DarkBlue, x, y, colWidth, barH);
                    g.DrawString(gr.Key, labelFont, Brushes.Black, new Point(x, startY + 6));
                    g.DrawString(gr.Count().ToString(), valFont, Brushes.DarkRed, new Point(x + colWidth / 2 - 10, y - (int)(20 * _fontScale)));
                    idx++;
                }
            }
        }

        private void DrawEventDistributionTimeline(Graphics g, Rectangle rect)
        {
            using (var titleFont = new Font("MS Sans Serif", 11f * _fontScale, FontStyle.Bold))
            {
                g.DrawString(
                    T($"Распределение событий во времени (Event Distribution) — SN F{_currentSite.SerialNumber}",
                      $"Event Distribution Graph — SN F{_currentSite.SerialNumber}"),
                    titleFont, Brushes.Black, new Point(20, 15));

                int ox = 70, oy = rect.Height - 55, w = Math.Max(100, rect.Width - 115), h = Math.Max(100, rect.Height - 130);
                g.DrawLine(Pens.Black, ox, oy, ox + w, oy);
                g.DrawLine(Pens.Black, ox, oy - h, ox, oy);

                DateTime minT = _currentSite.Events.Min(ev => ev.Timestamp);
                DateTime maxT = _currentSite.Events.Max(ev => ev.Timestamp);
                double totalSec = Math.Max(1.0, (maxT - minT).TotalSeconds);

                foreach (var ev in _currentSite.Events)
                {
                    double sec = (ev.Timestamp - minT).TotalSeconds;
                    int px = ox + (int)((sec / totalSec) * w);
                    int py = oy - (ev.Raw[1] % Math.Max(20, h - 20)) - 10;
                    Brush b = ev.IsTransient ? Brushes.Crimson : Brushes.RoyalBlue;
                    g.FillEllipse(b, px - 4, py - 4, 8, 8);
                }
            }
        }

        private void DrawSingleTransientSineWaveGraph(Graphics g, Rectangle rect)
        {
            EventRecord ev = _selectedTransientEvent;
            if (ev == null || !ev.IsSingleTransient)
            {
                ev = GetSelectedEventRecord();
                if (ev == null || !ev.IsSingleTransient)
                {
                    ev = _currentSite.Events.LastOrDefault(x => x.IsSingleTransient);
                    _selectedTransientEvent = ev;
                }
            }

            if (ev == null || !ev.IsSingleTransient)
            {
                using (var fnt = new Font("MS Sans Serif", 10.5f * _fontScale, FontStyle.Bold))
                {
                    g.DrawString(
                        T("В текущем журнале не выбрано (или отсутствует) событие одиночного импульса ('1 H-N Transient' / '1 N-G Transient').\n" +
                          "Перейдите на вкладку 'События' и дважды кликните по любой строке одиночного импульса.",
                          "No single transient event ('1 H-N Transient' / '1 N-G Transient') is selected or present in memory.\n" +
                          "Go to the 'Events' tab and double-click any single transient row."),
                        fnt, Brushes.DarkRed, new Point(25, 35));
                }
                return;
            }

            double nomRms = _currentSite.Is220V ? 220.0 : 110.0;
            double sinePeak = ev.IsNgTransient ? 0.0 : Math.Round(nomRms * Math.Sqrt(2.0), 1);
            double degAngle = Math.Max(0, Math.Min(360, ev.PhaseDegrees));
            double radAngle = degAngle * Math.PI / 180.0;

            double instSineVolt = sinePeak * Math.Sin(radAngle);
            double transientDelta = ev.TransientPeakSigned;
            double totalPeakVolt = instSineVolt + transientDelta;

            double maxAbsVolt = Math.Max(400.0, Math.Max(Math.Abs(totalPeakVolt), sinePeak) + 100.0);
            maxAbsVolt = Math.Ceiling(maxAbsVolt / 100.0) * 100.0;

            int leftMargin = (int)(95 * _fontScale);
            int rightMargin = 45;
            int topMargin = (int)(88 * _fontScale);
            int bottomMargin = (int)(70 * _fontScale);

            int plotW = Math.Max(200, rect.Width - leftMargin - rightMargin);
            int plotH = Math.Max(180, rect.Height - topMargin - bottomMargin);
            int zeroY = topMargin + plotH / 2;

            string title = T(
                $"Осциллограмма одиночного импульса (Single Transient Graph) — Событие №{ev.Index} ({ev.GetLocalizedEventName(_lang)})",
                $"Single Transient Graph — Event #{ev.Index} ({ev.GetLocalizedEventName(_lang)})");
            string subtitle = T(
                $"Время: {ev.Timestamp:dd.MM.yyyy HH:mm:ss}   |   Отклонение импульса: {ev.ExtremeText}   |   Фазовый угол: {degAngle:0}°   |   Мгновенный пик на линии: {totalPeakVolt:+0;-0;0} Vp",
                $"Start Time: {ev.Timestamp:dd.MM.yyyy HH:mm:ss}   |   Transient Deviation: {ev.ExtremeText}   |   Phase Angle: {degAngle:0}°   |   Absolute Line Peak: {totalPeakVolt:+0;-0;0} Vp");

            using (var titleFont = new Font("MS Sans Serif", 11f * _fontScale, FontStyle.Bold))
            using (var subFont = new Font("MS Sans Serif", 9.2f * _fontScale, FontStyle.Regular))
            {
                g.DrawString(title, titleFont, Brushes.DarkBlue, new Point(leftMargin, 14));
                g.DrawString(subtitle, subFont, Brushes.Black, new Point(leftMargin, (int)(16 + titleFont.Height + 4)));
            }

            Rectangle plotRect = new Rectangle(leftMargin, topMargin, plotW, plotH);
            g.FillRectangle(new SolidBrush(Color.FromArgb(252, 253, 255)), plotRect);
            g.DrawRectangle(Pens.Gray, plotRect);

            using (Pen gridPen = new Pen(Color.LightGray, 1f) { DashStyle = DashStyle.Dot })
            using (Font axisFont = new Font("Consolas", 8.5f * _fontScale))
            {
                int vSteps = 4;
                for (int i = -vSteps; i <= vSteps; i++)
                {
                    double vVal = (maxAbsVolt / vSteps) * i;
                    int y = zeroY - (int)((vVal / maxAbsVolt) * (plotH / 2.0));
                    g.DrawLine(gridPen, leftMargin, y, leftMargin + plotW, y);
                    string vLabel = $"{vVal:+0;-0;0} V";
                    SizeF sz = g.MeasureString(vLabel, axisFont);
                    g.DrawString(vLabel, axisFont, Brushes.Black, leftMargin - sz.Width - 6, y - sz.Height / 2);
                }

                for (int deg = 0; deg <= 360; deg += 45)
                {
                    int x = leftMargin + (int)((deg / 360.0) * plotW);
                    g.DrawLine(gridPen, x, topMargin, x, topMargin + plotH);
                    string dLabel = $"{deg}°";
                    SizeF sz = g.MeasureString(dLabel, axisFont);
                    g.DrawString(dLabel, axisFont, Brushes.Black, x - sz.Width / 2, topMargin + plotH + 6);
                }
            }

            using (Pen zeroPen = new Pen(Color.Black, 1.5f))
            {
                g.DrawLine(zeroPen, leftMargin, zeroY, leftMargin + plotW, zeroY);
            }

            List<PointF> sinePoints = new List<PointF>(361);
            for (int d = 0; d <= 360; d++)
            {
                float x = leftMargin + (float)((d / 360.0) * plotW);
                double v = sinePeak * Math.Sin(d * Math.PI / 180.0);
                float y = zeroY - (float)((v / maxAbsVolt) * (plotH / 2.0));
                sinePoints.Add(new PointF(x, y));
            }

            using (Pen sinePen = new Pen(Color.FromArgb(40, 110, 180), 2.2f))
            {
                g.DrawLines(sinePen, sinePoints.ToArray());
            }

            float spikeX = leftMargin + (float)((degAngle / 360.0) * plotW);
            float baseSineY = zeroY - (float)((instSineVolt / maxAbsVolt) * (plotH / 2.0));
            float peakSpikeY = zeroY - (float)((totalPeakVolt / maxAbsVolt) * (plotH / 2.0));

            float halfWidthPx = Math.Max(2.5f, plotW / 140f);
            PointF[] spikePoly = new PointF[]
            {
                new PointF(spikeX - halfWidthPx, baseSineY),
                new PointF(spikeX, peakSpikeY),
                new PointF(spikeX + halfWidthPx, baseSineY)
            };

            using (Pen spikePen = new Pen(Color.Crimson, 2.5f))
            {
                g.DrawLines(spikePen, spikePoly);
            }

            using (Pen dashMarker = new Pen(Color.DarkRed, 1f) { DashStyle = DashStyle.Dash })
            {
                g.DrawLine(dashMarker, spikeX, topMargin, spikeX, topMargin + plotH);
            }
            g.FillEllipse(Brushes.Crimson, spikeX - 4.5f, peakSpikeY - 4.5f, 9f, 9f);
            g.FillEllipse(Brushes.DarkBlue, spikeX - 3.5f, baseSineY - 3.5f, 7f, 7f);

            string calloutText = T(
                $"Импульс: {ev.ExtremeText} @ {degAngle:0}°\nСинусоида: {instSineVolt:+0;-0;0} V\nСуммарный пик: {totalPeakVolt:+0;-0;0} Vp",
                $"Spike: {ev.ExtremeText} @ {degAngle:0}°\nSine Base: {instSineVolt:+0;-0;0} V\nTotal Peak: {totalPeakVolt:+0;-0;0} Vp");

            using (Font calloutFont = new Font("MS Sans Serif", 8.5f * _fontScale, FontStyle.Bold))
            {
                SizeF boxSz = g.MeasureString(calloutText, calloutFont);
                float boxX = spikeX + 12;
                if (boxX + boxSz.Width + 12 > leftMargin + plotW)
                    boxX = spikeX - boxSz.Width - 18;

                float boxY = Math.Max(topMargin + 8, Math.Min(topMargin + plotH - boxSz.Height - 12, peakSpikeY - boxSz.Height / 2));
                RectangleF calloutRect = new RectangleF(boxX, boxY, boxSz.Width + 10, boxSz.Height + 6);

                g.FillRectangle(new SolidBrush(Color.FromArgb(255, 250, 225)), calloutRect);
                g.DrawRectangle(Pens.Peru, calloutRect.X, calloutRect.Y, calloutRect.Width, calloutRect.Height);
                g.DrawString(calloutText, calloutFont, Brushes.DarkRed, boxX + 5, boxY + 3);
            }

            string xTitle = T(
                $"Фазовый угол периода сети ({(_currentSite.Is50Hz ? "50 Гц, 1 период = 20.0 мс" : "60 Гц, 1 период = 16.67 мс")})",
                $"Line Cycle Phase Angle ({(_currentSite.Is50Hz ? "50 Hz, 1 cycle = 20.0 ms" : "60 Hz, 1 cycle = 16.67 ms")})");
            using (Font fntX = new Font("MS Sans Serif", 9f * _fontScale, FontStyle.Bold))
            {
                SizeF xSz = g.MeasureString(xTitle, fntX);
                g.DrawString(xTitle, fntX, Brushes.Black, leftMargin + (plotW - xSz.Width) / 2, topMargin + plotH + (int)(26 * _fontScale));
            }
        }

        private void OnOpenVerFile(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog { Filter = "Site Files (*.ver)|*.ver|All Files (*.*)|*.*" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        _currentSite = SiteData.LoadFromVerFile(ofd.FileName);
                        _selectedTransientEvent = _currentSite.Events.LastOrDefault(x => x.IsSingleTransient);
                        this.Text = $"Fluke EventView EV101 - [SN F{_currentSite.SerialNumber} - {Path.GetFileName(ofd.FileName)}]";
                        PopulateSetupInputsFromSite(_currentSite);
                        UpdateRealtimeAndInfoPanels(_currentSite);
                        PopulateEventsGrid(_currentSite);
                        LogMessage($"Loaded file \"{ofd.FileName}\" ({_currentSite.Events.Count} events).");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void OnSaveVerFile(object sender, EventArgs e)
        {
            if (!_currentSite.HasValidData)
            {
                MessageBox.Show(
                    T("Нет данных для сохранения! Сначала подключитесь к прибору или откройте файл.",
                      "No device data to save! Connect to a recorder first."),
                    "Fluke EventView", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var sfd = new SaveFileDialog { Filter = "Site Files (*.ver)|*.ver", FileName = $"snf{_currentSite.SerialNumber}.ver" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    _currentSite.Description = _txtSiteDesc.Text;
                    _currentSite.SaveToVerFile(sfd.FileName);
                    LogMessage($"File Saved: {sfd.FileName}");
                }
            }
        }

        private void OnExportCsv(object sender, EventArgs e)
        {
            if (!_currentSite.HasValidData)
            {
                MessageBox.Show(
                    T("Нет данных для экспорта! Сначала подключитесь к прибору или откройте файл.",
                      "No device data to export! Connect to a recorder first."),
                    "Fluke EventView", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var sfd = new SaveFileDialog { Filter = "Comma Delimited (*.csv)|*.csv|Tab Delimited (*.asc)|*.asc|Plain Text (*.txt)|*.txt", FileName = $"snf{_currentSite.SerialNumber}.csv" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Event,Start Time,Event,Extreme,End,RawHex");
                    foreach (var openEv in _currentSite.OpenEvents)
                        sb.AppendLine($"0,\"{openEv.Timestamp:dd.MM.yyyy HH:mm:ss}\",\"{openEv.GetLocalizedEventName(_lang)}\",\"{openEv.ExtremeText}\",\"{openEv.GetLocalizedEndText(_lang)}\",\"{BitConverter.ToString(openEv.Raw)}\"");
                    for (int i = _currentSite.Events.Count - 1; i >= 0; i--)
                    {
                        var ev = _currentSite.Events[i];
                        sb.AppendLine($"{ev.Index},\"{ev.Timestamp:dd.MM.yyyy HH:mm:ss}\",\"{ev.GetLocalizedEventName(_lang)}\",\"{ev.ExtremeText}\",\"{ev.GetLocalizedEndText(_lang)}\",\"{BitConverter.ToString(ev.Raw)}\"");
                    }
                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show(T("Файл успешно сохранен!", "File Saved!"), "Fluke EventView", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopScanningPort();
            base.OnFormClosing(e);
        }
    }

    // =========================================================================
    // КЛАСС ДАННЫХ ПРИБОРА И ТОЧНЫЙ ПАРСЕР ФОРМАТА .VER (VB3 152B Header + 16B Events)
    // =========================================================================
    public class SiteData
    {
        public bool HasValidData { get; set; } = false;

        public string Description { get; set; } = "";
        public int SerialNumber { get; set; } = 0;
        public int FirmwareVersion { get; set; } = 0;
        public byte OptionsByte { get; set; } = 0x00;

        public uint ElapsedTicks { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public bool Is220V { get; set; } = true;
        public bool Is50Hz { get; set; } = true;
        public bool IsFifo { get; set; } = true;
        public bool FlashLed { get; set; } = true;
        public bool NgScale025 { get; set; } = false;
        public bool IsMemoryFull { get; set; } = false;

        public int HnSag { get; set; } = 210;
        public int HnSwell { get; set; } = 256;
        public int HnImpulse { get; set; } = 200;
        public double NgSurge { get; set; } = 10.0;
        public int NgImpulse { get; set; } = 100;
        public double MinFreq { get; set; } = 48.8;
        public double MaxFreq { get; set; } = 51.2;

        public double LiveHnVolt { get; set; }
        public double LiveNgVolt { get; set; }
        public double LiveFreq { get; set; }

        public int StoredEventCount { get; set; }
        public byte[] RawHeader88 { get; set; } = new byte[88];
        public byte[] RawHeader150 { get; set; } = new byte[150];

        public List<EventRecord> OpenEvents { get; set; } = new List<EventRecord>();
        public List<EventRecord> Events { get; set; } = new List<EventRecord>();

        public static SiteData ParseFromDeviceHeader(byte[] h, DateTime nowTime)
        {
            var s = new SiteData { HasValidData = true };
            Array.Copy(h, 0, s.RawHeader88, 0, Math.Min(88, h.Length));

            s.ElapsedTicks = (uint)((h[0] << 16) | (h[1] << 8) | h[2]);

            uint endTicks = (uint)((h[82] << 16) | (h[83] << 8) | h[84]);
            if (endTicks == 0 || endTicks > s.ElapsedTicks) endTicks = s.ElapsedTicks;

            s.StartTime = nowTime.AddSeconds(-(s.ElapsedTicks * 8.0));
            s.EndTime = s.StartTime.AddSeconds(endTicks * 8.0);

            byte b51 = h[51];
            s.Is220V = (b51 & 0x80) != 0;
            s.FirmwareVersion = b51 & 0x0F;
            s.NgScale025 = (b51 & 0x10) != 0;
            double vMult = s.Is220V ? 2.0 : 1.0;

            s.LiveHnVolt = h[3] * vMult;
            s.LiveNgVolt = s.NgScale025 ? (h[4] * 0.25) : h[4];

            int liveFreqWord = (h[56] << 8) | h[57];
            s.LiveFreq = liveFreqWord > 100 ? Math.Round(57600.0 / liveFreqWord, 1) : 50.0;

            s.OptionsByte = h[6];
            s.Is50Hz = (s.OptionsByte & 0x04) != 0;
            s.FlashLed = (s.OptionsByte & 0x02) == 0;
            s.IsFifo = (s.OptionsByte & 0x01) != 0;

            int minFreqWord = (h[7] << 8) | h[8];
            int maxFreqWord = (h[9] << 8) | h[10];
            if (minFreqWord > 100) s.MinFreq = Math.Round(57600.0 / minFreqWord, 1);
            if (maxFreqWord > 100) s.MaxFreq = Math.Round(57600.0 / maxFreqWord, 1);

            s.HnSag = (int)Math.Round(h[11] * vMult);
            s.HnSwell = (int)Math.Round(h[12] * vMult);
            s.HnImpulse = h[13] * 10;
            s.NgSurge = s.NgScale025 ? Math.Round(h[14] * 0.25, 1) : h[14];
            s.NgImpulse = h[15] * 10;

            s.Description = Encoding.ASCII.GetString(h, 20, 30).Trim('\0', ' ');
            s.SerialNumber = (h[52] << 8) | h[53];

            int ptrBytes = (h[54] << 8) | h[55];
            if (h[50] != 0)
            {
                s.IsMemoryFull = true;
                s.StoredEventCount = 4000;
            }
            else
            {
                s.IsMemoryFull = false;
                s.StoredEventCount = ptrBytes / 8;
            }

            if (s.StoredEventCount > 0)
            {
                for (int slot = 0; slot < 3; slot++)
                {
                    byte[] slotBytes = new byte[8];
                    Array.Copy(h, 64 + slot * 8, slotBytes, 0, 8);
                    if (slotBytes[0] != 0x00 && slotBytes[0] != 0xFF)
                    {
                        var openEv = EventRecord.Decode(slotBytes, 0, s.StartTime, s.Is220V, s.Is50Hz, s.NgScale025, true);
                        s.OpenEvents.Add(openEv);
                    }
                }
            }

            if (s.OpenEvents.Count > 0)
            {
                s.StartTime = s.OpenEvents[0].Timestamp.AddSeconds(-16);
            }

            return s;
        }

        public void ParseAllEvents(byte[] rawBuffer)
        {
            Events.Clear();
            int maxEvents = Math.Min(StoredEventCount, rawBuffer.Length / 8);
            for (int i = 0; i < maxEvents; i++)
            {
                byte[] b = new byte[8];
                Array.Copy(rawBuffer, i * 8, b, 0, 8);
                if (b[0] == 0xFF || b[0] == 0x00) continue;
                Events.Add(EventRecord.Decode(b, i + 1, StartTime, Is220V, Is50Hz, NgScale025, false));
            }
        }

        /// <summary>
        /// Чтение оригинального файла .VER (152-байтный заголовок VB3 + события по 16 байт [8 x UInt16])
        /// </summary>
        public static SiteData LoadFromVerFile(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            if (data.Length < 152)
                throw new InvalidDataException("Файл .VER поврежден или имеет неверный размер (< 152 байт).");

            var s = new SiteData { HasValidData = true };
            Array.Copy(data, 2, s.RawHeader150, 0, 150);

            // 1. Время окончания (смещение 2..9) и время старта (смещение 10..17) в формате OLE Automation Date
            s.EndTime = DateTime.FromOADate(BitConverter.ToDouble(data, 2));
            s.StartTime = DateTime.FromOADate(BitConverter.ToDouble(data, 10));

            // 2. Тики работы прибора (смещение 18..21) и Серийный номер (смещение 22..25)
            s.ElapsedTicks = BitConverter.ToUInt32(data, 18);
            s.SerialNumber = BitConverter.ToInt32(data, 22);

            // 3. Байт аппаратной модификации и прошивки (смещение 92..93, например 0x0089 -> 220V, FW v9)
            byte b51 = data[92];
            if (b51 == 0) b51 = 0x89;
            s.Is220V = (b51 & 0x80) != 0;
            s.FirmwareVersion = b51 & 0x0F;
            s.NgScale025 = (b51 & 0x10) != 0;
            double vMult = s.Is220V ? 2.0 : 1.0;

            // 4. Живые показания на момент сохранения файла
            s.LiveHnVolt = BitConverter.ToUInt16(data, 26) * vMult;
            s.LiveNgVolt = s.NgScale025 ? (data[29] * 0.25) : data[29];
            double savedFreq = BitConverter.ToDouble(data, 102);
            s.LiveFreq = (savedFreq > 40.0 && savedFreq < 70.0) ? Math.Round(savedFreq, 1) : 50.0;

            // 5. Опции (смещение 32..33)
            s.OptionsByte = data[32];
            s.Is50Hz = (s.OptionsByte & 0x04) != 0;
            s.FlashLed = (s.OptionsByte & 0x02) == 0;
            s.IsFifo = (s.OptionsByte & 0x01) != 0;

            // 6. Пороги частоты (по смещению 34 лежит MaxFreqWord, по смещению 38 — MinFreqWord)
            int wordA = BitConverter.ToInt32(data, 34);
            int wordB = BitConverter.ToInt32(data, 38);
            if (wordA > 100 && wordB > 100)
            {
                double fA = Math.Round(57600.0 / wordA, 1);
                double fB = Math.Round(57600.0 / wordB, 1);
                s.MinFreq = Math.Min(fA, fB);
                s.MaxFreq = Math.Max(fA, fB);
            }

            // 7. Пороги напряжений (смещения 42, 44, 46, 48, 50 — по 2 байта Int16)
            s.HnSag = (int)Math.Round(BitConverter.ToUInt16(data, 42) * vMult);
            s.HnSwell = (int)Math.Round(BitConverter.ToUInt16(data, 44) * vMult);
            s.HnImpulse = BitConverter.ToUInt16(data, 46) * 10;
            ushort rawNgSwell = BitConverter.ToUInt16(data, 48);
            s.NgSurge = s.NgScale025 ? Math.Round(rawNgSwell * 0.25, 1) : rawNgSwell;
            s.NgImpulse = BitConverter.ToUInt16(data, 50) * 10;

            // 8. Описание сайта (смещение 52..81, 30 символов ASCII)
            s.Description = Encoding.ASCII.GetString(data, 52, 30).Trim('\0', ' ');

            // 9. Реальное число записанных событий хранится в байтах по смещению 110 (ptrBytes / 8) и 114 (totBytes / 8)
            int ptrBytes = BitConverter.ToInt32(data, 110);
            int totBytes = BitConverter.ToInt32(data, 114);
            int headerEventCount = Math.Max(ptrBytes, totBytes) / 8;
            int remainingBytes = data.Length - 152;

            // 10. Три 8-байтовых слота активных событий "Open Event" (смещения 126..149)
            for (int slot = 0; slot < 3; slot++)
            {
                byte[] slotBytes = new byte[8];
                Array.Copy(data, 126 + slot * 8, slotBytes, 0, 8);
                if (slotBytes[0] != 0x00 && slotBytes[0] != 0xFF)
                {
                    var openEv = EventRecord.Decode(slotBytes, 0, s.StartTime, s.Is220V, s.Is50Hz, s.NgScale025, true);
                    s.OpenEvents.Add(openEv);
                }
            }

            // 11. Чтение массива событий (со смещения 152):
            // В оригинальном .VER каждое событие занимает 16 байт (8 слов UInt16 Little-Endian)
            if (remainingBytes >= 16 && remainingBytes % 16 == 0)
            {
                int evCount = remainingBytes / 16;
                if (headerEventCount > 0 && headerEventCount < evCount)
                    evCount = headerEventCount;

                s.StoredEventCount = evCount;
                byte[] packedEvents = new byte[evCount * 8];
                for (int e = 0; e < evCount; e++)
                {
                    for (int bIdx = 0; bIdx < 8; bIdx++)
                    {
                        packedEvents[e * 8 + bIdx] = data[152 + e * 16 + bIdx * 2];
                    }
                }
                s.ParseAllEvents(packedEvents);
            }
            else if (remainingBytes >= 8)
            {
                // Резервная поддержка для упакованных 8-байтных файлов
                s.StoredEventCount = remainingBytes / 8;
                byte[] evBuf = new byte[remainingBytes];
                Array.Copy(data, 152, evBuf, 0, remainingBytes);
                s.ParseAllEvents(evBuf);
            }

            return s;
        }

        /// <summary>
        /// Сохранение в 100% совместимый с оригиналом формат .VER (152 байта заголовка + 16 байт на каждое событие)
        /// </summary>
        public void SaveToVerFile(string path)
        {
            using (var fs = new FileStream(path, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(Encoding.ASCII.GetBytes(".2"));
                byte[] hdr = (byte[])RawHeader150.Clone();

                double vDiv = Is220V ? 2.0 : 1.0;
                BitConverter.GetBytes(EndTime.ToOADate()).CopyTo(hdr, 0);
                BitConverter.GetBytes(StartTime.ToOADate()).CopyTo(hdr, 8);
                BitConverter.GetBytes((int)ElapsedTicks).CopyTo(hdr, 16);
                BitConverter.GetBytes(SerialNumber).CopyTo(hdr, 20);
                BitConverter.GetBytes((short)Math.Round(LiveHnVolt / vDiv)).CopyTo(hdr, 24);
                BitConverter.GetBytes((short)OptionsByte).CopyTo(hdr, 30);

                int maxFWord = (int)Math.Round(57600.0 / MaxFreq);
                int minFWord = (int)Math.Round(57600.0 / MinFreq);
                BitConverter.GetBytes(maxFWord).CopyTo(hdr, 32);
                BitConverter.GetBytes(minFWord).CopyTo(hdr, 36);

                BitConverter.GetBytes((short)Math.Round(HnSag / vDiv)).CopyTo(hdr, 40);
                BitConverter.GetBytes((short)Math.Round(HnSwell / vDiv)).CopyTo(hdr, 42);
                BitConverter.GetBytes((short)(HnImpulse / 10)).CopyTo(hdr, 44);
                BitConverter.GetBytes((short)(NgScale025 ? Math.Round(NgSurge * 4.0) : Math.Round(NgSurge))).CopyTo(hdr, 46);
                BitConverter.GetBytes((short)(NgImpulse / 10)).CopyTo(hdr, 48);

                string d30 = (Description ?? "").PadRight(30, ' ').Substring(0, 30);
                Encoding.ASCII.GetBytes(d30).CopyTo(hdr, 50);

                BitConverter.GetBytes((short)3).CopyTo(hdr, 80);
                BitConverter.GetBytes(32000).CopyTo(hdr, 82);
                byte b51 = (byte)((Is220V ? 0x80 : 0x00) | (NgScale025 ? 0x10 : 0x00) | (FirmwareVersion & 0x0F));
                BitConverter.GetBytes((short)b51).CopyTo(hdr, 90);
                BitConverter.GetBytes(LiveFreq).CopyTo(hdr, 100);

                // Смещения 110 и 114 в файле (108 и 112 в массиве hdr): размер буфера событий в байтах (Count * 8)
                BitConverter.GetBytes(Events.Count * 8).CopyTo(hdr, 108);
                BitConverter.GetBytes(Events.Count * 8).CopyTo(hdr, 112);

                for (int slot = 0; slot < Math.Min(3, OpenEvents.Count); slot++)
                {
                    Array.Copy(OpenEvents[slot].Raw, 0, hdr, 124 + slot * 8, 8);
                }

                // Смещение 150 в файле (148 в массиве hdr): флаг наличия выгруженных событий (1 или 0)
                BitConverter.GetBytes((ushort)(Events.Count > 0 ? 1 : 0)).CopyTo(hdr, 148);
                bw.Write(hdr);

                foreach (var ev in Events)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        bw.Write((ushort)ev.Raw[i]);
                    }
                }
            }
        }
    }

    public class EventRecord
    {
        public int Index { get; set; }
        public byte[] Raw { get; set; } = new byte[8];
        public DateTime Timestamp { get; set; }
        public string EventNameEn { get; set; } = "";
        public string EventNameRu { get; set; } = "";
        public string ExtremeText { get; set; } = "";
        public string EndTextEn { get; set; } = "";
        public string EndTextRu { get; set; } = "";

        public bool IsOpenEvent { get; set; }
        public bool IsTransient { get; set; }
        public bool IsNgTransient { get; set; }
        public int TransientCount { get; set; }
        public bool IsSingleTransient => IsTransient && !IsOpenEvent && TransientCount == 1;
        public int TransientPeakSigned { get; set; }
        public int PhaseDegrees { get; set; }

        public string GetLocalizedEventName(AppLang lang) => lang == AppLang.Ru ? EventNameRu : EventNameEn;
        public string GetLocalizedEndText(AppLang lang) => lang == AppLang.Ru ? EndTextRu : EndTextEn;

        public static EventRecord Decode(byte[] b, int index, DateTime baseStart, bool is220, bool is50, bool ngScale025, bool isOpenEvent)
        {
            var r = new EventRecord { Index = index, Raw = b, IsOpenEvent = isOpenEvent };
            byte type = b[0];

            uint eventTicks = (uint)((b[5] << 16) | (b[6] << 8) | b[7]);
            r.Timestamp = baseStart.AddSeconds(eventTicks * 8.0);

            double vMult = is220 ? 2.0 : 1.0;
            double baseFreq = is50 ? 50.0 : 60.0;
            double ticksPerDeg = is50 ? 3.2 : (960.0 / 360.0);

            uint rawDur = (uint)((b[2] << 16) | (b[3] << 8) | b[4]);
            // Коды 0x06, 0x86, 0x08, 0x88 хранят длительность в 8-секундных тиках, а 0x05, 0x85, 0x07, 0x87 — в полупериодах!
            bool durIn8SecTicks = (type == 0x06 || type == 0x86 || type == 0x08 || type == 0x88);

            string durEn = isOpenEvent ? "Open Event" : (durIn8SecTicks ? FormatTickDuration(rawDur, AppLang.En) : FormatHalfCycles(rawDur, baseFreq, AppLang.En));
            string durRu = isOpenEvent ? "Активное событие" : (durIn8SecTicks ? FormatTickDuration(rawDur, AppLang.Ru) : FormatHalfCycles(rawDur, baseFreq, AppLang.Ru));

            switch (type)
            {
                case 0x01:
                case 0x02:
                    string sign = (type == 0x01) ? "-" : "+";
                    int count = b[2] > 0 ? b[2] : 1;
                    int phaseTicks = (b[3] << 8) | b[4];
                    int deg = (int)Math.Floor((phaseTicks / ticksPerDeg) + 0.625);

                    r.IsTransient = true;
                    r.IsNgTransient = false;
                    r.TransientCount = count;
                    r.TransientPeakSigned = (type == 0x01 ? -1 : 1) * (b[1] * 10);
                    r.PhaseDegrees = deg;

                    r.EventNameEn = count == 1 ? "1 H-N Transient" : $"{count} H-N Transients";
                    r.EventNameRu = count == 1 ? "1 Импульс H-N" : $"{count} Импульсов H-N";
                    r.ExtremeText = $"{sign}{b[1] * 10} Vp";
                    r.EndTextEn = $"{deg}°";
                    r.EndTextRu = $"{deg}°";
                    break;

                case 0x03:
                case 0x04:
                    string ngSign = (type == 0x03) ? "-" : "+";
                    int ngCount = b[2] > 0 ? b[2] : 1;
                    int ngTicks = (b[3] << 8) | b[4];
                    int ngDeg = (int)Math.Floor((ngTicks / ticksPerDeg) + 0.625);

                    r.IsTransient = true;
                    r.IsNgTransient = true;
                    r.TransientCount = ngCount;
                    r.TransientPeakSigned = (type == 0x03 ? -1 : 1) * (b[1] * 10);
                    r.PhaseDegrees = ngDeg;

                    r.EventNameEn = ngCount == 1 ? "1 N-G Transient" : $"{ngCount} N-G Transients";
                    r.EventNameRu = ngCount == 1 ? "1 Импульс N-G" : $"{ngCount} Импульсов N-G";
                    r.ExtremeText = $"{ngSign}{b[1] * 10} Vp";
                    r.EndTextEn = $"{ngDeg}°";
                    r.EndTextRu = $"{ngDeg}°";
                    break;

                case 0x85: // H-N Swell (длительность в полупериодах)
                case 0x86: // H-N Swell (длительность в 8-сек тиках)
                case 0x16:
                    r.EventNameEn = "H-N Swell";
                    r.EventNameRu = "Перенапряжение H-N";
                    r.ExtremeText = $"{(b[1] * vMult):0}   Vrms";
                    r.EndTextEn = durEn;
                    r.EndTextRu = durRu;
                    break;

                case 0x05: // H-N Sag (длительность в полупериодах)
                case 0x06: // H-N Sag (длительность в 8-сек тиках)
                case 0x17:
                    if (b[1] == 0)
                    {
                        r.EventNameEn = "Outage";
                        r.EventNameRu = "Обрыв питания";
                        r.ExtremeText = "0 Vrms";
                    }
                    else
                    {
                        r.EventNameEn = "H-N Sag";
                        r.EventNameRu = "Провал H-N";
                        r.ExtremeText = $"{(b[1] * vMult):0}   Vrms";
                    }
                    r.EndTextEn = durEn;
                    r.EndTextRu = durRu;
                    break;

                case 0x07: // Low Frequency (в полупериодах)
                case 0x08: // Low Frequency (в 8-сек тиках)
                    r.EventNameEn = "Low Frequency";
                    r.EventNameRu = "Низкая частота";
                    double lowF = baseFreq - ((b[1] - 1) * 0.1);
                    r.ExtremeText = $"{lowF:00.0} Hz";
                    r.EndTextEn = durEn;
                    r.EndTextRu = durRu;
                    break;

                case 0x87: // High Frequency (в полупериодах)
                case 0x88: // High Frequency (в 8-сек тиках)
                    r.EventNameEn = "High Frequency";
                    r.EventNameRu = "Высокая частота";
                    double highF = baseFreq + (b[1] * 0.1);
                    r.ExtremeText = $"{highF:00.0} Hz";
                    r.EndTextEn = durEn;
                    r.EndTextRu = durRu;
                    break;

                case 0x12:
                case 0x14:
                case 0x09:
                case 0x89:
                    r.EventNameEn = "N-G Swell";
                    r.EventNameRu = "Превышение N-G";
                    double ngV = ngScale025 ? (b[1] * 0.25) : b[1];
                    r.ExtremeText = $"{ngV:0.0} Vrms";
                    r.EndTextEn = durEn;
                    r.EndTextRu = durRu;
                    break;

                case 0x80:
                    r.EventNameEn = "Outage";
                    r.EventNameRu = "Обрыв питания";
                    r.ExtremeText = "0 Vrms";
                    r.EndTextEn = durEn;
                    r.EndTextRu = durRu;
                    break;

                default:
                    r.EventNameEn = $"Event 0x{type:X2}";
                    r.EventNameRu = $"Событие 0x{type:X2}";
                    r.ExtremeText = $"{(b[1] * vMult):0} V";
                    r.EndTextEn = durEn;
                    r.EndTextRu = durRu;
                    break;
            }

            return r;
        }

        private static string FormatHalfCycles(uint rawHalfCycles, double lineFreq, AppLang lang)
        {
            double cycles = (rawHalfCycles + 1) / 2.0;
            if (cycles < 50.0)
                return lang == AppLang.Ru ? $"{cycles:0.0} период." : $"{cycles:0.0} cycles";

            double seconds = cycles / lineFreq;
            if (seconds < 60.0)
                return lang == AppLang.Ru ? $"{seconds:0.0} сек" : $"{seconds:0.0} seconds";

            int mins = (int)(seconds / 60);
            double remSec = seconds - (mins * 60);
            if (mins < 60)
                return lang == AppLang.Ru ? $"{mins} мин, {remSec:0.0} сек" : $"{mins} min, {remSec:0.0} sec";

            return TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");
        }

        private static string FormatTickDuration(uint ticks, AppLang lang)
        {
            double seconds = ticks * 8.0;
            if (seconds < 60.0)
                return lang == AppLang.Ru ? $"{seconds:0.0} сек" : $"{seconds:0.0} seconds";

            int mins = (int)(seconds / 60);
            int remSec = (int)(seconds - (mins * 60));
            if (mins < 60)
                return lang == AppLang.Ru ? $"{mins} мин, {remSec} сек" : $"{mins} minutes, {remSec} seconds";

            return TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");
        }
    }
}