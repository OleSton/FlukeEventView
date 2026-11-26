// SPDX-License-Identifier: MIT
// Copyright (c) 2026 OleSton
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
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
    public partial class Form1 : Form
    {
        private AppLang _lang = AppLang.Ru;

        // Коэффициент масштабирования шрифта (1.0f = 100%, диапазон 0.80f .. 1.60f)
        private float _fontScale = 1.0f;
        private readonly Dictionary<Control, Font> _baseFonts = new Dictionary<Control, Font>();

        // --- ЭЛЕМЕНТЫ ГЛАВНОГО МЕНЮ (MenuStrip) ---
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

        // --- ПАНЕЛЬ ИНСТРУМЕНТОВ (ToolStrip) ---
        private ToolStrip _toolBar;
        private ToolStripButton _tsbOpen, _tsbSave, _tsbExport, _tsbRefreshPorts;
        private ToolStripButton _tsbFontDec, _tsbFontReset, _tsbFontInc;
        private ToolStripLabel _tslPort, _tslLang;
        private ComboBox _cmbLang;

        // --- СТРОКА СОСТОЯНИЯ (StatusStrip) И ИНДИКАТОРЫ СВЯЗИ ---
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _lblStatus;
        private ToolStripProgressBar _progressBar;
        private ToolStripStatusLabel _lblPortStatus;
        private ToolStripStatusLabel _lblDeviceOnlineLed;
        private ToolStripStatusLabel _lblTxLed;
        private ToolStripStatusLabel _lblRxLed;

        private Bitmap _bmpLedGray;
        private Bitmap _bmpLedYellow;
        private Bitmap _bmpLedGreenOn;
        private Bitmap _bmpLedGreenOff;
        private Bitmap _bmpLedTxOn;
        private Bitmap _bmpLedTxOff;

        private System.Windows.Forms.Timer _ledBlinkTimer;
        private volatile int _lastTxTick = -10000;
        private volatile int _lastRxTick = -10000;
        private volatile int _lastValidDeviceTick = -10000;
        private bool _txLedCurrentlyLit = false;
        private bool _rxLedCurrentlyLit = false;

        private ToolTip _toolTip;

        // --- ГЛАВНЫЕ ВКЛАДКИ ОКНА ---
        private TabControl _mainTabs;
        private TabPage _tabStatus;
        private TabPage _tabEvents;
        private TabPage _tabCharts;
        private TabPage _tabLog;

        private SplitContainer _splitStatus;
        private Panel _pnlLeft;
        private Panel _pnlRight;
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

        // Правая колонка: Панель "RealTime" (Текущее, Мин, Макс за сессию)
        private Label _lblRtHdrParam, _lblRtHdrCur, _lblRtHdrMin, _lblRtHdrMax;
        private Label _lblRealLineTitle, _lblRealGndTitle, _lblRealFreqTitle;

        private Label _lblRealLineVolt, _lblMinLineVolt, _lblMaxLineVolt;
        private Label _lblRealGndVolt, _lblMinGndVolt, _lblMaxGndVolt;
        private Label _lblRealFreq, _lblMinFreqVal, _lblMaxFreqVal;
        private Button _btnResetMinMax;

        // Хранение экстремумов (Мин / Макс) за текущую сессию подключения
        private bool _hasSessionMinMax = false;
        private double _sessionMinHnVolt, _sessionMaxHnVolt;
        private double _sessionMinNgVolt, _sessionMaxNgVolt;
        private double _sessionMinFreq, _sessionMaxFreq;

        // Панель "Recorder Info"
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

        // --- ЭЛЕМЕНТЫ ВКЛАДКИ "EVENTS" И ПАНЕЛИ ФИЛЬТРАЦИИ ПО ТИПУ И ДАТЕ/ВРЕМЕНИ ---
        private FlowLayoutPanel _pnlEventsFilter;
        private Label _lblFilterTitle;
        private ComboBox _cmbEventFilter;
        private CheckBox _chkEnableDateFilter;
        private DateTimePicker _dtpFilterFrom;
        private Label _lblDateTo;
        private DateTimePicker _dtpFilterTo;
        private Button _btnResetFilter;
        private CheckBox _chkFilterChartsAndCsv;
        private Label _lblFilterCount;
        private bool _suppressFilterEvent = false;
        private DataGridView _gridEvents;

        // --- ЭЛЕМЕНТЫ ВКЛАДКИ "GRAPHS" И "LOG" ---
        private Panel _pnlCharts;
        private Label _lblChartStyle;
        private ComboBox _cmbChartType;
        private Button _btnSaveChartImg;
        private Label _lblChartHint;
        private TextBox _txtCommLog;

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

        private SiteData _currentSite = new SiteData();

        public Form1()
        {
            this.KeyPreview = true;
            InitLedBitmaps();
            BuildEventViewInterface();
            CaptureBaseFonts(this);
            InitTimers();
            RefreshComPorts();
            ApplyLocalization();
            ApplyParityLocalization();
            LogMessage(T("Fluke EventView V2.16 инициализирован.", "Fluke EventView V2.16 initialized."));
            LogMessage(T("Выберите COM-порт и нажмите 'Старт опроса' для подключения к VR101.", "Select COM port and click 'Start Scanning' to connect to VR101."));
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            AdjustStatusSplitterForScale();
            AlignStatusColumnBottoms();
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

        // =========================================================================
        // УЧЕТ И СБРОС МИНИМАЛЬНЫХ / МАКСИМАЛЬНЫХ ЗНАЧЕНИЙ ЗА СЕССИЮ ПОДКЛЮЧЕНИЯ
        // =========================================================================
        /// <summary>
        /// Оригинальный EventView выводит сообщение «Loss of Synchronization», когда теряет
        /// синхронизацию с регистратором. Отдельного кода события для этого сообщения не
        /// установлено, поэтому признаком служит откат счётчика наработки прибора назад:
        /// это означает, что прибор был сброшен либо терял питание/синхронизацию.
        /// </summary>
        private void CheckSynchronizationLoss(SiteData live)
        {
            if (live == null || !live.HasValidData) return;

            bool lost = _prevElapsedTicks > 0 && live.ElapsedTicks + 2 < _prevElapsedTicks;

            if (lost && DateTime.Now < _suppressSyncLossUntil)
            {
                // Откат вызван нашей записью уставок — не считаем это потерей синхронизации
                _prevElapsedTicks = live.ElapsedTicks;
                return;
            }

            if (lost && (DateTime.Now - _lastSyncLossReport).TotalSeconds > 20)
            {
                _lastSyncLossReport = DateTime.Now;
                LogMessage(T(
                    $"{DateTime.Now:dd.MM.yyyy HH:mm:ss} — Loss of Synchronization (наработка прибора сброшена: {_prevElapsedTicks} -> {live.ElapsedTicks} тиков)",
                    $"{DateTime.Now:dd.MM.yyyy HH:mm:ss} - Loss of Synchronization (recorder uptime reset: {_prevElapsedTicks} -> {live.ElapsedTicks} ticks)"));
                _lblStatus.Text = T(
                    "Потеря синхронизации с прибором: наработка сброшена (прибор перезапускался).",
                    "Loss of Synchronization: recorder uptime was reset.");

                // Добавляем запись в таблицу как событие, обнаруженное программой
                var syn = new EventRecord
                {
                    Index = 0,
                    Raw = new byte[8],
                    IsSynthetic = true,
                    IsOpenEvent = false,
                    Category = EventCategory.LossOfSync,
                    Timestamp = DateTime.Now,
                    EventNameRu = "Потеря синхронизации",
                    EventNameEn = "Loss of Synchronization",
                    ExtremeText = "—",
                    EndTextRu = $"наработка сброшена: {_prevElapsedTicks} → {live.ElapsedTicks} тиков",
                    EndTextEn = $"uptime reset: {_prevElapsedTicks} -> {live.ElapsedTicks} ticks"
                };

                _syntheticEvents.Add(syn);
                if (_syntheticEvents.Count > 200) _syntheticEvents.RemoveAt(0);

                PopulateEventsGrid(_currentSite);
            }

            _prevElapsedTicks = live.ElapsedTicks;
        }

        private void ResetSessionMinMax()
        {
            _hasSessionMinMax = false;
            _sessionMinHnVolt = _sessionMaxHnVolt = 0;
            _sessionMinNgVolt = _sessionMaxNgVolt = 0;
            _sessionMinFreq = _sessionMaxFreq = 0;

            if (_isScanning && _currentSite != null && _currentSite.HasValidData)
            {
                UpdateSessionMinMax(_currentSite.LiveHnVolt, _currentSite.LiveNgVolt, _currentSite.LiveFreq);
            }

            UpdateRealtimeAndInfoPanels(_currentSite);
        }

        private void UpdateSessionMinMax(double hnVolt, double ngVolt, double freq)
        {
            if (!_hasSessionMinMax)
            {
                _sessionMinHnVolt = _sessionMaxHnVolt = hnVolt;
                _sessionMinNgVolt = _sessionMaxNgVolt = ngVolt;
                _sessionMinFreq = _sessionMaxFreq = freq;
                _hasSessionMinMax = true;
            }
            else
            {
                if (hnVolt < _sessionMinHnVolt) _sessionMinHnVolt = hnVolt;
                if (hnVolt > _sessionMaxHnVolt) _sessionMaxHnVolt = hnVolt;

                if (ngVolt < _sessionMinNgVolt) _sessionMinNgVolt = ngVolt;
                if (ngVolt > _sessionMaxNgVolt) _sessionMaxNgVolt = ngVolt;

                if (freq > 10.0)
                {
                    if (freq < _sessionMinFreq || _sessionMinFreq <= 10.0) _sessionMinFreq = freq;
                    if (freq > _sessionMaxFreq) _sessionMaxFreq = freq;
                }
            }
        }

        // =========================================================================
        // ГЕНЕРАЦИЯ СВЕТОДИОДНЫХ ИНДИКАТОРОВ И ОБРАБОТКА МИГАНИЯ TX / RX
        // =========================================================================
        private void InitLedBitmaps()
        {
            _bmpLedGray = CreateLedBitmap(Color.FromArgb(160, 165, 170), false);
            _bmpLedYellow = CreateLedBitmap(Color.FromArgb(255, 190, 0), true);
            _bmpLedGreenOn = CreateLedBitmap(Color.FromArgb(0, 215, 70), true);
            _bmpLedGreenOff = CreateLedBitmap(Color.FromArgb(45, 95, 55), false);
            _bmpLedTxOn = CreateLedBitmap(Color.FromArgb(255, 110, 0), true);
            _bmpLedTxOff = CreateLedBitmap(Color.FromArgb(100, 65, 45), false);
        }

        private static Bitmap CreateLedBitmap(Color baseColor, bool isLit)
        {
            Bitmap bmp = new Bitmap(14, 14);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(1, 1, 11, 11);

                using (SolidBrush fillBrush = new SolidBrush(baseColor))
                {
                    g.FillEllipse(fillBrush, rect);
                }

                if (isLit)
                {
                    using (SolidBrush glareBrush = new SolidBrush(Color.FromArgb(150, Color.White)))
                    {
                        g.FillEllipse(glareBrush, new Rectangle(3, 2, 5, 4));
                    }
                }

                using (Pen borderPen = new Pen(isLit ? Color.FromArgb(60, 60, 60) : Color.FromArgb(110, 110, 110), 1f))
                {
                    g.DrawEllipse(borderPen, rect);
                }
            }
            return bmp;
        }

        private void InitTimers()
        {
            _scanTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            _scanTimer.Tick += async (s, e) => await PollRecorderStatusAsync();

            _ledBlinkTimer = new System.Windows.Forms.Timer { Interval = 80 };
            _ledBlinkTimer.Tick += OnLedBlinkTimerTick;
            _ledBlinkTimer.Start();
        }

        private void PulseTxIndicator()
        {
            _lastTxTick = Environment.TickCount;
            if (!IsHandleCreated || IsDisposed) return;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (!_txLedCurrentlyLit && _lblTxLed != null)
                    {
                        _txLedCurrentlyLit = true;
                        _lblTxLed.Image = _bmpLedTxOn;
                        _lblTxLed.ForeColor = Color.FromArgb(180, 50, 0);
                    }
                }));
            }
            catch { }
        }

        private void PulseRxIndicator()
        {
            _lastRxTick = Environment.TickCount;
            if (!IsHandleCreated || IsDisposed) return;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (!_rxLedCurrentlyLit && _lblRxLed != null)
                    {
                        _rxLedCurrentlyLit = true;
                        _lblRxLed.Image = _bmpLedGreenOn;
                        _lblRxLed.ForeColor = Color.DarkGreen;
                    }
                }));
            }
            catch { }
        }

        private void OnLedBlinkTimerTick(object sender, EventArgs e)
        {
            int now = Environment.TickCount;

            bool txActive = unchecked((uint)(now - _lastTxTick)) < 160;
            if (txActive != _txLedCurrentlyLit)
            {
                _txLedCurrentlyLit = txActive;
                _lblTxLed.Image = txActive ? _bmpLedTxOn : _bmpLedTxOff;
                _lblTxLed.ForeColor = txActive ? Color.FromArgb(180, 50, 0) : Color.DimGray;
            }

            bool rxActive = unchecked((uint)(now - _lastRxTick)) < 160;
            if (rxActive != _rxLedCurrentlyLit)
            {
                _rxLedCurrentlyLit = rxActive;
                _lblRxLed.Image = rxActive ? _bmpLedGreenOn : _bmpLedGreenOff;
                _lblRxLed.ForeColor = rxActive ? Color.DarkGreen : Color.DimGray;
            }

            UpdateDeviceConnectionLedState(now);
        }

        private void UpdateDeviceConnectionLedState(int nowTick)
        {
            if (_lblDeviceOnlineLed == null) return;

            if (!_isScanning)
            {
                _lblDeviceOnlineLed.Image = _bmpLedGray;
                _lblDeviceOnlineLed.Text = T("Прибор: ОТКЛ", "VR101: OFFLINE");
                _lblDeviceOnlineLed.ForeColor = Color.DimGray;
            }
            else
            {
                bool isOnline = unchecked((uint)(nowTick - _lastValidDeviceTick)) < 3200;
                if (isOnline)
                {
                    _lblDeviceOnlineLed.Image = _bmpLedGreenOn;
                    _lblDeviceOnlineLed.Text = _currentSite.SerialNumber > 0
                        ? T($"VR101: НА СВЯЗИ (F{_currentSite.SerialNumber})", $"VR101: ONLINE (F{_currentSite.SerialNumber})")
                        : T("VR101: НА СВЯЗИ", "VR101: ONLINE");
                    _lblDeviceOnlineLed.ForeColor = Color.DarkGreen;
                }
                else
                {
                    _lblDeviceOnlineLed.Image = _bmpLedYellow;
                    _lblDeviceOnlineLed.Text = T("Поиск VR101...", "Waiting VR101...");
                    _lblDeviceOnlineLed.ForeColor = Color.FromArgb(160, 95, 0);
                }
            }
        }

        // =========================================================================
        // ПОСТРОЕНИЕ ПОЛЬЗОВАТЕЛЬСКОГО ИНТЕРФЕЙСА
        // =========================================================================
        private void BuildEventViewInterface()
        {
            this.Text = "Fluke EventView VR101";
            this.Size = new Size(1460, 850);
            this.MinimumSize = new Size(1260, 740);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("MS Sans Serif", 8.25f);

            // Иконка окна берётся из иконки самого exe-файла (app.ico задана в проекте)
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

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
                    T("FLUKE EventView VR101 для Windows\nПрограмма мониторинга и настройки регистратора Fluke VR101 / VR101S.\nПротокол: 9600, N, 8, 1 (Lite-Link).",
                      "FLUKE EventView VR101 for Windows\nFor use with the Fluke Voltage Event Recorder VR101 / VR101S.\nProtocol: 9600, N, 8, 1 (Lite-Link)."),
                    T("О программе EventView VR101", "About EventView VR101"),
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

            _toolBar.Items.Add(new ToolStripSeparator());
            _tsbFontDec = new ToolStripButton("🔉 A-", null, (s, e) => ChangeFontScale(-0.10f)) { Font = new Font("MS Sans Serif", 8.5f, FontStyle.Bold) };
            _tsbFontReset = new ToolStripButton("100%", null, (s, e) => ResetFontScale());
            _tsbFontInc = new ToolStripButton("🔊 A+", null, (s, e) => ChangeFontScale(0.10f)) { Font = new Font("MS Sans Serif", 8.5f, FontStyle.Bold) };
            _toolBar.Items.AddRange(new ToolStripItem[] { _tsbFontDec, _tsbFontReset, _tsbFontInc });

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

            // 3. Строка состояния (StatusStrip) со светодиодными индикаторами
            _statusStrip = new StatusStrip { SizingGrip = false, Padding = new Padding(4, 2, 10, 2) };
            _lblStatus = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _progressBar = new ToolStripProgressBar { Width = 150, Visible = false };

            _lblPortStatus = new ToolStripStatusLabel
            {
                BorderSides = ToolStripStatusLabelBorderSides.Left,
                Padding = new Padding(8, 0, 8, 0)
            };

            _lblDeviceOnlineLed = new ToolStripStatusLabel
            {
                Image = _bmpLedGray,
                ImageScaling = ToolStripItemImageScaling.None,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                BorderSides = ToolStripStatusLabelBorderSides.Left,
                Padding = new Padding(8, 0, 8, 0),
                Font = new Font("MS Sans Serif", 8.25f, FontStyle.Bold),
                ForeColor = Color.DimGray,
                Text = "Прибор: ОТКЛ"
            };

            _lblTxLed = new ToolStripStatusLabel
            {
                Image = _bmpLedTxOff,
                ImageScaling = ToolStripItemImageScaling.None,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                BorderSides = ToolStripStatusLabelBorderSides.Left,
                Padding = new Padding(8, 0, 6, 0),
                Font = new Font("Consolas", 9f, FontStyle.Bold),
                ForeColor = Color.DimGray,
                Text = "TX"
            };

            _lblRxLed = new ToolStripStatusLabel
            {
                Image = _bmpLedGreenOff,
                ImageScaling = ToolStripItemImageScaling.None,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                BorderSides = ToolStripStatusLabelBorderSides.Left,
                Padding = new Padding(6, 0, 8, 0),
                Font = new Font("Consolas", 9f, FontStyle.Bold),
                ForeColor = Color.DimGray,
                Text = "RX"
            };

            _statusStrip.Items.AddRange(new ToolStripItem[]
            {
                _lblStatus,
                _progressBar,
                _lblPortStatus,
                _lblDeviceOnlineLed,
                _lblTxLed,
                _lblRxLed
            });

            // 4. Вкладки главного окна
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

            BuildParityExtras();

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
                SplitterDistance = 720,
                FixedPanel = FixedPanel.Panel1,
                Padding = new Padding(6)
            };

            _pnlLeft = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(6) };
            var pnlLeft = _pnlLeft;

            // 1. Пороги Фаза — Нейтраль (H-N)
            _grpHn = new GroupBox { Dock = DockStyle.Top, Height = 132, Padding = new Padding(10) };
            _tblHn = CreateSettingsTable(3);
            _numHnSwell = CreateValidatedNum(100, 300, 256, 2, 0);
            _numHnSag = CreateValidatedNum(60, 240, 210, 2, 0);
            _numHnTransient = CreateValidatedNum(80, 2500, 200, 10, 0);

            _lblHnSwellTitle = AddRowWithLabelRef(_tblHn, 0, "", _numHnSwell);
            _lblHnSagTitle = AddRowWithLabelRef(_tblHn, 1, "", _numHnSag);
            _lblHnTransTitle = AddRowWithLabelRef(_tblHn, 2, "", _numHnTransient);
            _grpHn.Controls.Add(_tblHn);

            // 2. Пороги Нейтраль — Земля (N-G)
            _grpNg = new GroupBox { Dock = DockStyle.Top, Height = 100, Padding = new Padding(10) };
            _tblNg = CreateSettingsTable(2);
            _numNgSwell = CreateValidatedNum(1, 250, 10, 1, 0);
            _numNgTransient = CreateValidatedNum(80, 2500, 100, 10, 0);

            _lblNgSwellTitle = AddRowWithLabelRef(_tblNg, 0, "", _numNgSwell);
            _lblNgTransTitle = AddRowWithLabelRef(_tblNg, 1, "", _numNgTransient);
            _grpNg.Controls.Add(_tblNg);

            // 3. Пороги частоты сети
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

            // 4. Опции
            _grpOpt = new GroupBox { Dock = DockStyle.Top, Height = 112, Padding = new Padding(10) };
            var pnlOpt = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            _rbStopFull = new RadioButton { AutoSize = true };
            _rbFifo = new RadioButton { Checked = true, AutoSize = true };
            _chkFlashLed = new CheckBox { Checked = true, AutoSize = true };
            pnlOpt.Controls.AddRange(new Control[] { _rbStopFull, _rbFifo, _chkFlashLed });
            _grpOpt.Controls.Add(pnlOpt);

            // 5. Описание объекта
            _grpDesc = new GroupBox { Dock = DockStyle.Top, Height = 72, Padding = new Padding(10) };
            _txtSiteDesc = new TextBox { Text = "", MaxLength = 30, Dock = DockStyle.Top };
            _grpDesc.Controls.Add(_txtSiteDesc);

            pnlLeft.Controls.Add(_grpDesc);
            pnlLeft.Controls.Add(_grpOpt);
            pnlLeft.Controls.Add(_grpFreq);
            pnlLeft.Controls.Add(_grpNg);
            pnlLeft.Controls.Add(_grpHn);
            _splitStatus.Panel1.Controls.Add(pnlLeft);

            // --- ПРАВАЯ ПАНЕЛЬ: REALTIME (БЕЗ РАМОК, ЕДИНЫЙ ЦВЕТ СТОЛБЦОВ), RECORDER INFO И КНОПКИ ---
            _pnlRight = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(6) };
            var pnlRight = _pnlRight;

            _grpRealTime = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 200,
                Padding = new Padding(10),
                ForeColor = Color.DarkRed
            };

            _tblReal = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 4,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            _tblReal.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
            _tblReal.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            _tblReal.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            _tblReal.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            _tblReal.RowStyles.Add(new RowStyle(SizeType.Percent, 22f));
            _tblReal.RowStyles.Add(new RowStyle(SizeType.Percent, 26f));
            _tblReal.RowStyles.Add(new RowStyle(SizeType.Percent, 26f));
            _tblReal.RowStyles.Add(new RowStyle(SizeType.Percent, 26f));

            var hdrFont = new Font("MS Sans Serif", 8.25f, FontStyle.Bold);
            _lblRtHdrParam = CreateCellLabel("", hdrFont, Color.DimGray, ContentAlignment.MiddleLeft);
            _lblRtHdrCur = CreateCellLabel("", hdrFont, Color.DarkGreen, ContentAlignment.MiddleCenter);
            _lblRtHdrMin = CreateCellLabel("", hdrFont, Color.DarkSlateBlue, ContentAlignment.MiddleCenter);
            _lblRtHdrMax = CreateCellLabel("", hdrFont, Color.DarkRed, ContentAlignment.MiddleCenter);

            _tblReal.Controls.Add(_lblRtHdrParam, 0, 0);
            _tblReal.Controls.Add(_lblRtHdrCur, 1, 0);
            _tblReal.Controls.Add(_lblRtHdrMin, 2, 0);
            _tblReal.Controls.Add(_lblRtHdrMax, 3, 0);

            var rtCurFont = new Font("Consolas", 11f, FontStyle.Bold);
            var rtExtFont = new Font("Consolas", 10f, FontStyle.Bold);
            var rowTitleFont = new Font("MS Sans Serif", 8.5f, FontStyle.Regular);

            _lblRealLineTitle = CreateCellLabel("", rowTitleFont, Color.Black, ContentAlignment.MiddleLeft);
            _lblRealLineVolt = CreateCellLabel("---", rtCurFont, Color.DarkGreen, ContentAlignment.MiddleCenter);
            _lblMinLineVolt = CreateCellLabel("---", rtExtFont, Color.DarkSlateBlue, ContentAlignment.MiddleCenter);
            _lblMaxLineVolt = CreateCellLabel("---", rtExtFont, Color.DarkRed, ContentAlignment.MiddleCenter);

            _lblRealGndTitle = CreateCellLabel("", rowTitleFont, Color.Black, ContentAlignment.MiddleLeft);
            _lblRealGndVolt = CreateCellLabel("---", rtCurFont, Color.DarkGreen, ContentAlignment.MiddleCenter);
            _lblMinGndVolt = CreateCellLabel("---", rtExtFont, Color.DarkSlateBlue, ContentAlignment.MiddleCenter);
            _lblMaxGndVolt = CreateCellLabel("---", rtExtFont, Color.DarkRed, ContentAlignment.MiddleCenter);

            _lblRealFreqTitle = CreateCellLabel("", rowTitleFont, Color.Black, ContentAlignment.MiddleLeft);
            _lblRealFreq = CreateCellLabel("---", rtCurFont, Color.DarkGreen, ContentAlignment.MiddleCenter);
            _lblMinFreqVal = CreateCellLabel("---", rtExtFont, Color.DarkSlateBlue, ContentAlignment.MiddleCenter);
            _lblMaxFreqVal = CreateCellLabel("---", rtExtFont, Color.DarkRed, ContentAlignment.MiddleCenter);

            _tblReal.Controls.Add(_lblRealLineTitle, 0, 1);
            _tblReal.Controls.Add(_lblRealLineVolt, 1, 1);
            _tblReal.Controls.Add(_lblMinLineVolt, 2, 1);
            _tblReal.Controls.Add(_lblMaxLineVolt, 3, 1);

            _tblReal.Controls.Add(_lblRealGndTitle, 0, 2);
            _tblReal.Controls.Add(_lblRealGndVolt, 1, 2);
            _tblReal.Controls.Add(_lblMinGndVolt, 2, 2);
            _tblReal.Controls.Add(_lblMaxGndVolt, 3, 2);

            _tblReal.Controls.Add(_lblRealFreqTitle, 0, 3);
            _tblReal.Controls.Add(_lblRealFreq, 1, 3);
            _tblReal.Controls.Add(_lblMinFreqVal, 2, 3);
            _tblReal.Controls.Add(_lblMaxFreqVal, 3, 3);

            _btnResetMinMax = new Button
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                FlatStyle = FlatStyle.Standard,
                BackColor = Color.FromArgb(240, 244, 248),
                ForeColor = Color.Black,
                Font = new Font("MS Sans Serif", 8.25f, FontStyle.Regular)
            };
            _btnResetMinMax.Click += (s, e) => ResetSessionMinMax();

            _grpRealTime.Controls.Add(_tblReal);
            _grpRealTime.Controls.Add(_btnResetMinMax);

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

            // Пересчитываем выравнивание нижних блоков при любом изменении размеров вкладки
            _splitStatus.SizeChanged += (s, e) => AlignStatusColumnBottoms();
            _splitStatus.SplitterMoved += (s, e) => AlignStatusColumnBottoms();
        }

        private static Label CreateCellLabel(string text, Font font, Color foreColor, ContentAlignment align)
        {
            return new Label
            {
                Text = text,
                Font = font,
                ForeColor = foreColor,
                TextAlign = align,
                Dock = DockStyle.Fill,
                AutoSize = false,
                Margin = new Padding(3, 1, 3, 1)
            };
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

        /// <summary>
        /// Строит вкладку "События (Events)" с панелью фильтрации по типу и интервалу дат/времени (с ... по ...).
        /// </summary>
        private void BuildEventsTab(TabPage page)
        {
            // 1. Панель фильтрации событий (автоматически переносит элементы при увеличении шрифта)
            _pnlEventsFilter = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 42),
                Padding = new Padding(8, 6, 8, 6),
                BackColor = Color.FromArgb(240, 244, 248),
                WrapContents = true
            };

            _lblFilterTitle = new Label
            {
                AutoSize = true,
                Margin = new Padding(2, 6, 4, 2),
                Font = new Font("MS Sans Serif", 8.5f, FontStyle.Bold),
                ForeColor = Color.Black
            };

            _cmbEventFilter = new ComboBox
            {
                Width = 260,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 2, 12, 2),
                Font = new Font("MS Sans Serif", 9f, FontStyle.Regular)
            };
            for (int i = 0; i < 11; i++) _cmbEventFilter.Items.Add(i.ToString());
            _cmbEventFilter.SelectedIndex = 0;
            _cmbEventFilter.SelectedIndexChanged += (s, e) =>
            {
                if (_suppressFilterEvent) return;
                PopulateEventsGrid(_currentSite);
            };

            // Фильтр по интервалу дат и времени: "[x] Период с: [dd.MM.yyyy HH:mm:ss] по: [dd.MM.yyyy HH:mm:ss]"
            _chkEnableDateFilter = new CheckBox
            {
                AutoSize = true,
                Checked = false,
                Margin = new Padding(4, 5, 4, 2),
                Font = new Font("MS Sans Serif", 8.5f, FontStyle.Bold),
                ForeColor = Color.DarkSlateBlue
            };
            _chkEnableDateFilter.CheckedChanged += (s, e) =>
            {
                _dtpFilterFrom.Enabled = _chkEnableDateFilter.Checked;
                _dtpFilterTo.Enabled = _chkEnableDateFilter.Checked;
                if (_suppressFilterEvent) return;
                PopulateEventsGrid(_currentSite);
            };

            _dtpFilterFrom = new DateTimePicker
            {
                Width = 155,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd.MM.yyyy HH:mm:ss",
                Enabled = false,
                Margin = new Padding(0, 2, 4, 2),
                Font = new Font("Consolas", 9f, FontStyle.Regular)
            };
            _dtpFilterFrom.ValueChanged += (s, e) =>
            {
                if (_suppressFilterEvent) return;
                PopulateEventsGrid(_currentSite);
            };

            _lblDateTo = new Label
            {
                AutoSize = true,
                Margin = new Padding(2, 6, 4, 2),
                Font = new Font("MS Sans Serif", 8.5f, FontStyle.Bold),
                ForeColor = Color.DarkSlateBlue
            };

            _dtpFilterTo = new DateTimePicker
            {
                Width = 155,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd.MM.yyyy HH:mm:ss",
                Enabled = false,
                Margin = new Padding(0, 2, 12, 2),
                Font = new Font("Consolas", 9f, FontStyle.Regular)
            };
            _dtpFilterTo.ValueChanged += (s, e) =>
            {
                if (_suppressFilterEvent) return;
                PopulateEventsGrid(_currentSite);
            };

            _btnResetFilter = new Button
            {
                Width = 120,
                Height = 25,
                Margin = new Padding(0, 1, 12, 2),
                BackColor = Color.WhiteSmoke,
                Font = new Font("MS Sans Serif", 8.25f, FontStyle.Regular)
            };
            _btnResetFilter.Click += (s, e) =>
            {
                _suppressFilterEvent = true;
                try
                {
                    _cmbEventFilter.SelectedIndex = 0;
                    _chkEnableDateFilter.Checked = false;
                    _dtpFilterFrom.Enabled = false;
                    _dtpFilterTo.Enabled = false;
                    SyncFilterDatePickersFromSite(_currentSite);
                }
                finally
                {
                    _suppressFilterEvent = false;
                }
                PopulateEventsGrid(_currentSite);
            };

            _chkFilterChartsAndCsv = new CheckBox
            {
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 5, 14, 2),
                Font = new Font("MS Sans Serif", 8.5f, FontStyle.Regular),
                ForeColor = Color.Black
            };
            _chkFilterChartsAndCsv.CheckedChanged += (s, e) => _pnlCharts?.Invalidate();

            _lblFilterCount = new Label
            {
                AutoSize = true,
                Margin = new Padding(4, 6, 0, 2),
                Font = new Font("MS Sans Serif", 9f, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };

            _pnlEventsFilter.Controls.AddRange(new Control[]
            {
                _lblFilterTitle,
                _cmbEventFilter,
                _chkEnableDateFilter,
                _dtpFilterFrom,
                _lblDateTo,
                _dtpFilterTo,
                _btnResetFilter,
                _chkFilterChartsAndCsv,
                _lblFilterCount
            });

            // 2. Таблица событий (DataGridView)
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
            page.Controls.Add(_pnlEventsFilter);
        }

        // =========================================================================
        // ЛОГИКА ФИЛЬТРАЦИИ СОБЫТИЙ ПО ТИПУ И ИНТЕРВАЛУ ДАТ / ВРЕМЕНИ
        // =========================================================================

        /// <summary>
        /// Синхронизирует начальные значения полей DateTimePicker ("с" и "по") с реальным временем начала
        /// и окончания загруженной сессии прибора.
        /// </summary>
        private void SyncFilterDatePickersFromSite(SiteData s)
        {
            if (s == null || !s.HasValidData || _dtpFilterFrom == null || _dtpFilterTo == null)
                return;

            bool prevSuppress = _suppressFilterEvent;
            _suppressFilterEvent = true;
            try
            {
                DateTime minDt = s.StartTime;
                DateTime maxDt = s.EndTime;

                if (s.Events.Count > 0)
                {
                    DateTime evMin = s.Events.Min(e => e.Timestamp);
                    DateTime evMax = s.Events.Max(e => e.Timestamp);
                    if (evMin < minDt) minDt = evMin;
                    if (evMax > maxDt) maxDt = evMax;
                }

                if (s.OpenEvents.Count > 0)
                {
                    DateTime opMin = s.OpenEvents.Min(e => e.Timestamp);
                    DateTime opMax = s.OpenEvents.Max(e => e.Timestamp);
                    if (opMin < minDt) minDt = opMin;
                    if (opMax > maxDt) maxDt = opMax;
                }

                if (minDt < _dtpFilterFrom.MinDate) minDt = _dtpFilterFrom.MinDate;
                if (maxDt > _dtpFilterTo.MaxDate) maxDt = _dtpFilterTo.MaxDate;
                if (maxDt < minDt) maxDt = minDt;

                _dtpFilterFrom.Value = minDt;
                _dtpFilterTo.Value = maxDt;
            }
            catch { }
            finally
            {
                _suppressFilterEvent = prevSuppress;
            }
        }

        /// <summary>
        /// Возвращает текущую выбранную категорию фильтра по типу события.
        /// </summary>
        private EventCategory GetSelectedFilterCategory()
        {
            if (_cmbEventFilter == null || _cmbEventFilter.SelectedIndex < 0)
                return EventCategory.All;
            return (EventCategory)_cmbEventFilter.SelectedIndex;
        }

        /// <summary>
        /// Проверяет, проходит ли событие через фильтр по типу помехи И фильтр по интервалу дат/времени.
        /// </summary>
        private bool MatchesEventFilter(EventRecord ev)
        {
            if (ev == null) return false;

            // 1. Проверка фильтра по типу события
            EventCategory cat = GetSelectedFilterCategory();
            if (cat == EventCategory.OpenOnly)
            {
                if (!ev.IsOpenEvent) return false;
            }
            else if (cat != EventCategory.All)
            {
                if (ev.Category != cat) return false;
            }

            // 2. Проверка фильтра по интервалу дат и времени (если включен чекбокс "Период с ... по ...")
            if (_chkEnableDateFilter != null && _chkEnableDateFilter.Checked)
            {
                DateTime fromTime = _dtpFilterFrom.Value;
                DateTime toTime = _dtpFilterTo.Value;
                if (fromTime > toTime)
                {
                    DateTime tmp = fromTime;
                    fromTime = toTime;
                    toTime = tmp;
                }

                if (ev.Timestamp < fromTime || ev.Timestamp > toTime)
                    return false;
            }

            return true;
        }

        /// <summary>
        ///Проверяет, активен ли хотя бы один из фильтров (по типу или по времени).
        /// </summary>
        private bool IsAnyFilterActive()
        {
            return GetSelectedFilterCategory() != EventCategory.All ||
                   (_chkEnableDateFilter != null && _chkEnableDateFilter.Checked);
        }

        /// <summary>
        /// Возвращает список завершенных событий для построения графиков и экспорта с учетом активных фильтров.
        /// </summary>
        private List<EventRecord> GetEventsForChartsAndExport()
        {
            if (_currentSite == null || !_currentSite.HasValidData)
                return new List<EventRecord>();

            if (_chkFilterChartsAndCsv != null && _chkFilterChartsAndCsv.Checked && IsAnyFilterActive())
            {
                return _currentSite.Events.Where(MatchesEventFilter).ToList();
            }

            return _currentSite.Events;
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

                if (_dtpFilterFrom != null && _dtpFilterTo != null)
                {
                    int dtpWidth = (int)Math.Round(155 * Math.Max(1.0f, _fontScale * 0.95f));
                    _dtpFilterFrom.Width = dtpWidth;
                    _dtpFilterTo.Width = dtpWidth;
                }

                int labelColWidth = (int)Math.Round(450 * Math.Max(1.0f, _fontScale * 0.96f));
                if (_tblHn != null) _tblHn.ColumnStyles[0].Width = labelColWidth;
                if (_tblNg != null) _tblNg.ColumnStyles[0].Width = labelColWidth;
                if (_tblFreq != null) _tblFreq.ColumnStyles[0].Width = labelColWidth;

                if (_grpHn != null) _grpHn.Height = (int)Math.Round(132 * Math.Max(1.0f, _fontScale * 0.95f));
                if (_grpNg != null) _grpNg.Height = (int)Math.Round(100 * Math.Max(1.0f, _fontScale * 0.95f));
                if (_grpFreq != null) _grpFreq.Height = (int)Math.Round(132 * Math.Max(1.0f, _fontScale * 0.95f));
                if (_grpOpt != null) _grpOpt.Height = (int)Math.Round(112 * Math.Max(1.0f, _fontScale * 0.98f));
                if (_grpDesc != null) _grpDesc.Height = (int)Math.Round(72 * Math.Max(1.0f, _fontScale * 0.98f));
                if (_grpRealTime != null) _grpRealTime.Height = (int)Math.Round(200 * Math.Max(1.0f, _fontScale * 0.96f));
                if (_grpRecInfo != null) _grpRecInfo.Height = (int)Math.Round(205 * Math.Max(1.0f, _fontScale));
                if (_grpButtons != null) _grpButtons.Height = (int)Math.Round(175 * Math.Max(1.0f, _fontScale * 0.95f));

                AdjustStatusSplitterForScale();
                ResizePresetComboToContent();
                AlignStatusColumnBottoms();

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
            int targetLeftWidth = (int)Math.Round(710 * Math.Max(1.0f, _fontScale * 0.95f));
            targetLeftWidth = Math.Min(_splitStatus.Width - 460, Math.Max(660, targetLeftWidth));
            _splitStatus.SplitterDistance = targetLeftWidth;
        }

        /// <summary>
        /// Выравнивает нижние края колонок вкладки «Статус»: последний блок левой колонки
        /// («Описание точки контроля») и последний блок правой («Команды управления регистратором»)
        /// растягиваются так, чтобы обе колонки заканчивались на одной высоте.
        /// </summary>
        private void AlignStatusColumnBottoms()
        {
            if (_splitStatus == null || _pnlLeft == null || _pnlRight == null) return;
            if (_grpHn == null || _grpDesc == null) return;

            const int minLastLeft = 120;
            const int minLastRight = 120;

            // Левая колонка: H-N, N-G, частоты, опции — фиксированные, «Описание» тянется вниз
            int leftAvail = _pnlLeft.ClientSize.Height - _pnlLeft.Padding.Vertical;
            int leftFixed = _grpHn.Height
                          + (_grpNg != null ? _grpNg.Height : 0)
                          + (_grpFreq != null ? _grpFreq.Height : 0)
                          + (_grpOpt != null ? _grpOpt.Height : 0);

            // Правая колонка: RealTime и Recorder Info фиксированные, «Команды» тянется вниз
            int rightAvail = _pnlRight.ClientSize.Height - _pnlRight.Padding.Vertical;
            int rightFixed = (_grpRealTime != null ? _grpRealTime.Height : 0)
                           + (_grpRecInfo != null ? _grpRecInfo.Height : 0);

            // Чтобы нижние края совпали, нижний блок каждой колонки занимает остаток её высоты
            int leftLast = leftAvail - leftFixed;
            int rightLast = rightAvail - rightFixed;

            if (leftLast <= 0 || rightLast <= 0) return; // окно слишком маленькое — не трогаем

            if (leftLast < minLastLeft) leftLast = minLastLeft;
            if (rightLast < minLastRight) rightLast = minLastRight;

            if (_grpDesc.Height != leftLast) _grpDesc.Height = leftLast;
            if (_grpButtons != null && _grpButtons.Height != rightLast) _grpButtons.Height = rightLast;
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
            ApplyParityLocalization();
        }

        private void ApplyLocalization()
        {
            _mnuLangRu.Checked = (_lang == AppLang.Ru);
            _mnuLangEn.Checked = (_lang == AppLang.En);

            this.Text = (_currentSite.HasValidData && _currentSite.SerialNumber > 0)
                ? $"Fluke EventView VR101 - [SN F{_currentSite.SerialNumber}]"
                : T("Fluke EventView VR101 - [Нет активного соединения с прибором]", "Fluke EventView VR101 - [No Active Device Session]");

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
            _mnuHelpAbout.Text = T("&О программе EventView VR101...", "&About EventView VR101...");

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

            // Подсказки для индикаторов связи
            _lblDeviceOnlineLed.ToolTipText = T(
                "Индикатор связи с прибором Fluke VR101 (Зеленый — прибор отвечает, Желтый — ожидание ответа, Серый — порт закрыт)",
                "Fluke VR101 link status (Green = device responding, Yellow = scanning/waiting, Gray = port closed)");
            _lblTxLed.ToolTipText = T("Передача данных в прибор (TX)", "Transmitting data to recorder (TX)");
            _lblRxLed.ToolTipText = T("Прием данных от прибора (RX)", "Receiving data from recorder (RX)");

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

            // Правая панель Status: RealTime с учетом Мин/Макс за сессию
            _grpRealTime.Text = T("Текущие показания в реальном времени и экстремумы за сессию (RealTime)", "RealTime Readings & Session Min / Max");
            _lblRtHdrParam.Text = T("Параметр сети", "Parameter");
            _lblRtHdrCur.Text = T("Текущее", "Current");
            _lblRtHdrMin.Text = T("Мин. (Min)", "Min");
            _lblRtHdrMax.Text = T("Макс. (Max)", "Max");

            _lblRealLineTitle.Text = T("Фаза-Нейтраль (Vrms):", "Line H-N (Vrms):");
            _lblRealGndTitle.Text = T("Нейтраль-Земля (Vrms):", "Ground N-G (Vrms):");
            _lblRealFreqTitle.Text = T("Частота сети (Hz):", "Frequency (Hz):");
            _btnResetMinMax.Text = T("↺ Сбросить Мин / Макс за сессию", "↺ Reset Session Min / Max");
            _toolTip.SetToolTip(_btnResetMinMax, T(
                "Обнулить накопленные минимальные и максимальные значения за текущий сеанс связи с прибором.",
                "Reset recorded minimum and maximum values for the current connection session."));

            _grpRecInfo.Text = T("Информация о регистраторе (Recorder Info)", "Recorder Info");
            _grpButtons.Text = T("Команды управления регистратором", "Recorder Setup Commands");

            _btnSendSetupClear.Text = T("Записать уставки / Очистить память прибора", "Send Setup / Clear Recorder");
            _btnReloadInputsRight.Text = T("Прочитать текущие уставки из прибора", "Read Current Setup from Recorder");
            _btnDefaultSetup.Text = T("Заводские уставки по умолчанию (220V / 50Hz)", "Default Setup (220V / 50Hz)");

            // Панель фильтрации по типу и интервалу дат/времени (Events Tab)
            _lblFilterTitle.Text = T("Тип:", "Type:");
            _chkEnableDateFilter.Text = T("Период с:", "Period from:");
            _lblDateTo.Text = T("по:", "to:");
            _btnResetFilter.Text = T("Показать все", "Show All");
            _chkFilterChartsAndCsv.Text = T("Учитывать на графиках и в CSV", "Apply to Graphs & CSV");

            _suppressFilterEvent = true;
            try
            {
                int selFilter = _cmbEventFilter.SelectedIndex >= 0 ? _cmbEventFilter.SelectedIndex : 0;
                _cmbEventFilter.Items[0] = T("Все типы событий (All Events)", "All Events");
                _cmbEventFilter.Items[1] = T("Перенапряжение H-N (H-N Swell)", "H-N Swell");
                _cmbEventFilter.Items[2] = T("Провал напряжения H-N (H-N Sag)", "H-N Sag");
                _cmbEventFilter.Items[3] = T("Импульсы Фаза-Нейтраль (H-N Transients)", "H-N Transients");
                _cmbEventFilter.Items[4] = T("Превышение Нейтраль-Земля (N-G Swell)", "N-G Swell");
                _cmbEventFilter.Items[5] = T("Импульсы Нейтраль-Земля (N-G Transients)", "N-G Transients");
                _cmbEventFilter.Items[6] = T("Высокая частота (High Frequency)", "High Frequency");
                _cmbEventFilter.Items[7] = T("Низкая частота (Low Frequency)", "Low Frequency");
                _cmbEventFilter.Items[8] = T("Обрыв питания (Outage)", "Outage");
                _cmbEventFilter.Items[9] = T("Только активные события (Open Events)", "Open Events Only");
                _cmbEventFilter.Items[10] = T("Потеря синхронизации (Loss of Synchronization)", "Loss of Synchronization");
                _cmbEventFilter.SelectedIndex = selFilter;
            }
            finally
            {
                _suppressFilterEvent = false;
            }

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
            UpdateDeviceConnectionLedState(Environment.TickCount);
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
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
            _txtCommLog.AppendText(line + Environment.NewLine);

            // Дублируем журнал в файл рядом с программой (как EV101MSG.LOG оригинала)
            try
            {
                string path = Path.Combine(
                    Path.GetDirectoryName(Application.ExecutablePath) ?? ".",
                    "FlukeEventView.log");
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch { /* файл недоступен — журнал остаётся только в окне */ }
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
            UpdateDeviceConnectionLedState(Environment.TickCount);
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
                _lastValidDeviceTick = -10000;

                _hasSessionMinMax = false;
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
            _lastValidDeviceTick = -10000;
            _hasSessionMinMax = false;

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

            UpdateRealtimeAndInfoPanels(_currentSite);
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
                _btnResetMinMax.Enabled = true;
                _lblPortStatus.Text = T($"Порт: {_serialPort?.PortName}", $"Port: {_serialPort?.PortName}");
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
                _btnResetMinMax.Enabled = false;
                _lblPortStatus.Text = T("Порт: ЗАКРЫТ", "Port: CLOSED");
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

                PulseTxIndicator();
                _serialPort.Write(cmdA, 0, cmdA.Length);

                byte[] raw = ReadBytesWithTimeout(_serialPort, 94, 650, cmdA.Length);
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

                _lastValidDeviceTick = Environment.TickCount;
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

                    if (!_initialSetupLoadedFromDevice || _currentSite.SerialNumber != liveSite.SerialNumber)
                    {
                        _hasSessionMinMax = false;
                        _currentSite = liveSite;
                        PopulateSetupInputsFromSite(_currentSite);
                        SyncFilterDatePickersFromSite(_currentSite);
                        _initialSetupLoadedFromDevice = true;
                        this.Text = $"Fluke EventView VR101 - [SN F{_currentSite.SerialNumber}]";
                        LogMessage($"Detected Recorder SN#F{_currentSite.SerialNumber} (FW v{_currentSite.FirmwareVersion}). Loaded device parameters and setup.");
                        _prevElapsedTicks = liveSite.ElapsedTicks;
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

                    CheckSynchronizationLoss(liveSite);
                    LogRawHeaderDetails(hdr);

                    UpdateSessionMinMax(liveSite.LiveHnVolt, liveSite.LiveNgVolt, liveSite.LiveFreq);

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
                    this.Text = $"Fluke EventView VR101 - [SN F{_currentSite.SerialNumber}]";
                    UpdateSessionMinMax(_currentSite.LiveHnVolt, _currentSite.LiveNgVolt, _currentSite.LiveFreq);
                    PopulateSetupInputsFromSite(_currentSite);
                    SyncFilterDatePickersFromSite(_currentSite);
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
                this.Text = $"Fluke EventView VR101 - [SN F{site.SerialNumber}]";
                UpdateSessionMinMax(site.LiveHnVolt, site.LiveNgVolt, site.LiveFreq);

                if (site.StoredEventCount == 0)
                {
                    _currentSite = site;
                    _selectedTransientEvent = null;
                    _eventsDownloadedForCurrentSession = true;
                    PopulateSetupInputsFromSite(_currentSite);
                    SyncFilterDatePickersFromSite(_currentSite);
                    UpdateRealtimeAndInfoPanels(_currentSite);
                    PopulateEventsGrid(_currentSite);
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
                    _hasUnsavedSessionData = true;

                    Invoke(new Action(() =>
                    {
                        PopulateSetupInputsFromSite(_currentSite);
                        SyncFilterDatePickersFromSite(_currentSite);
                        UpdateRealtimeAndInfoPanels(_currentSite);
                        PopulateEventsGrid(_currentSite);
                        UpdateDirtyMarker();
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
                PulseTxIndicator();
                _serialPort.Write(req, 0, req.Length);

                byte[] rx = ReadBytesWithTimeout(_serialPort, 263, 2200, req.Length);
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
                if (copyLen > 0)
                {
                    Array.Copy(rx, startIdx, data256, 0, copyLen);
                    _lastValidDeviceTick = Environment.TickCount;
                }
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
                            // Запись уставок перезапускает регистратор: его счётчик наработки
                            // обнуляется. Это наше собственное действие, а не потеря синхронизации,
                            // поэтому на 20 секунд отключаем детектор.
                            _suppressSyncLossUntil = DateTime.Now.AddSeconds(20);
                            LogMessage($"Sending Setup (Try {tryNum}): {BitConverter.ToString(rawBytes)}");
                            PulseTxIndicator();
                            _serialPort.Write(rawBytes, 0, rawBytes.Length);
                            Thread.Sleep(550);

                            byte[] rx = ReadBytesWithTimeout(_serialPort, 64, 450, rawBytes.Length);
                            LogMessage($"Setup Response: {BitConverter.ToString(rx)}");
                            if (rx.Length > rawBytes.Length)
                            {
                                _lastValidDeviceTick = Environment.TickCount;
                                success = true;
                                break;
                            }
                        }
                    }
                });

                _hasSessionMinMax = false;
                byte[] verifyHdr = await Task.Run(() => RequestDeviceHeader88());
                if (verifyHdr != null && verifyHdr.Length == 88)
                {
                    _currentSite = SiteData.ParseFromDeviceHeader(verifyHdr, DateTime.Now);
                    _initialSetupLoadedFromDevice = true;
                    this.Text = $"Fluke EventView VR101 - [SN F{_currentSite.SerialNumber}]";
                    UpdateSessionMinMax(_currentSite.LiveHnVolt, _currentSite.LiveNgVolt, _currentSite.LiveFreq);
                    PopulateSetupInputsFromSite(_currentSite);
                    SyncFilterDatePickersFromSite(_currentSite);
                    UpdateRealtimeAndInfoPanels(_currentSite);
                }

                _currentSite.Events.Clear();
                _currentSite.OpenEvents.Clear();
                _selectedTransientEvent = null;
                _eventsDownloadedForCurrentSession = false;
                PopulateEventsGrid(_currentSite);

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

        private byte[] ReadBytesWithTimeout(SerialPort port, int targetCount, int timeoutMs, int echoLength = 5)
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

                    if (result.Count > echoLength)
                    {
                        PulseRxIndicator();
                    }
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
            return result.ToArray();
        }

        // =========================================================================
        // РАЗДЕЛЬНОЕ ОБНОВЛЕНИЕ: 1) УСТАВКИ, 2) REALTIME & INFO, 3) ТАБЛИЦА EVENTS С ФИЛЬТРОМ
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

        private void UpdateRealtimeAndInfoPanels(SiteData s)
        {
            if (_isScanning && s != null && s.HasValidData)
            {
                _lblRealLineVolt.Text = $"{s.LiveHnVolt:0} V";
                _lblRealGndVolt.Text = $"{s.LiveNgVolt:0.0} V";
                _lblRealFreq.Text = $"{s.LiveFreq:00.0} Hz";

                if (_hasSessionMinMax)
                {
                    _lblMinLineVolt.Text = $"{_sessionMinHnVolt:0} V";
                    _lblMaxLineVolt.Text = $"{_sessionMaxHnVolt:0} V";

                    _lblMinGndVolt.Text = $"{_sessionMinNgVolt:0.0} V";
                    _lblMaxGndVolt.Text = $"{_sessionMaxNgVolt:0.0} V";

                    _lblMinFreqVal.Text = $"{_sessionMinFreq:00.0} Hz";
                    _lblMaxFreqVal.Text = $"{_sessionMaxFreq:00.0} Hz";
                }
            }
            else
            {
                _lblRealLineVolt.Text = "---";
                _lblRealGndVolt.Text = "---";
                _lblRealFreq.Text = "---";

                _lblMinLineVolt.Text = "---";
                _lblMaxLineVolt.Text = "---";
                _lblMinGndVolt.Text = "---";
                _lblMaxGndVolt.Text = "---";
                _lblMinFreqVal.Text = "---";
                _lblMaxFreqVal.Text = "---";
            }

            _lblRealLineVolt.ForeColor = Color.DarkGreen;
            _lblRealGndVolt.ForeColor = Color.DarkGreen;
            _lblRealFreq.ForeColor = Color.DarkGreen;

            _lblMinLineVolt.ForeColor = Color.DarkSlateBlue;
            _lblMinGndVolt.ForeColor = Color.DarkSlateBlue;
            _lblMinFreqVal.ForeColor = Color.DarkSlateBlue;

            _lblMaxLineVolt.ForeColor = Color.DarkRed;
            _lblMaxGndVolt.ForeColor = Color.DarkRed;
            _lblMaxFreqVal.ForeColor = Color.DarkRed;

            if (s == null || !s.HasValidData)
            {
                _lblInfoSerial.Text = T("Серийный номер прибора: ---", "Logger Serial Number: ---");
                _lblInfoEventsCount.Text = T("Нет данных (прибор не подключен)", "No Device Data (Not Connected)");
                _lblInfoFromDate.Text = T("      с     ---", "      from  ---");
                _lblInfoToDate.Text = T("      по   ---", "      to      ---");
                _lblInfoFirmware.Text = T("Версия прошивки VR101: ---", "VR101 Firmware Version: ---");
                _lblInfoMode.Text = T("Режим прибора: ---", "Device Mode: ---");
                return;
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

            // Подсказка к строке «Нейтраль-Земля»: сырое значение и байт конфигурации прибора
            if (_toolTip != null && _lblRealGndVolt != null)
            {
                byte cfg = 0, ngRaw = 0;
                if (s != null && s.RawHeader88 != null && s.RawHeader88.Length > 51)
                {
                    cfg = s.RawHeader88[51];
                    ngRaw = s.RawHeader88[4];
                }
                bool ngOn = (cfg & 0x10) != 0;
                _toolTip.SetToolTip(_lblRealGndVolt, T(
                    $"Сырое значение из прибора: h[4] = {ngRaw} (0x{ngRaw:X2}). Байт конфигурации h[51] = 0x{cfg:X2}, бит 0x10 = {(ngOn ? 1 : 0)}. " +
                    (ngOn ? "Канал N-G включён."
                          : "Канал N-G выключен (режим TN): прибор его не измеряет, поэтому показывается 0 В."),
                    $"Raw value from recorder: h[4] = {ngRaw} (0x{ngRaw:X2}). Config byte h[51] = 0x{cfg:X2}, bit 0x10 = {(ngOn ? 1 : 0)}. " +
                    (ngOn ? "N-G channel is enabled."
                          : "N-G channel is disabled (TN mode): the recorder does not measure it, so 0 V is shown.")));
            }
        }

        // =========================================================================
        // ПАРИТЕТ С ОРИГИНАЛЬНЫМ EVENTVIEW: сводные счётчики, Graph Setup, «не сохранено»
        // =========================================================================
        private Panel _pnlSummary;
        private Label _lblSummary;
        private Label _lblUnsavedMark;
        private Panel _pnlGraphSetup;
        private CheckBox _chkShowHnEvents;
        private CheckBox _chkShowNgEvents;
        private Label _lblPeriods;
        private NumericUpDown _numPeriods;
        private bool _hasUnsavedSessionData = false;
        private ToolStripMenuItem _mnuFilePrint;
        private ToolStripMenuItem _mnuFilePrintPreview;
        private ToolStripMenuItem _mnuCalibration;
        // --- Профили порогов по нормативным документам (ГОСТ, EN, IEC, ANSI) ---
        private Label _lblPreset;
        private ComboBox _cmbPreset;
        private Button _btnApplyPreset;
        private Panel _pnlPreset;
        private Label _lblPresetNote;

        private sealed class ThresholdPreset
        {
            public string NameRu, NameEn;
            public int Sag, Swell;              // В, шаг прибора 2 В в режиме 220 В
            public decimal MinF, MaxF;          // Гц, шаг 0,1 Гц
            public string NoteRu, NoteEn;
            public bool Arbitration;            // true — значения из нормативного документа (арбитражный профиль)
        }

        /// <summary>
        /// Профили порогов, приведённые к шагам прибора (2 В для H-N, 0,1 Гц для частоты).
        /// Первые профили — «арбитражные»: значения взяты из нормативных документов.
        /// Остальные — «сценарии»: практические наборы под типовые объекты и задачи.
        /// Пороги импульсов и N-G профилями не меняются: единого норматива для них нет.
        /// </summary>
        private static readonly ThresholdPreset[] _presets = new ThresholdPreset[]
        {
            // ================= АРБИТРАЖНЫЕ (по нормативным документам) =================
            new ThresholdPreset
            {
                Arbitration = true,
                NameRu = "Арбитраж — ГОСТ 32144-2013: ±10 % / ±0,2 Гц",
                NameEn = "Arbitration — GOST 32144-2013: ±10 % / ±0.2 Hz",
                Sag = 198, Swell = 242, MinF = 49.8m, MaxF = 50.2m,
                NoteRu = "Напряжение: отклонение ±10 % от 220 В (198…242 В). Частота: ±0,2 Гц в течение 95 % времени недели.",
                NoteEn = "Voltage: ±10 % of 220 V (198…242 V). Frequency: ±0.2 Hz for 95 % of a week."
            },
            new ThresholdPreset
            {
                Arbitration = true,
                NameRu = "Арбитраж — ГОСТ 32144-2013: ±10 % / ±0,4 Гц",
                NameEn = "Arbitration — GOST 32144-2013: ±10 % / ±0.4 Hz",
                Sag = 198, Swell = 242, MinF = 49.6m, MaxF = 50.4m,
                NoteRu = "Напряжение: ±10 % (198…242 В). Частота: ±0,4 Гц — предел для 100 % времени.",
                NoteEn = "Voltage: ±10 % (198…242 V). Frequency: ±0.4 Hz — limit for 100 % of the time."
            },
            new ThresholdPreset
            {
                Arbitration = true,
                NameRu = "Арбитраж — EN 50160: ±10 % / ±1 %",
                NameEn = "Arbitration — EN 50160: ±10 % / ±1 %",
                Sag = 198, Swell = 242, MinF = 49.5m, MaxF = 50.5m,
                NoteRu = "Напряжение: ±10 % для 95 % времени недели (10-минутные RMS). Частота: ±1 % (49,5…50,5 Гц) для 99,5 % года.",
                NoteEn = "Voltage: ±10 % for 95 % of a week (10-min RMS). Frequency: ±1 % (49.5…50.5 Hz) for 99.5 % of a year."
            },
            new ThresholdPreset
            {
                Arbitration = true,
                NameRu = "Арбитраж — ANSI C84.1 Range A: ±5 %",
                NameEn = "Arbitration — ANSI C84.1 Range A: ±5 %",
                Sag = 210, Swell = 230, MinF = 49.5m, MaxF = 50.5m,
                NoteRu = "Напряжение: Range A — ±5 % от 220 В (209…231 В, приведено к шагу 2 В: 210/230). Частота в ANSI C84.1 не нормируется — взята по EN 50160.",
                NoteEn = "Voltage: Range A — ±5 % of 220 V (209…231 V, snapped to the 2 V step: 210/230). Frequency is not specified by ANSI C84.1 — taken from EN 50160."
            },
            new ThresholdPreset
            {
                Arbitration = true,
                NameRu = "Арбитраж — IEC 61000-2-4, класс 2: ±10 %",
                NameEn = "Arbitration — IEC 61000-2-4, class 2: ±10 %",
                Sag = 198, Swell = 242, MinF = 49.5m, MaxF = 50.5m,
                NoteRu = "Напряжение: ±10 % от номинала (198…242 В). Частота: ±1 % по EN 50160.",
                NoteEn = "Voltage: ±10 % of nominal (198…242 V). Frequency: ±1 % per EN 50160."
            },

            // ================= СЦЕНАРИИ (практические наборы под объект) =================
            new ThresholdPreset
            {
                NameRu = "Сценарий — чувствительная нагрузка (±7,5 %)",
                NameEn = "Scenario — sensitive load (±7.5 %)",
                Sag = 204, Swell = 236, MinF = 49.8m, MaxF = 50.2m,
                NoteRu = "Для серверных, лабораторного и медицинского оборудования: ±7,5 % по напряжению (204…236 В) и частота ±0,2 Гц. Строже ГОСТ/EN, мягче диагностического режима.",
                NoteEn = "For server rooms, laboratory and medical equipment: ±7.5 % voltage (204…236 V) and ±0.2 Hz frequency. Stricter than GOST/EN, looser than the diagnostic profile."
            },
            new ThresholdPreset
            {
                NameRu = "Сценарий — серверная / ЦОД: глубокие провалы",
                NameEn = "Scenario — data centre: deep sags",
                Sag = 187, Swell = 242, MinF = 49.5m, MaxF = 50.5m,
                NoteRu = "Ловим провалы до −15 % (187 В) — то, что обычно приводит к перезагрузке ИТ-оборудования; всплески и частота — на уровне EN 50160.",
                NoteEn = "Captures sags down to −15 % (187 V) — the level that usually reboots IT equipment; swells and frequency at EN 50160 level."
            },
            new ThresholdPreset
            {
                NameRu = "Сценарий — промышленный объект с двигателями",
                NameEn = "Scenario — industrial site with motors",
                Sag = 176, Swell = 253, MinF = 49.6m, MaxF = 50.4m,
                NoteRu = "Широкие пороги ±15…20 % (176…253 В): на производстве кратковременные провалы при пуске двигателей — норма, важны длительные отклонения.",
                NoteEn = "Wide limits ±15…20 % (176…253 V): in plants short sags from motor starts are normal, long deviations matter."
            },
            new ThresholdPreset
            {
                NameRu = "Сценарий — ИБП / резервное питание",
                NameEn = "Scenario — UPS / backup power",
                Sag = 176, Swell = 242, MinF = 49.5m, MaxF = 50.5m,
                NoteRu = "Контроль перехода на ИБП: провалы до −20 % (176 В) и всплески +10 % (242 В); удобно для проверки работы источника резервного питания.",
                NoteEn = "UPS transfer checks: sags down to −20 % (176 V) and swells +10 % (242 V); handy for validating backup power behaviour."
            },
            new ThresholdPreset
            {
                NameRu = "Сценарий — диагностика сети (±2 %)",
                NameEn = "Scenario — network diagnostics (±2 %)",
                Sag = 216, Swell = 224, MinF = 49.9m, MaxF = 50.1m,
                NoteRu = "Максимальная чувствительность: пороги ±2 % от 220 В (216…224 В) и ±0,1 Гц. Даёт много событий — применять для обследования, а не для длительного мониторинга.",
                NoteEn = "Maximum sensitivity: ±2 % of 220 V (216…224 V) and ±0.1 Hz. Produces many events — for surveys, not long-term monitoring."
            },
            new ThresholdPreset
            {
                NameRu = "Сценарий — только тяжёлые отклонения (±20 %)",
                NameEn = "Scenario — severe deviations only (±20 %)",
                Sag = 176, Swell = 264, MinF = 49.0m, MaxF = 51.0m,
                NoteRu = "Фильтр грубых аварий: реакция только на ±20 % и частоту ±1 Гц. Полезно, когда нужен минимум ложных срабатываний.",
                NoteEn = "Coarse fault filter: reacts only to ±20 % and ±1 Hz. Useful when false events must be minimised."
            },

            // ================= БАЗОВЫЙ =================
            new ThresholdPreset
            {
                NameRu = "Базовый — заводские 220 В / 50 Гц",
                NameEn = "Baseline — factory defaults 220 V / 50 Hz",
                Sag = 210, Swell = 256, MinF = 48.8m, MaxF = 51.2m,
                NoteRu = "Значения по умолчанию, которые использует эта программа для сети 220 В / 50 Гц.",
                NoteEn = "Default values used by this program for a 220 V / 50 Hz network."
            }
        };

        private uint _prevElapsedTicks = 0;
        private DateTime _lastSyncLossReport = DateTime.MinValue;

        // Пока это время не наступило, «потеря синхронизации» не фиксируется:
        // счётчик наработки прибора обнуляется нашей же записью уставок.
        private DateTime _suppressSyncLossUntil = DateTime.MinValue;

        // Последние залогированные «сырые» значения канала N-G и байта конфигурации прибора
        private int _lastLoggedNgRaw = -1;
        private int _lastLoggedCfgByte = -1;

        /// <summary>
        /// Пишет в «Лог обмена» сырые байты заголовка, относящиеся к каналу «нейтраль — земля»:
        /// h[3] (линия), h[4] (сырое значение N-G) и h[51] (байт конфигурации с битом 0x10).
        /// Строка добавляется только при изменении значений, чтобы не забивать лог каждые 1,5 с.
        /// </summary>
        private void LogRawHeaderDetails(byte[] h)
        {
            if (h == null || h.Length < 88) return;

            int ngRaw = h[4];
            int cfg = h[51];
            if (ngRaw == _lastLoggedNgRaw && cfg == _lastLoggedCfgByte) return;

            _lastLoggedNgRaw = ngRaw;
            _lastLoggedCfgByte = cfg;

            double vMult = (cfg & 0x80) != 0 ? 2.0 : 1.0;
            bool ngOn = (cfg & 0x10) != 0;

            LogMessage("RAW: " + T(
                $"линия h[3]={h[3]} (0x{h[3]:X2}) → {h[3] * vMult:0} В; " +
                $"N-G h[4]={ngRaw} (0x{ngRaw:X2}); конфигурация h[51]=0x{cfg:X2}, бит 0x10={(ngOn ? 1 : 0)} " +
                $"→ канал N-G {(ngOn ? "включён, показываем " + (ngRaw * 0.25) + " В" : "выключен (TN), показываем 0 В")}",
                $"line h[3]={h[3]} (0x{h[3]:X2}) -> {h[3] * vMult:0} V; " +
                $"N-G h[4]={ngRaw} (0x{ngRaw:X2}); config h[51]=0x{cfg:X2}, bit 0x10={(ngOn ? 1 : 0)} " +
                $"-> N-G channel {(ngOn ? "enabled, showing " + (ngRaw * 0.25) + " V" : "disabled (TN), showing 0 V")}"));
        }

        /// <summary>
        /// События, обнаруженные самой программой (потеря синхронизации с прибором).
        /// В файл .VER не сохраняются, в таблице помечаются как «обнаружено программой».
        /// </summary>
        private readonly List<EventRecord> _syntheticEvents = new List<EventRecord>();

        /// <summary>
        /// Достраивает элементы, которых не было в первой версии: панель сводки на вкладке
        /// событий и панель «Graph Setup» на вкладке графиков (как в оригинале).
        /// </summary>
        private void BuildParityExtras()
        {
            // --- Панель сводки по типам событий (вкладка Events) ---
            _pnlSummary = new Panel
            {
                Dock = DockStyle.Top,
                Height = 26,
                Padding = new Padding(8, 4, 8, 2),
                BackColor = Color.FromArgb(232, 240, 248)
            };

            _lblSummary = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("MS Sans Serif", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 40, 70)
            };

            _lblUnsavedMark = new Label
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("MS Sans Serif", 8.5f, FontStyle.Bold),
                ForeColor = Color.Firebrick,
                Visible = false
            };

            _pnlSummary.Controls.Add(_lblSummary);
            _pnlSummary.Controls.Add(_lblUnsavedMark);
            _tabEvents.Controls.Add(_pnlSummary);
            _pnlSummary.BringToFront();
            if (_pnlEventsFilter != null) _pnlEventsFilter.BringToFront();

            // --- Панель «Graph Setup» (вкладка Graphs) ---
            _pnlGraphSetup = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                Padding = new Padding(8, 5, 8, 3),
                BackColor = Color.FromArgb(240, 244, 248)
            };

            _chkShowHnEvents = new CheckBox
            {
                Checked = true,
                AutoSize = true,
                Location = new Point(8, 6)
            };
            _chkShowHnEvents.CheckedChanged += (s, e) => _pnlCharts?.Invalidate();

            _chkShowNgEvents = new CheckBox
            {
                Checked = true,
                AutoSize = true,
                Location = new Point(210, 6)
            };
            _chkShowNgEvents.CheckedChanged += (s, e) => _pnlCharts?.Invalidate();

            _lblPeriods = new Label
            {
                AutoSize = true,
                Location = new Point(410, 8),
                Font = new Font("MS Sans Serif", 8.5f)
            };

            _numPeriods = new NumericUpDown
            {
                Location = new Point(500, 5),
                Width = 60,
                Minimum = 1,
                Maximum = 60,
                Value = 12
            };
            _numPeriods.ValueChanged += (s, e) => _pnlCharts?.Invalidate();

            _pnlGraphSetup.Controls.AddRange(new Control[]
            {
                _chkShowHnEvents, _chkShowNgEvents, _lblPeriods, _numPeriods
            });
            _tabCharts.Controls.Add(_pnlGraphSetup);
            // Порядок докинга: сверху полоса выбора типа графика, под ней — панель Graph Setup.
            // В WinForms раньше докидывается контрол с большим индексом, поэтому полосу отправляем в конец списка.
            var chartTopBar = _tabCharts.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
            if (chartTopBar != null && _tabCharts.Controls.Count > 1)
                _tabCharts.Controls.SetChildIndex(chartTopBar, _tabCharts.Controls.Count - 1);

            BuildPresetPanel();

            // Пункты печати в меню «Файл» (как File -> Print в оригинале)
            if (_mnuFile != null)
            {
                _mnuFilePrint = new ToolStripMenuItem(null, null, (s, e) => PrintReport())
                {
                    ShortcutKeys = Keys.Control | Keys.P
                };
                _mnuFilePrintPreview = new ToolStripMenuItem(null, null, (s, e) => PrintReportPreview());

                int insertAt = _mnuFile.DropDownItems.IndexOf(_mnuFileExit);
                if (insertAt < 0) insertAt = _mnuFile.DropDownItems.Count;

                _mnuFile.DropDownItems.Insert(insertAt, new ToolStripSeparator());
                _mnuFile.DropDownItems.Insert(insertAt + 1, _mnuFilePrintPreview);
                _mnuFile.DropDownItems.Insert(insertAt + 2, _mnuFilePrint);
            }

            // Пункт сервисной калибровки в меню «Связь» (аналог «VR101 Calibration»)
            if (_mnuComm != null)
            {
                _mnuCalibration = new ToolStripMenuItem(null, null, (s, e) => ShowCalibrationDialog());
                _mnuComm.DropDownItems.Add(new ToolStripSeparator());
                _mnuComm.DropDownItems.Add(_mnuCalibration);
            }

            UpdateDirtyMarker();
        }

        /// <summary>
        /// Тексты новых элементов на выбранном языке.
        /// </summary>
        private void ApplyParityLocalization()
        {
            if (_chkShowHnEvents != null)
                _chkShowHnEvents.Text = T("Показывать события H-N", "Show Hot to Neutral Events");
            if (_chkShowNgEvents != null)
                _chkShowNgEvents.Text = T("Показывать события N-G", "Show Neutral to Ground Events");
            if (_lblPeriods != null)
                _lblPeriods.Text = T("Периодов:", "Periods:");
            if (_numPeriods != null && _lblPeriods != null)
                _numPeriods.Left = _lblPeriods.Right + 6;

            if (_chkShowHnEvents != null)
            {
                _toolTip?.SetToolTip(_chkShowHnEvents,
                    T("Учитывать события «фаза — нейтраль» на графиках",
                      "Include Hot-to-Neutral events in graphs"));
                _toolTip?.SetToolTip(_chkShowNgEvents,
                    T("Учитывать события «нейтраль — земля» на графиках",
                      "Include Neutral-to-Ground events in graphs"));
                _toolTip?.SetToolTip(_numPeriods,
                    T("Количество периодов (столбцов) на сводной гистограмме",
                      "Number of periods (bars) in the Quick Summary chart"));
                _toolTip?.SetToolTip(_lblUnsavedMark,
                    T("Данные считаны из прибора, но ещё не сохранены в файл .VER",
                      "Data was downloaded from the recorder but not saved to a .VER file"));
            }

            if (_mnuFilePrint != null)
                _mnuFilePrint.Text = T("&Печать отчёта...", "&Print report...");
            if (_mnuFilePrintPreview != null)
                _mnuFilePrintPreview.Text = T("Предварительный просмотр", "Print pre&view");
            if (_mnuCalibration != null)
                _mnuCalibration.Text = T("Калибровка VR101...", "VR101 Calibration...");

            if (_lblPreset != null) _lblPreset.Text = T("Профиль порогов:", "Threshold preset:");
            if (_btnApplyPreset != null) _btnApplyPreset.Text = T("Применить профиль", "Apply preset");
            if (_lblPresetNote != null)
                _lblPresetNote.Text = T("меняет только Sag / Swell и частоту; импульсы и N-G не трогает",
                                        "changes Sag / Swell and frequency only; transients and N-G untouched");
            if (_cmbPreset != null)
            {
                for (int i = 0; i < _presets.Length && i < _cmbPreset.Items.Count; i++)
                {
                    var p = _presets[i];
                    _cmbPreset.Items[i] = (p.Arbitration ? "★ " : "") + T(p.NameRu, p.NameEn);
                }

                int pIdx = _cmbPreset.SelectedIndex;
                if (pIdx >= 0 && pIdx < _presets.Length && _toolTip != null)
                    _toolTip.SetToolTip(_cmbPreset, T(_presets[pIdx].NoteRu, _presets[pIdx].NoteEn));

                ResizePresetComboToContent();
            }

            UpdateEventSummary(_currentSite);
            UpdateDirtyMarker();
        }

        /// <summary>
        /// Пересчитывает сводку по типам событий (аналог счётчиков из отчёта оригинала).
        /// </summary>
        private void UpdateEventSummary(SiteData s)
        {
            if (_lblSummary == null) return;

            int swells = 0, sags = 0, transients = 0, outages = 0, freq = 0, syncLoss = 0, total = 0;
            if (s != null && s.HasValidData)
            {
                var all = new List<EventRecord>();
                all.AddRange(s.Events);
                all.AddRange(s.OpenEvents);
                all.AddRange(_syntheticEvents);

                foreach (var ev in all)
                {
                    total++;
                    switch (ev.Category)
                    {
                        case EventCategory.HnSwell:
                        case EventCategory.NgSwell:
                            swells++;
                            break;
                        case EventCategory.HnSag:
                            sags++;
                            break;
                        case EventCategory.HnTransient:
                        case EventCategory.NgTransient:
                            transients++;
                            break;
                        case EventCategory.Outage:
                            outages++;
                            break;
                        case EventCategory.LossOfSync:
                            syncLoss++;
                            break;
                        case EventCategory.HighFreq:
                        case EventCategory.LowFreq:
                            freq++;
                            break;
                    }
                }
            }

            string text = T(
                $"Событий: {total}   ·   всплески: {swells}   ·   просадки: {sags}   ·   импульсы: {transients}   ·   обрывы: {outages}   ·   частота: {freq}   ·   потеря синхр.: {syncLoss}",
                $"Events: {total}   ·   swells: {swells}   ·   sags: {sags}   ·   transients: {transients}   ·   outages: {outages}   ·   frequency: {freq}   ·   sync loss: {syncLoss}");

            if (s != null && s.HasValidData && s.IsMemoryFull)
                text += T("   ·   ПАМЯТЬ ЗАПОЛНЕНА", "   ·   RECORDER FULL!");

            _lblSummary.Text = text;
        }

        /// <summary>
        /// Панель выбора профиля порогов (по нормативным документам) на вкладке «Статус».
        /// </summary>
        private void BuildPresetPanel()
        {
            if (_tabStatus == null) return;

            _pnlPreset = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                Padding = new Padding(8, 6, 8, 4),
                BackColor = Color.FromArgb(236, 244, 236)
            };

            _lblPreset = new Label { AutoSize = true, Location = new Point(8, 10) };

            _cmbPreset = new ComboBox
            {
                Width = 430,
                Left = 150,
                Top = 6,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            for (int i = 0; i < _presets.Length; i++) _cmbPreset.Items.Add(i.ToString());
            _cmbPreset.SelectedIndex = 0;
            _cmbPreset.SelectedIndexChanged += (s, e) =>
            {
                int idx = _cmbPreset.SelectedIndex;
                if (idx >= 0 && idx < _presets.Length && _toolTip != null)
                    _toolTip.SetToolTip(_cmbPreset, T(_presets[idx].NoteRu, _presets[idx].NoteEn));
            };

            _btnApplyPreset = new Button
            {
                Left = 418,
                Top = 4,
                Width = 150,
                Height = 26,
                BackColor = Color.FromArgb(214, 236, 214)
            };
            _btnApplyPreset.Click += (s, e) => ApplySelectedPreset();

            _lblPresetNote = new Label
            {
                AutoSize = true,
                Left = 578,
                Top = 10,
                ForeColor = Color.DimGray
            };

            _pnlPreset.Controls.AddRange(new Control[] { _lblPreset, _cmbPreset, _btnApplyPreset, _lblPresetNote });
            _tabStatus.Controls.Add(_pnlPreset);
            // Панель добавлена последней, поэтому докинг применяется к ней первой:
            // она занимает верхнюю полосу, а SplitContainer ниже получает оставшуюся высоту.
            // BringToFront здесь вызывать нельзя — панель начала бы накрывать блоки уставок.

            // После раскладки панели пересчитываем ширину списка под фактическую ширину
            _pnlPreset.Resize += (s, e) => ResizePresetComboToContent();
        }

        /// <summary>
        /// Подгоняет ширину выпадающего списка профилей под самый длинный пункт,
        /// чтобы названия профилей не обрезались (в том числе при смене языка и масштаба шрифта).
        /// </summary>
        private void ResizePresetComboToContent()
        {
            if (_cmbPreset == null || _cmbPreset.Items.Count == 0) return;

            int maxTextWidth = 0;
            using (Graphics gg = _cmbPreset.CreateGraphics())
            {
                foreach (object item in _cmbPreset.Items)
                {
                    string text = item as string;
                    if (string.IsNullOrEmpty(text)) continue;

                    Size sz = TextRenderer.MeasureText(gg, text, _cmbPreset.Font);
                    if (sz.Width > maxTextWidth) maxTextWidth = sz.Width;
                }
            }

            // запас под стрелку списка и внутренние отступы
            int width = Math.Max(320, maxTextWidth + SystemInformation.VerticalScrollBarWidth + 28);

            // не даём списку выйти за пределы панели, но только если панель уже разложена
            int panelWidth = _pnlPreset != null ? _pnlPreset.Width : 0;
            if (panelWidth >= 600)
            {
                int roomForCombo = Math.Max(320, panelWidth - _cmbPreset.Left - 200);
                if (width > roomForCombo) width = roomForCombo;
            }

            _cmbPreset.Width = width;

            if (_btnApplyPreset != null) _btnApplyPreset.Left = _cmbPreset.Right + 8;
            if (_lblPresetNote != null && _btnApplyPreset != null) _lblPresetNote.Left = _btnApplyPreset.Right + 12;
        }

        /// <summary>
        /// Переносит значения выбранного профиля в поля порогов (без записи в прибор).
        /// </summary>
        private void ApplySelectedPreset()
        {
            int idx = _cmbPreset != null ? _cmbPreset.SelectedIndex : -1;
            if (idx < 0 || idx >= _presets.Length) return;

            var pr = _presets[idx];

            SetSafeNum(_numHnSag, pr.Sag);
            SetSafeNum(_numHnSwell, pr.Swell);
            SetSafeNum(_numMinFreq, pr.MinF);
            SetSafeNum(_numMaxFreq, pr.MaxF);

            string kind = pr.Arbitration
                ? T("Арбитражный профиль", "Arbitration profile")
                : T("Профиль-сценарий", "Scenario profile");

            string msg = T(
                $"{kind} «{pr.NameRu}» подставлен: просадка {pr.Sag} В, всплеск {pr.Swell} В, частота {pr.MinF:0.0}…{pr.MaxF:0.0} Гц. Пороги импульсов и N-G не изменялись. Проверьте и нажмите «Записать уставки».",
                $"{kind} \"{pr.NameEn}\" applied: sag {pr.Sag} V, swell {pr.Swell} V, frequency {pr.MinF:0.0}…{pr.MaxF:0.0} Hz. Transient and N-G thresholds were not changed. Review and press Send Setup.");

            _lblStatus.Text = msg;
            LogMessage(msg);
        }

        /// <summary>
        /// Включает/выключает пометку «не сохранено» рядом со сводкой.
        /// </summary>
        private void UpdateDirtyMarker()
        {
            if (_lblUnsavedMark == null) return;
            _lblUnsavedMark.Text = T("(не сохранено)", "(not saved)");
            _lblUnsavedMark.Visible = _hasUnsavedSessionData;
        }

        /// <summary>
        /// Список событий для графиков: фильтр по типу/дате (если включён) плюс
        /// тумблеры «показывать H-N / N-G», как в диалоге Graph Setup оригинала.
        /// </summary>
        private List<EventRecord> GetEventsForGraphs()
        {
            var list = GetEventsForChartsAndExport();
            bool showHn = _chkShowHnEvents == null || _chkShowHnEvents.Checked;
            bool showNg = _chkShowNgEvents == null || _chkShowNgEvents.Checked;
            if (showHn && showNg) return list;

            return list.Where(ev =>
            {
                bool isNg = ev.IsNgTransient || ev.Category == EventCategory.NgSwell;
                bool isHn = ev.Category == EventCategory.HnSwell
                         || ev.Category == EventCategory.HnSag
                         || ev.Category == EventCategory.HnTransient;

                if (isNg) return showNg;
                if (isHn) return showHn;
                return true; // события частоты и обрывы питания показываем всегда
            }).ToList();
        }

        private void PopulateEventsGrid(SiteData s)
        {
            _gridEvents.Rows.Clear();
            UpdateEventSummary(s);

            if (s == null || !s.HasValidData || (s.StoredEventCount == 0 && s.Events.Count == 0))
            {
                if (_lblFilterCount != null)
                    _lblFilterCount.Text = T("Показано: 0 из 0", "Showing: 0 of 0");
                _pnlCharts?.Invalidate();
                return;
            }

            int totalRecords = s.OpenEvents.Count + s.Events.Count + _syntheticEvents.Count;
            int shownRecords = 0;

            foreach (var openEv in s.OpenEvents)
            {
                if (!MatchesEventFilter(openEv)) continue;

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
                shownRecords++;
            }

            // События, обнаруженные самой программой (потеря синхронизации с прибором)
            foreach (var syn in _syntheticEvents)
            {
                if (!MatchesEventFilter(syn)) continue;

                int rIdx = _gridEvents.Rows.Add(
                    0,
                    syn.Timestamp.ToString("dd.MM.yyyy HH:mm:ss"),
                    syn.GetLocalizedEventName(_lang),
                    syn.ExtremeText,
                    syn.GetLocalizedEndText(_lang),
                    T("обнаружено программой", "detected by program")
                );
                _gridEvents.Rows[rIdx].Tag = syn;
                _gridEvents.Rows[rIdx].Height = _gridEvents.RowTemplate.Height;
                _gridEvents.Rows[rIdx].DefaultCellStyle.BackColor = Color.FromArgb(255, 246, 205);
                _gridEvents.Rows[rIdx].DefaultCellStyle.Font = new Font(_gridEvents.Font, FontStyle.Italic);
                shownRecords++;
            }

            for (int i = s.Events.Count - 1; i >= 0; i--)
            {
                var ev = s.Events[i];
                if (!MatchesEventFilter(ev)) continue;

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
                shownRecords++;
            }

            if (_lblFilterCount != null)
            {
                _lblFilterCount.Text = T(
                    $"Показано записей: {shownRecords} из {totalRecords}",
                    $"Showing: {shownRecords} of {totalRecords}");
            }

            _pnlCharts?.Invalidate();
        }

        // =========================================================================
        // ОТРИСОВКА 3 ШТАТНЫХ РЕЖИМОВ ГРАФИКОВ (С УЧЕТОМ ФИЛЬТРАЦИИ)
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
            var listToPlot = GetEventsForGraphs();
            if (listToPlot.Count == 0)
            {
                using (var fnt = new Font("MS Sans Serif", 10f * _fontScale))
                {
                    g.DrawString(
                        T("Нет событий, соответствующих выбранному фильтру.", "No events match the selected filter."),
                        fnt, Brushes.Gray, new Point(20, 20));
                }
                return;
            }

            using (var titleFont = new Font("MS Sans Serif", 11f * _fontScale, FontStyle.Bold))
            using (var labelFont = new Font("Arial", 8.5f * _fontScale))
            using (var valFont = new Font("Arial", 9.5f * _fontScale, FontStyle.Bold))
            {
                g.DrawString(
                    T($"Сводная гистограмма: событий за период — SN F{_currentSite.SerialNumber}",
                      $"Quick Summary: Events per Period — SN F{_currentSite.SerialNumber}"),
                    titleFont, Brushes.Black, new Point(20, 15));

                int periods = _numPeriods != null ? (int)_numPeriods.Value : 12;
                periods = Math.Max(1, Math.Min(periods, 60));

                DateTime minT = listToPlot.Min(ev => ev.Timestamp);
                DateTime maxT = listToPlot.Max(ev => ev.Timestamp);
                if (maxT <= minT) maxT = minT.AddMinutes(1);
                double spanSec = (maxT - minT).TotalSeconds;

                var counts = new int[periods];
                foreach (var ev in listToPlot)
                {
                    double rel = (ev.Timestamp - minT).TotalSeconds / spanSec;
                    int p = (int)(rel * periods);
                    if (p < 0) p = 0;
                    if (p >= periods) p = periods - 1;
                    counts[p]++;
                }

                int startX = (int)(70 * _fontScale);
                int startY = rect.Height - (int)(62 * _fontScale);
                int plotH = Math.Max(60, startY - (int)(70 * _fontScale));
                int plotW = Math.Max(120, rect.Width - startX - 30);
                int slot = Math.Max(6, plotW / periods);
                int barW = Math.Max(4, slot - Math.Max(2, slot / 8));
                int maxCount = Math.Max(1, counts.Max());

                g.DrawLine(Pens.Black, startX, startY, startX + periods * slot, startY);
                g.DrawLine(Pens.Black, startX, startY - plotH, startX, startY);

                g.DrawString(T("событий", "events"), labelFont, Brushes.DimGray, new Point(4, startY - plotH - 16));
                g.DrawString(T("окончание периода", "period ending"), labelFont, Brushes.DimGray, new Point(startX, startY + 34));

                int labelEvery = Math.Max(1, (int)Math.Ceiling(periods / 12.0));
                for (int i = 0; i < periods; i++)
                {
                    int barH = counts[i] == 0 ? 0 : Math.Max(2, (int)((counts[i] / (double)maxCount) * (plotH - 10)));
                    int x = startX + i * slot + (slot - barW) / 2;
                    int y = startY - barH;

                    if (barH > 0)
                    {
                        g.FillRectangle(Brushes.SteelBlue, x, y, barW, barH);
                        g.DrawRectangle(Pens.DarkBlue, x, y, barW, barH);
                        if (slot >= 18)
                        {
                            g.DrawString(counts[i].ToString(), valFont, Brushes.DarkRed,
                                new Point(x + barW / 2 - 5, y - (int)(17 * _fontScale)));
                        }
                    }

                    if (i % labelEvery == 0)
                    {
                        DateTime endT = minT.AddSeconds(spanSec * (i + 1) / periods);
                        g.DrawString(endT.ToString("dd.MM HH:mm"), labelFont, Brushes.Black,
                            new Point(startX + i * slot, startY + 6));
                    }
                }
            }
        }

        /// <summary>
        /// Извлекает из записи события длительность (с) и амплитуду (В) для графика
        /// распределения. Для частотных событий «вольтовой» амплитуды нет — возвращает false.
        /// </summary>
        private bool TryMeasureEvent(EventRecord ev, out double seconds, out double volts)
        {
            seconds = 0;
            volts = 0;

            byte[] b = ev.Raw;
            if (b == null || b.Length < 8) return false;

            byte type = b[0];
            double vMult = (_currentSite != null && _currentSite.Is220V) ? 2.0 : 1.0;
            double lineFreq = (_currentSite != null && _currentSite.Is50Hz) ? 50.0 : 60.0;
            bool ng025 = _currentSite != null && _currentSite.NgScale025;

            uint rawDur = (uint)((b[2] << 16) | (b[3] << 8) | b[4]);
            bool durIn8SecTicks = (type == 0x06 || type == 0x86 || type == 0x08 || type == 0x88);
            seconds = durIn8SecTicks ? rawDur * 8.0 : ((rawDur + 1) / 2.0) / lineFreq;

            switch (type)
            {
                case 0x01:
                case 0x02:
                case 0x03:
                case 0x04:
                    // Импульс: амплитуда пиковая в вольтах, длительность в записи не сохраняется
                    volts = b[1] * 10.0;
                    seconds = 0;
                    return true;

                case 0x05:
                case 0x85:
                case 0x16:
                case 0x06:
                case 0x86:
                case 0x17:
                    volts = b[1] * vMult;
                    return true;

                case 0x09:
                case 0x89:
                case 0x12:
                case 0x14:
                    volts = ng025 ? b[1] * 0.25 : b[1];
                    return true;

                case 0x80:
                    volts = 0;
                    return true;

                default:
                    // Частотные и неизвестные события в распределении не участвуют
                    return false;
            }
        }

        private void DrawEventDistributionTimeline(Graphics g, Rectangle rect)
        {
            var all = GetEventsForGraphs();

            var pts = new List<Tuple<EventRecord, double, double>>();
            foreach (var ev in all)
            {
                double sec, volt;
                if (!TryMeasureEvent(ev, out sec, out volt)) continue;
                pts.Add(Tuple.Create(ev, sec, volt));
            }

            if (pts.Count == 0)
            {
                using (var fnt = new Font("MS Sans Serif", 10f * _fontScale))
                {
                    g.DrawString(
                        T("Нет событий, соответствующих выбранному фильтру.", "No events match the selected filter."),
                        fnt, Brushes.Gray, new Point(20, 20));
                }
                return;
            }

            using (var titleFont = new Font("MS Sans Serif", 11f * _fontScale, FontStyle.Bold))
            using (var axisFont = new Font("Arial", 8.5f * _fontScale))
            using (var tickFont = new Font("Arial", 7.5f * _fontScale))
            {
                g.DrawString(
                    T($"Распределение событий — SN F{_currentSite.SerialNumber}",
                      $"Event Distribution — SN F{_currentSite.SerialNumber}"),
                    titleFont, Brushes.Black, new Point(20, 12));

                // Легенда как в оригинале: HN Events / NG Events / Outage and Spikes
                int lx = Math.Max(320, rect.Width - (int)(330 * _fontScale));
                int ly = 14;
                g.FillEllipse(Brushes.RoyalBlue, lx, ly + 2, 10, 10);
                g.DrawString(T("события H-N", "HN Events"), tickFont, Brushes.Black, new Point(lx + 14, ly));
                g.FillEllipse(Brushes.SeaGreen, lx + (int)(110 * _fontScale), ly + 2, 10, 10);
                g.DrawString(T("события N-G", "NG Events"), tickFont, Brushes.Black, new Point(lx + (int)(124 * _fontScale), ly));
                g.FillEllipse(Brushes.Crimson, lx + (int)(220 * _fontScale), ly + 2, 10, 10);
                g.DrawString(T("обрывы и импульсы", "Outage and Spikes"), tickFont, Brushes.Black, new Point(lx + (int)(234 * _fontScale), ly));

                int ox = (int)(82 * _fontScale);
                int oy = rect.Height - (int)(66 * _fontScale);
                int w = Math.Max(150, rect.Width - ox - 40);
                int h = Math.Max(90, oy - (int)(62 * _fontScale));

                double maxSec = Math.Max(0.5, pts.Max(p => p.Item2));
                double maxVolt = Math.Max(50.0, pts.Max(p => p.Item3));

                // Округляем верхние пределы до «красивого» значения
                maxSec = NiceCeil(maxSec);
                maxVolt = NiceCeil(maxVolt);

                g.DrawLine(Pens.Black, ox, oy, ox + w, oy);
                g.DrawLine(Pens.Black, ox, oy - h, ox, oy);

                for (int i = 0; i <= 4; i++)
                {
                    int gy = oy - (int)(h * i / 4.0);
                    g.DrawLine(Pens.Gainsboro, ox, gy, ox + w, gy);
                    g.DrawString((maxVolt * i / 4.0).ToString("0"), tickFont, Brushes.DimGray, new Point(ox - 34, gy - 7));

                    int gx = ox + (int)(w * i / 4.0);
                    g.DrawLine(Pens.Gainsboro, gx, oy, gx, oy - h);
                    g.DrawString((maxSec * i / 4.0).ToString("0.##"), tickFont, Brushes.DimGray, new Point(gx - 10, oy + 4));
                }

                foreach (var p in pts)
                {
                    double sec = p.Item2, volt = p.Item3;
                    int px = ox + (int)((sec / maxSec) * (w - 6));
                    int py = oy - (int)((volt / maxVolt) * (h - 6));

                    Brush br;
                    if (p.Item1.Category == EventCategory.Outage || p.Item1.IsTransient) br = Brushes.Crimson;
                    else if (p.Item1.Category == EventCategory.NgSwell || p.Item1.IsNgTransient) br = Brushes.SeaGreen;
                    else br = Brushes.RoyalBlue;

                    g.FillEllipse(br, px - 4, py - 4, 8, 8);
                }

                g.DrawString(T("длительность, с", "Duration (seconds)"), axisFont, Brushes.Black,
                    new Point(ox + w / 2 - 40, oy + 20));
                g.DrawString(T("амплитуда, В", "Magnitude (volts)"), axisFont, Brushes.Black,
                    new Point(6, oy - h / 2));
            }
        }

        private static double NiceCeil(double value)
        {
            if (value <= 0) return 1;
            double exp = Math.Pow(10, Math.Floor(Math.Log10(value)));
            double mant = value / exp;
            double step = mant <= 1 ? 1 : mant <= 2 ? 2 : mant <= 5 ? 5 : 10;
            return step * exp;
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

        // =========================================================================
        // ОТКРЫТИЕ И СОХРАНЕНИЕ ФАЙЛОВ СЕССИИ (.VER) И ЭКСПОРТ (.CSV)
        // =========================================================================
        // =========================================================================
        // ПЕЧАТЬ ОТЧЁТА (аналог File -> Print оригинала)
        // =========================================================================
        private List<EventRecord> _printEvents = new List<EventRecord>();
        private int _printRowIndex = 0;
        private int _printPageNo = 1;
        private string _printSubtitle = "";

        private void PrintReportPreview()
        {
            PreparePrintSession();
            if (_printEvents.Count == 0 && !_currentSite.HasValidData)
            {
                MessageBox.Show(T("Нет данных для печати!", "Nothing to print!"),
                    "Fluke EventView", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var doc = CreateReportPrintDocument();
            using (var preview = new PrintPreviewDialog
            {
                Document = doc,
                Width = 1000,
                Height = 760,
                UseAntiAlias = true
            })
            {
                preview.ShowDialog(this);
            }
            doc.Dispose();
        }

        private void PrintReport()
        {
            PreparePrintSession();
            var doc = CreateReportPrintDocument();
            using (var dlg = new PrintDialog { Document = doc, UseEXDialog = true })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        doc.Print();
                        LogMessage("Report sent to printer.");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Print error: {ex.Message}");
                        MessageBox.Show(ex.Message, T("Ошибка печати", "Print Error"),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            doc.Dispose();
        }

        /// <summary>
        /// Фиксирует данные на момент печати: заголовок сессии и список событий
        /// (с учётом включённого фильтра по типу и дате).
        /// </summary>
        private void PreparePrintSession()
        {
            _printEvents = GetEventsForChartsAndExport();
            _printRowIndex = 0;
            _printPageNo = 1;

            if (_currentSite == null || !_currentSite.HasValidData)
            {
                _printSubtitle = T("Данные не загружены", "No data loaded");
                return;
            }

            _printSubtitle = T(
                $"Прибор SN F{_currentSite.SerialNumber} · прошивка {_currentSite.FirmwareVersion} · " +
                $"{(_currentSite.Is220V ? "220 В" : "110 В")} / {(_currentSite.Is50Hz ? "50 Гц" : "60 Гц")} · " +
                $"запись с {_currentSite.StartTime:dd.MM.yyyy HH:mm} по {_currentSite.EndTime:dd.MM.yyyy HH:mm}",
                $"Recorder SN F{_currentSite.SerialNumber} · firmware {_currentSite.FirmwareVersion} · " +
                $"{(_currentSite.Is220V ? "220 V" : "110 V")} / {(_currentSite.Is50Hz ? "50 Hz" : "60 Hz")} · " +
                $"from {_currentSite.StartTime:dd.MM.yyyy HH:mm} to {_currentSite.EndTime:dd.MM.yyyy HH:mm}");
        }

        private PrintDocument CreateReportPrintDocument()
        {
            var doc = new PrintDocument();
            doc.DocumentName = T("Отчёт Fluke EventView", "Fluke EventView Report");
            doc.PrintPage += ReportPrintPage;
            return doc;
        }

        private void ReportPrintPage(object sender, PrintPageEventArgs e)
        {
            Graphics g = e.Graphics;
            RectangleF bounds = e.MarginBounds;

            float y = bounds.Top;
            float left = bounds.Left;
            float width = bounds.Width;

            using (var h1 = new Font("Arial", 14f, FontStyle.Bold))
            using (var h2 = new Font("Arial", 9.5f, FontStyle.Bold))
            using (var body = new Font("Arial", 9f))
            using (var small = new Font("Arial", 8f))
            {
                // Заголовок страницы
                g.DrawString(T("FLUKE EVENTVIEW — ОТЧЁТ ПО СЕССИИ", "FLUKE EVENTVIEW — SITE REPORT"),
                    h1, Brushes.Black, left, y);
                y += 26;

                g.DrawString(_printSubtitle, body, Brushes.Black, left, y);
                y += 18;

                if (_currentSite != null && _currentSite.HasValidData)
                {
                    string thr = T(
                        $"Пороги: всплеск {_currentSite.HnSwell} В · просадка {_currentSite.HnSag} В · импульс {_currentSite.HnImpulse} Вп · " +
                        $"N-G {_currentSite.NgSurge} В / {_currentSite.NgImpulse} Вп · частота {_currentSite.MinFreq:0.0}–{_currentSite.MaxFreq:0.0} Гц",
                        $"Thresholds: swell {_currentSite.HnSwell} V · sag {_currentSite.HnSag} V · transient {_currentSite.HnImpulse} Vp · " +
                        $"N-G {_currentSite.NgSurge} V / {_currentSite.NgImpulse} Vp · frequency {_currentSite.MinFreq:0.0}–{_currentSite.MaxFreq:0.0} Hz");
                    g.DrawString(thr, small, Brushes.DimGray, left, y);
                    y += 16;

                    if (_lblSummary != null && !string.IsNullOrEmpty(_lblSummary.Text))
                    {
                        g.DrawString(_lblSummary.Text, small, Brushes.DimGray, left, y);
                        y += 16;
                    }
                }

                y += 4;
                g.DrawLine(Pens.Black, left, y, left + width, y);
                y += 6;

                // Шапка таблицы
                float colNo = left;
                float colDate = left + width * 0.06f;
                float colEvent = left + width * 0.30f;
                float colMag = left + width * 0.58f;
                float colEnd = left + width * 0.74f;

                g.DrawString("#", h2, Brushes.Black, colNo, y);
                g.DrawString(T("Дата и время", "Start Time"), h2, Brushes.Black, colDate, y);
                g.DrawString(T("Событие", "Event"), h2, Brushes.Black, colEvent, y);
                g.DrawString(T("Амплитуда", "Extreme"), h2, Brushes.Black, colMag, y);
                g.DrawString(T("Длительность/фаза", "Duration/Degree"), h2, Brushes.Black, colEnd, y);
                y += 16;
                g.DrawLine(Pens.Gray, left, y, left + width, y);
                y += 4;

                float lineH = body.GetHeight(g) + 4f;

                // Строки событий
                while (_printRowIndex < _printEvents.Count && y + lineH < bounds.Bottom - 24)
                {
                    var ev = _printEvents[_printRowIndex];
                    g.DrawString(ev.Index.ToString(), body, Brushes.Black, colNo, y);
                    g.DrawString(ev.Timestamp.ToString("dd.MM.yyyy HH:mm:ss"), body, Brushes.Black, colDate, y);
                    g.DrawString(ev.GetLocalizedEventName(_lang), body, Brushes.Black, colEvent, y);
                    g.DrawString(ev.ExtremeText, body, Brushes.Black, colMag, y);
                    g.DrawString(ev.GetLocalizedEndText(_lang), body, Brushes.Black, colEnd, y);

                    y += lineH;
                    _printRowIndex++;
                }

                if (_printEvents.Count == 0)
                {
                    g.DrawString(T("Событий нет.", "No events."), body, Brushes.Gray, colDate, y);
                    y += lineH;
                }

                // Нижний колонтитул
                string footer = T($"Страница {_printPageNo} · записей: {_printEvents.Count}",
                                  $"Page {_printPageNo} · records: {_printEvents.Count}");
                g.DrawString(footer, small, Brushes.Gray, left, bounds.Bottom - 14);

                if (_printRowIndex < _printEvents.Count)
                {
                    _printPageNo++;
                    e.HasMorePages = true;
                }
                else
                {
                    e.HasMorePages = false;
                    _printRowIndex = 0;
                    _printPageNo = 1;
                }
            }
        }

        /// <summary>
        /// Открывает окно сервисной калибровки, передавая ему текущее измерение прибора.
        /// </summary>
        private void ShowCalibrationDialog()
        {
            using (var dlg = new CalibrationForm(
                () => (_currentSite != null && _currentSite.HasValidData) ? _currentSite.LiveHnVolt : 0,
                () => _isScanning && _serialPort != null && _serialPort.IsOpen,
                _lang,
                LogMessage))
            {
                dlg.ShowDialog(this);
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
                        this.Text = $"Fluke EventView VR101 - [SN F{_currentSite.SerialNumber} - {Path.GetFileName(ofd.FileName)}]";
                        PopulateSetupInputsFromSite(_currentSite);
                        SyncFilterDatePickersFromSite(_currentSite);
                        UpdateRealtimeAndInfoPanels(_currentSite);
                        PopulateEventsGrid(_currentSite);
                        _hasUnsavedSessionData = false;
                        UpdateDirtyMarker();
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
                    _hasUnsavedSessionData = false;
                    UpdateDirtyMarker();
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
                    bool applyFilter = _chkFilterChartsAndCsv != null && _chkFilterChartsAndCsv.Checked && IsAnyFilterActive();
                    var sb = new StringBuilder();
                    sb.AppendLine("Event,Start Time,Event,Extreme,End,RawHex");

                    foreach (var openEv in _currentSite.OpenEvents)
                    {
                        if (applyFilter && !MatchesEventFilter(openEv)) continue;
                        sb.AppendLine($"0,\"{openEv.Timestamp:dd.MM.yyyy HH:mm:ss}\",\"{openEv.GetLocalizedEventName(_lang)}\",\"{openEv.ExtremeText}\",\"{openEv.GetLocalizedEndText(_lang)}\",\"{BitConverter.ToString(openEv.Raw)}\"");
                    }

                    for (int i = _currentSite.Events.Count - 1; i >= 0; i--)
                    {
                        var ev = _currentSite.Events[i];
                        if (applyFilter && !MatchesEventFilter(ev)) continue;
                        sb.AppendLine($"{ev.Index},\"{ev.Timestamp:dd.MM.yyyy HH:mm:ss}\",\"{ev.GetLocalizedEventName(_lang)}\",\"{ev.ExtremeText}\",\"{ev.GetLocalizedEndText(_lang)}\",\"{BitConverter.ToString(ev.Raw)}\"");
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show(T("Файл успешно сохранен!", "File Saved!"), "Fluke EventView", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _ledBlinkTimer?.Stop();
            StopScanningPort();
            base.OnFormClosing(e);
        }
    }

    // =========================================================================
    // КЛАСС ДАННЫХ ПРИБОРА И ПАРСЕР ФОРМАТА .VER (VB3 152B Header + 16B Events)
    // =========================================================================
}
