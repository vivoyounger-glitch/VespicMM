using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CULauncher
{
    public class DllGroup
    {
        public string Name { get; set; }
        public bool IsExpanded { get; set; } = false;
    }

    public class ModItem
    {
        public string FullPath { get; set; }
        public string CleanName { get; set; }
        public bool IsFolder { get; set; }
        public bool IsDisabled { get; set; }
        public string DllGroupName { get; set; } = ""; 
    }

    public class CustomListPanel : FlowLayoutPanel
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        public CustomListPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();
            AutoScroll = true;
            HorizontalScroll.Enabled = false;
            HorizontalScroll.Visible = false;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            HideScrollBars();
        }

        private void HideScrollBars()
        {
            if (!IsHandleCreated) return;
            ShowScrollBar(Handle, 0, false); // горизонтальний
            ShowScrollBar(Handle, 1, false); // вертикальний
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0114) return; // WM_HSCROLL — блокуємо горизонтальний
            base.WndProc(ref m);
            if (m.Msg == 0x0083 || m.Msg == 0x0085)
                HideScrollBars();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var brush = new SolidBrush(Color.FromArgb(160, 15, 20, 15)))
                e.Graphics.FillRectangle(brush, ClientRectangle);
            using (var pen = new Pen(Color.FromArgb(68, 58, 22), 1))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            base.OnPaint(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            HideScrollBars();
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            HideScrollBars();
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            HideScrollBars();
        }

        protected override void OnControlRemoved(ControlEventArgs e)
        {
            base.OnControlRemoved(e);
            HideScrollBars();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            HideScrollBars();
        }
    }

    public class MainForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public static readonly Color C_BG     = UiTheme.Bg;
        public static readonly Color C_PANEL2 = UiTheme.PanelHeader;
        public static readonly Color C_ITEM_BG= UiTheme.Item;
        public static readonly Color C_BORDER  = UiTheme.Border;
        public static readonly Color C_AMBER   = UiTheme.Amber;
        public static readonly Color C_GREEN   = UiTheme.Green;
        public static readonly Color C_RED     = UiTheme.Red;
        public static readonly Color C_TEXT    = UiTheme.Text;
        public static readonly Color C_DIM     = UiTheme.Dim;
        public static readonly Color C_SEL     = UiTheme.Selection;

        static readonly string[] SKIP = { "bepinex", "configuration", "core", "patchers", "plugins", "cu_modlauncher" };

        string _pluginsDir;
        string _gameExe;
        public readonly List<ModItem> _mods = new List<ModItem>();
        readonly List<string> _virtualFolders = new List<string> { "ВСІ МОДИ" };
        public readonly List<DllGroup> _dllGroups = new List<DllGroup>();
        
        string _selectedFolder = "ВСІ МОДИ";
        readonly HashSet<ModItem> _selectedMods = new HashSet<ModItem>();
        object _selectedRightItem = null;
        string _configPath;
        public Dictionary<string, string> _modFolderMap = new Dictionary<string, string>();
        public Dictionary<string, string> _dllToGroupMap = new Dictionary<string, string>(); 
        readonly Dictionary<string, List<string>> _dllGroupMemberOrder = new Dictionary<string, List<string>>();
        HashSet<string> _knownDlls = new HashSet<string>(); 
        int _lastRightPanelIdx = -1;
        readonly List<object> _rightPanelSelectOrder = new List<object>();
        string _modSearchFilter = "";
        ToolTip _toolTip;

        Bitmap _bgImage = null;
        Timer _parallaxTimer;
        double _targetX = 0, _targetY = 0;
        double _currentX = 0, _currentY = 0;
        const int MAX_OFFSET = 25;

        Label _logoHeader;
        CustomListPanel _folderList;
        CustomListPanel _modListPanel;
        Label _leftHeader;
        Label _rightHeader;
        Button _btnNewFolder;
        Button _btnRefresh;
        Button _btnOpenFolder;
        Button _btnPlay;
        Button _btnPlayNoMods;
        TextBox _txtModSearch;
        Button _btnMassToggle;
        Button _btnMassDelete;
        Button _btnAssignFolder;
        Button _btnLang;
        Button _btnManageDllGroups;
        Button _btnGrpDelete;
        Button _btnGrpUngroup;
        Button _btnGrpUp;
        Button _btnGrpDown;
        Button _btnGrpAssignFolder;
        Button _btnRenameFolder;
        Button _btnMoveAndDeleteFolder;
        Label _statusBar;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;
                return cp;
            }
        }

        public MainForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();
            AutoScaleMode = AutoScaleMode.Font;

            int useDark = 1;
            DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));

            try 
            { 
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); 
            } 
            catch 
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (File.Exists(iconPath)) { try { this.Icon = new Icon(iconPath); } catch { } }
            }

            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher_debug.txt");
            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher_virtual_packs.cfg");
            
            try
            {
                File.AppendAllText(logPath, "[MainForm] Starting Smooth Parallax Engine...\r\n");
                
                LoadBackground(); 
                BuildUI();
                LoadVirtualConfig();

                this.Load += (s, e) => {
                    try
                    {
                        FindPaths();
                        ReloadMods(true);
                        UpdateLocalization();
                        StartParallax(); 
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText(logPath, $"[Load Error]: {ex.Message}\r\n");
                    }
                };
            }
            catch (Exception ex)
            {
                File.AppendAllText(logPath, $"[Constructor Error]: {ex.Message}\r\n");
                throw;
            }
        }

        void LoadBackground()
        {
            string bgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "background.png");
            if (!File.Exists(bgPath)) return;

            try
            {
                using (var original = new Bitmap(bgPath))
                {
                    int targetWidth = 860 + (MAX_OFFSET * 2);
                    int targetHeight = 620 + (MAX_OFFSET * 2);
                    
                    _bgImage = new Bitmap(targetWidth, targetHeight);
                    using (var g = Graphics.FromImage(_bgImage))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(original, 0, 0, targetWidth, targetHeight);

                        using (var brush = new SolidBrush(Color.FromArgb(115, 10, 12, 10)))
                        {
                            g.FillRectangle(brush, 0, 0, targetWidth, targetHeight);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher_debug.txt");
                File.AppendAllText(logPath, $"[BG Load Error]: {ex.Message}\r\n");
            }
        }

        void StartParallax()
        {
            if (_bgImage == null) return;

            _parallaxTimer = new Timer();
            _parallaxTimer.Interval = 15;
            _parallaxTimer.Tick += (s, e) =>
            {
                Point mousePos = this.PointToClient(Cursor.Position);

                if (this.ClientRectangle.Contains(mousePos) && Form.ActiveForm == this)
                {
                    double centerX = this.ClientSize.Width / 2.0;
                    double centerY = this.ClientSize.Height / 2.0;

                    double pctX = (mousePos.X - centerX) / centerX;
                    double pctY = (mousePos.Y - centerY) / centerY;

                    pctX = Math.Max(-1.0, Math.Min(1.0, pctX));
                    pctY = Math.Max(-1.0, Math.Min(1.0, pctY));

                    _targetX = -pctX * MAX_OFFSET;
                    _targetY = -pctY * MAX_OFFSET;
                }
                else
                {
                    _targetX = 0;
                    _targetY = 0;
                }

                if (Math.Abs(_targetX - _currentX) > 0.01 || Math.Abs(_targetY - _currentY) > 0.01)
                {
                    _currentX += (_targetX - _currentX) * 0.08;
                    _currentY += (_targetY - _currentY) * 0.08;
                    this.Invalidate(false);
                }
            };
            _parallaxTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_bgImage != null)
            {
                int posX = -MAX_OFFSET + (int)_currentX;
                int posY = -MAX_OFFSET + (int)_currentY;
                e.Graphics.DrawImage(_bgImage, posX, posY);
            }
            else
            {
                base.OnPaint(e);
            }
        }

        void LoadVirtualConfig()
        {
            if (!File.Exists(_configPath)) return;
            try
            {
                var lines = File.ReadAllLines(_configPath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("LANG:")) {
                        Lang.IsUkr = line.Substring(5).Trim() == "UA";
                    }
                    else if (line.StartsWith("F:")) {
                        string fName = line.Substring(2).Trim();
                        if (!_virtualFolders.Contains(fName)) _virtualFolders.Add(fName);
                    }
                    else if (line.StartsWith("G:")) {
                        string gName = line.Substring(2).Trim();
                        if (!_dllGroups.Any(g => g.Name == gName)) _dllGroups.Add(new DllGroup { Name = gName });
                    }
                    else if (line.Contains("->")) {
                        var parts = line.Split(new[] { "--" + ">" }, StringSplitOptions.None);
                        if (parts.Length == 2) _modFolderMap[parts[0].Trim()] = parts[1].Trim();
                    }
                    else if (line.Contains("=>")) {
                        var parts = line.Split(new[] { "=>" }, StringSplitOptions.None);
                        if (parts.Length == 2) _dllToGroupMap[parts[0].Trim()] = parts[1].Trim();
                    }
                    else if (line.StartsWith("GO:")) {
                        var parts = line.Substring(3).Split(new[] { ':' }, 2);
                        if (parts.Length == 2)
                        {
                            var names = parts[1].Split('|').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                            if (names.Count > 0) _dllGroupMemberOrder[parts[0].Trim()] = names;
                        }
                    }
                    else if (line.StartsWith("KNOWN:")) {
                        _knownDlls.Add(line.Substring(6).Trim());
                    }
                }
            }
            catch {}
        }

        public void SaveVirtualConfig()
        {
            try
            {
                var lines = new List<string>();
                lines.Add("LANG:" + (Lang.IsUkr ? "UA" : "EN"));
                foreach (var f in _virtualFolders) if (f != "ВСІ МОДИ" && f != "ALL MODS") lines.Add("F:" + f);
                foreach (var g in _dllGroups) lines.Add("G:" + g.Name);
                foreach (var kvp in _modFolderMap) lines.Add(kvp.Key + "->" + kvp.Value);
                foreach (var kvp in _dllToGroupMap) lines.Add(kvp.Key + "=>" + kvp.Value);
                foreach (var kvp in _dllGroupMemberOrder)
                    if (kvp.Value.Count > 0) lines.Add("GO:" + kvp.Key + ":" + string.Join("|", kvp.Value));
                foreach (var kd in _knownDlls) lines.Add("KNOWN:" + kd);
                File.WriteAllLines(_configPath, lines);
            }
            catch {}
        }

        void FindPaths()
        {
            string cur = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 6; i++)
            {
                if (string.IsNullOrEmpty(cur) || !Directory.Exists(cur)) break;
                var exes = Directory.GetFiles(cur, "*.exe");
                foreach (var f in exes)
                {
                    string nm = Path.GetFileNameWithoutExtension(f).ToLower();
                    if (nm.Contains("crash") || nm.Contains("launcher") || nm.Contains("vespic")) continue;
                    _gameExe = f; break;
                }
                string bp = Path.Combine(cur, "BepInEx", "plugins");
                if (Directory.Exists(bp)) _pluginsDir = bp;
                if (_gameExe != null && _pluginsDir != null) break;
                cur = Path.GetDirectoryName(cur);
            }

            if (_pluginsDir == null)
            {
                using (var fbd = new FolderBrowserDialog { Description = Lang.SelectPlugins })
                    if (fbd.ShowDialog() == DialogResult.OK) _pluginsDir = fbd.SelectedPath;
            }
            if (_gameExe == null)
            {
                using (var ofd = new OpenFileDialog { Filter = "Executable|*.exe", Title = Lang.SelectExe })
                    if (ofd.ShowDialog() == DialogResult.OK) _gameExe = ofd.FileName;
            }
        }

        public void ReloadMods() => ReloadMods(false);

        public void ReloadMods(bool detectNewDlls)
        {
            _mods.Clear();
            _selectedMods.Clear();
            _selectedRightItem = null;
            _lastRightPanelIdx = -1;

            _virtualFolders[0] = Lang.IsUkr ? "ВСІ МОДИ" : "ALL MODS";
            if (_selectedFolder == "ВСІ МОДИ" || _selectedFolder == "ALL MODS") 
                _selectedFolder = _virtualFolders[0];

            if (string.IsNullOrEmpty(_pluginsDir) || !Directory.Exists(_pluginsDir))
            {
                _statusBar.Text = Lang.IsUkr ? "Папку plugins не значено." : "Plugins folder not found.";
                return;
            }

            foreach (var sub in Directory.GetDirectories(_pluginsDir))
            {
                string name = Path.GetFileName(sub);
                if (SKIP.Contains(name.ToLower())) continue;
                bool dis = name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                _mods.Add(new ModItem {
                    FullPath = sub,
                    CleanName = dis ? name.Substring(0, name.Length - 9) : name,
                    IsFolder = true, IsDisabled = dis
                });
            }

            List<ModItem> currentDlls = new List<ModItem>();
            var dllFiles = Directory.GetFiles(_pluginsDir, "*.dll").Concat(Directory.GetFiles(_pluginsDir, "*.dll.disabled"));
            
            foreach (var f in dllFiles)
            {
                string name = Path.GetFileName(f);
                if (SKIP.Any(s => name.ToLower().Contains(s))) continue;
                bool dis = name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                var item = new ModItem {
                    FullPath = f,
                    CleanName = dis ? name.Substring(0, name.Length - 9) : name,
                    IsFolder = false, IsDisabled = dis
                };
                
                if (_dllToGroupMap.ContainsKey(item.CleanName))
                {
                    item.DllGroupName = _dllToGroupMap[item.CleanName];
                }
                
                currentDlls.Add(item);
                _mods.Add(item);
            }

            if (detectNewDlls && currentDlls.Count > 0)
            {
                var newDlls = currentDlls.Where(d => !_knownDlls.Contains(d.CleanName)).ToList();
                if (newDlls.Count > 0) {
                    string question = Lang.IsUkr 
                        ? "Знайдено нові DLL-файли (" + newDlls.Count + " шт.). Чи належать вони до одного мода?" 
                        : "Found new DLL files (" + newDlls.Count + " pcs). Do they belong to the same mod?";
                    
                    string title = Lang.IsUkr ? "Групування DLL" : "DLL Grouping";
                    if (MessageBox.Show(question, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        string prompt = Lang.IsUkr ? "Введіть назву для цієї DLL-групи:" : "Enter name for this DLL group:";
                        using (var dlg = new InputDialog(prompt))
                        {
                            if (dlg.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.Result))
                            {
                                string gName = dlg.Result.Trim();
                                if (!_dllGroups.Any(g => g.Name == gName))
                                {
                                    _dllGroups.Add(new DllGroup { Name = gName });
                                }
                                foreach (var d in newDlls)
                                {
                                    _dllToGroupMap[d.CleanName] = gName;
                                    d.DllGroupName = gName;
                                }
                                EnsureMemberOrder(gName, newDlls.Select(d => d.CleanName));
                            }
                        }
                    }
                }
                
                foreach (var d in currentDlls)
                {
                    _knownDlls.Add(d.CleanName);
                }
                SaveVirtualConfig();
            }

            foreach (var gn in _dllToGroupMap.Values.Distinct())
            {
                if (!_dllGroups.Any(g => g.Name == gn))
                    _dllGroups.Add(new DllGroup { Name = gn });
            }
            foreach (var gn in _dllToGroupMap.Values.Distinct())
            {
                var names = _mods.Where(m => m.DllGroupName == gn).Select(m => m.CleanName);
                EnsureMemberOrder(gn, names);
            }

            _mods.Sort((a, b) => string.Compare(a.CleanName, b.CleanName, StringComparison.OrdinalIgnoreCase));

            RenderFolders();
            RenderMods();
            UpdateControlPanel();
        }

        void RenderFolders()
        {
            _folderList.Controls.Clear();
            _folderList.SuspendLayout();

            foreach (var fName in _virtualFolders)
            {
                bool isRoot = (fName == _virtualFolders[0]);
                bool isSelected = fName == _selectedFolder;
                
                var p = new Panel { 
                    Size = new Size(232, 40), 
                    BackColor = isSelected ? C_SEL : C_ITEM_BG, 
                    Margin = new Padding(6, 4, 0, 0), 
                    Cursor = Cursors.Hand 
                };
                
                var folderMods = isRoot ? _mods : _mods.Where(m => _modFolderMap.ContainsKey(m.CleanName) && _modFolderMap[m.CleanName] == fName).ToList();
                bool anyEnabled = folderMods.Any(m => !m.IsDisabled);
                bool allDisabled = folderMods.Count > 0 && folderMods.All(m => m.IsDisabled);

                var btnFolderState = new Label {
                    Text = allDisabled ? "○" : "●",
                    Font = new Font("Consolas", 12f, FontStyle.Bold),
                    ForeColor = allDisabled ? C_RED : C_GREEN,
                    Location = new Point(6, 10), Size = new Size(20, 20),
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                
                btnFolderState.Click += (s, e) => {
                    bool shouldDisableAll = anyEnabled; 
                    foreach (var m in folderMods)
                    {
                        if (m.IsDisabled != shouldDisableAll) ToggleSingleModSilently(m, shouldDisableAll);
                    }
                    ReloadMods();
                };

                var lbl = new Label {
                    Text = fName.ToUpper(),
                    Font = new Font("Consolas", 9f, FontStyle.Bold),
                    ForeColor = C_TEXT,
                    Location = new Point(28, 12),
                    Size = new Size(160, 20),
                    BackColor = Color.Transparent
                };

                p.Controls.Add(btnFolderState);
                p.Controls.Add(lbl);

                if (!isRoot)
                {
                    var btnDelF = new Label { 
                        Text = "×", 
                        Font = new Font("Consolas", 14f, FontStyle.Bold), 
                        ForeColor = C_RED, 
                        Location = new Point(202, 8), 
                        Size = new Size(20, 20), 
                        BackColor = Color.Transparent,
                        TextAlign = ContentAlignment.MiddleCenter 
                    };
                    btnDelF.Click += (s, e) => {
                        _virtualFolders.Remove(fName);
                        var keys = _modFolderMap.Where(kvp => kvp.Value == fName).Select(kvp => kvp.Key).ToList();
                        foreach (var k in keys) _modFolderMap.Remove(k);
                        _selectedFolder = _virtualFolders[0];
                        SaveVirtualConfig();
                        ReloadMods();
                    };
                    p.Controls.Add(btnDelF);
                }

                string targetFolder = fName;
                p.Click += (s, e) => { _selectedFolder = targetFolder; _selectedMods.Clear(); _selectedRightItem = null; RenderFolders(); RenderMods(); UpdateControlPanel(); };
                lbl.MouseClick += (s, e) => {
                    if (e.Clicks == 2) return;
                    _selectedFolder = targetFolder; _selectedMods.Clear(); _selectedRightItem = null; RenderFolders(); RenderMods(); UpdateControlPanel();
                };

                _folderList.Controls.Add(p);
            }

            _folderList.ResumeLayout();
        }

        void EnsureMemberOrder(string gName, IEnumerable<string> memberNames)
        {
            var members = memberNames.ToList();
            if (!_dllGroupMemberOrder.ContainsKey(gName))
                _dllGroupMemberOrder[gName] = new List<string>(members);
            else
            {
                var order = _dllGroupMemberOrder[gName];
                order.RemoveAll(n => !members.Contains(n));
                foreach (var n in members)
                    if (!order.Contains(n)) order.Add(n);
            }
        }

        List<ModItem> GetOrderedGroupMods(string gName, List<ModItem> filteredMods)
        {
            var members = filteredMods.Where(m => m.DllGroupName == gName).ToList();
            if (members.Count == 0) return members;
            EnsureMemberOrder(gName, members.Select(m => m.CleanName));
            var order = _dllGroupMemberOrder[gName];
            var result = new List<ModItem>();
            foreach (var name in order)
            {
                var m = members.FirstOrDefault(x => x.CleanName == name);
                if (m != null) result.Add(m);
            }
            foreach (var m in members)
                if (!result.Contains(m)) result.Add(m);
            return result;
        }

        static bool IsKeyDown(Keys key) => (Control.ModifierKeys & key) == key;

        string MultiSelectHint() => Lang.IsUkr
            ? "Ctrl+клік — додати/прибрати з виділення\nShift+клік — виділити кілька підряд\nПробіл — увімкнути/вимкнути виділене"
            : "Ctrl+click — add/remove from selection\nShift+click — select a range\nSpace — toggle enabled state";

        void WireSelectTooltip(Control c)
        {
            if (_toolTip == null) return;
            _toolTip.SetToolTip(c, MultiSelectHint());
        }

        void HandleRightPanelSelect(object entry, int idx)
        {
            if (IsKeyDown(Keys.Shift) && _lastRightPanelIdx >= 0 && _lastRightPanelIdx < _rightPanelSelectOrder.Count)
            {
                _selectedMods.Clear();
                _selectedRightItem = null;
                int start = Math.Min(_lastRightPanelIdx, idx);
                int end = Math.Max(_lastRightPanelIdx, idx);
                for (int k = start; k <= end; k++)
                {
                    if (_rightPanelSelectOrder[k] is ModItem m) _selectedMods.Add(m);
                    else if (_rightPanelSelectOrder[k] is DllGroup g) _selectedRightItem = g;
                }
                if (_rightPanelSelectOrder[end] is ModItem lastMod) _selectedRightItem = lastMod;
            }
            else if (IsKeyDown(Keys.Control))
            {
                if (entry is ModItem mod)
                {
                    if (_selectedMods.Contains(mod)) _selectedMods.Remove(mod);
                    else _selectedMods.Add(mod);
                    _selectedRightItem = mod;
                }
                else if (entry is DllGroup grp)
                {
                    var members = _mods.Where(m => m.DllGroupName == grp.Name).ToList();
                    bool anyIn = members.Any(m => _selectedMods.Contains(m));
                    if (anyIn) { foreach (var m in members) _selectedMods.Remove(m); }
                    else { foreach (var m in members) _selectedMods.Add(m); }
                    _selectedRightItem = grp;
                }
                _lastRightPanelIdx = idx;
            }
            else
            {
                _selectedMods.Clear();
                if (entry is ModItem singleMod)
                {
                    _selectedMods.Add(singleMod);
                    _selectedRightItem = singleMod;
                }
                else if (entry is DllGroup singleGrp)
                {
                    _selectedRightItem = singleGrp;
                }
                _lastRightPanelIdx = idx;
            }
        }

        bool IsTextInputFocused() => _txtModSearch != null && _txtModSearch.Focused;

        void ToggleFocusedSelection()
        {
            if (_selectedRightItem is DllGroup grp && _selectedMods.Count == 0)
            {
                var members = _mods.Where(m => m.DllGroupName == grp.Name).ToList();
                bool disable = members.Any(m => !m.IsDisabled);
                foreach (var m in members) ToggleSingleModSilently(m, disable);
            }
            else if (_selectedMods.Count > 0)
            {
                foreach (var m in _selectedMods.ToList())
                {
                    if (m.IsFolder) ToggleSingleModSilently(m, !m.IsDisabled);
                    else OnModStateButtonClick(m);
                }
            }
            else if (_selectedRightItem is ModItem one)
            {
                if (one.IsFolder) ToggleSingleModSilently(one, !one.IsDisabled);
                else OnModStateButtonClick(one);
            }
            else return;

            RenderMods();
            RenderFolders();
            UpdateControlPanel();
        }

        void LaunchGameWithoutMods()
        {
            if (string.IsNullOrEmpty(_gameExe) || !File.Exists(_gameExe)) return;

            var enabledNames = _mods.Where(m => !m.IsDisabled).Select(m => m.CleanName).ToList();
            foreach (var m in _mods.Where(m => !m.IsDisabled).ToList())
                ToggleSingleModSilently(m, true);
            ReloadMods();

            try
            {
                var p = Process.Start(new ProcessStartInfo {
                    FileName = _gameExe,
                    WorkingDirectory = Path.GetDirectoryName(_gameExe)
                });
                if (p == null) return;
                p.EnableRaisingEvents = true;
                p.Exited += (s, e) => {
                    try
                    {
                        BeginInvoke(new Action(() => {
                            foreach (var name in enabledNames)
                            {
                                var m = _mods.FirstOrDefault(x => x.CleanName == name);
                                if (m != null && m.IsDisabled) ToggleSingleModSilently(m, false);
                            }
                            ReloadMods();
                        }));
                    }
                    catch { }
                };
            }
            catch (Exception ex)
            {
                ReloadMods();
                foreach (var name in enabledNames)
                {
                    var m = _mods.FirstOrDefault(x => x.CleanName == name);
                    if (m != null && m.IsDisabled) ToggleSingleModSilently(m, false);
                }
                ReloadMods();
                MessageBox.Show(ex.Message);
            }
        }

        bool ModMatchesSearch(ModItem m, string q)
        {
            if (m.CleanName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (!string.IsNullOrEmpty(m.DllGroupName) && m.DllGroupName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        List<ModItem> GetGroupActionTargets()
        {
            var grp = GetSelectedDllGroup();
            if (grp == null)
            {
                if (_selectedMods.Count > 0)
                {
                    string gn = _selectedMods.First().DllGroupName;
                    if (!string.IsNullOrEmpty(gn) && _selectedMods.All(m => m.DllGroupName == gn))
                        return _selectedMods.ToList();
                }
                return new List<ModItem>();
            }
            if (_selectedRightItem is DllGroup || _selectedMods.Count == 0)
                return _mods.Where(m => m.DllGroupName == grp.Name).ToList();
            return _selectedMods.Where(m => m.DllGroupName == grp.Name).ToList();
        }

        DllGroup GetSelectedDllGroup()
        {
            if (_selectedRightItem is DllGroup dg) return dg;
            return null;
        }

        bool IsGroupContextActive() => GetSelectedDllGroup() != null;

        bool IsModSelected(ModItem m) => _selectedMods.Contains(m) || ReferenceEquals(_selectedRightItem, m);

        bool IsGroupHeaderSelected(DllGroup g) => ReferenceEquals(_selectedRightItem, g);

        void AfterSelectionChanged()
        {
            RefreshSelectionVisuals();
            UpdateControlPanel();
        }

        void RefreshSelectionVisuals()
        {
            foreach (Control c in _modListPanel.Controls)
            {
                if (c is ModRowControl mr) mr.SetSelected(IsModSelected(mr.Mod));
                else if (c is GroupRowControl gr) gr.SetSelected(IsGroupHeaderSelected(gr.Group));
            }
        }

        int RegisterSelectRow(object entry)
        {
            _rightPanelSelectOrder.Add(entry);
            return _rightPanelSelectOrder.Count - 1;
        }

        void RenderMods()
        {
            _modListPanel.Controls.Clear();
            _modListPanel.SuspendLayout();
            _rightPanelSelectOrder.Clear();

            List<ModItem> folderMods = (_selectedFolder == _virtualFolders[0]) 
                ? _mods 
                : _mods.Where(m => _modFolderMap.ContainsKey(m.CleanName) && _modFolderMap[m.CleanName] == _selectedFolder).ToList();

            string q = _modSearchFilter.Trim();
            List<ModItem> filteredMods = folderMods;
            if (q.Length > 0)
            {
                var groupsByName = _dllGroups.Where(g => g.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(g => g.Name).ToHashSet();
                filteredMods = folderMods.Where(m => ModMatchesSearch(m, q)
                    || (!string.IsNullOrEmpty(m.DllGroupName) && groupsByName.Contains(m.DllGroupName))).ToList();
            }

            var ungrouped = filteredMods.Where(m => string.IsNullOrEmpty(m.DllGroupName))
                .OrderBy(m => m.CleanName, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var mod in ungrouped)
            {
                int rowIdx = RegisterSelectRow(mod);
                string vFolder = _modFolderMap.ContainsKey(mod.CleanName) ? _modFolderMap[mod.CleanName] : "";
                var row = new ModRowControl(false);
                row.Bind(mod, vFolder, IsModSelected(mod));
                row.RowMouseClick += (m, e) => {
                    if (e.Button != MouseButtons.Left) return;
                    HandleRightPanelSelect(m, rowIdx);
                    AfterSelectionChanged();
                };
                row.StateClick += m => {
                    OnModStateButtonClick(m);
                    RenderMods();
                    RenderFolders();
                    UpdateControlPanel();
                };
                WireSelectTooltip(row);
                _modListPanel.Controls.Add(row);
            }

            foreach (var groupInfo in _dllGroups.ToList())
            {
                string gName = groupInfo.Name;
                var groupMods = GetOrderedGroupMods(gName, filteredMods);
                if (groupMods.Count == 0) continue;

                bool anyGroupEnabled = groupMods.Any(m => !m.IsDisabled);
                var capturedGroup = groupInfo;
                string grpFolder = _modFolderMap.ContainsKey(groupMods[0].CleanName) ? _modFolderMap[groupMods[0].CleanName] : "";

                int groupRowIdx = RegisterSelectRow(capturedGroup);
                var groupRow = new GroupRowControl();
                groupRow.Bind(capturedGroup, groupMods, grpFolder, IsGroupHeaderSelected(capturedGroup));
                groupRow.RowMouseClick += (g, e) => {
                    if (e.Button != MouseButtons.Left) return;
                    HandleRightPanelSelect(g, groupRowIdx);
                    AfterSelectionChanged();
                };
                groupRow.ExpandClick += () => {
                    capturedGroup.IsExpanded = !capturedGroup.IsExpanded;
                    RenderMods();
                };
                groupRow.StateClick += () => {
                    bool targetDisable = anyGroupEnabled;
                    foreach (var m in groupMods) ToggleSingleModSilently(m, targetDisable);
                    RenderMods();
                    RenderFolders();
                };
                groupRow.TitleDoubleClick += () => {
                    var lblTitle = groupRow.Controls.OfType<Label>().FirstOrDefault(l => l.Text.StartsWith("[📦]"));
                    if (lblTitle == null) return;

                    string oldName = capturedGroup.Name;
                    lblTitle.Visible = false;

                    var txtEdit = new TextBox {
                        Text = oldName,
                        Font = new Font("Consolas", 10f, FontStyle.Bold),
                        ForeColor = Color.FromArgb(214, 208, 188),
                        BackColor = Color.FromArgb(13, 17, 13),
                        Location = new Point(58, 12),
                        Size = new Size(270, 20),
                        BorderStyle = BorderStyle.FixedSingle
                    };
                    txtEdit.KeyDown += (ks, ke) => {
                        if (ke.KeyCode == Keys.Enter)
                        {
                            string newName = txtEdit.Text.Trim();
                            if (string.IsNullOrWhiteSpace(newName) || newName.Length > 100)
                            {
                                MessageBox.Show(Lang.IsUkr ? "Назва групи не може бути порожньою і має бути не більше 100 символів." : "Group name cannot be empty and must be no more than 100 characters.", Lang.IsUkr ? "Помилка" : "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                txtEdit.Text = oldName;
                                return;
                            }
                            if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                            {
                                MessageBox.Show(Lang.IsUkr ? "Назва групи містить некоректні символи." : "Group name contains invalid characters.", Lang.IsUkr ? "Помилка" : "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                txtEdit.Text = oldName;
                                return;
                            }
                            if (_dllGroups.Any(g => g.Name == newName) && newName != oldName)
                            {
                                MessageBox.Show(Lang.IsUkr ? "Група з такою назвою вже існує." : "Group with this name already exists.", Lang.IsUkr ? "Помилка" : "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                txtEdit.Text = oldName;
                                return;
                            }
                            try
                            {
                                capturedGroup.Name = newName;
                                foreach (var kvp in _dllToGroupMap.Where(kvp => kvp.Value == oldName).ToList())
                                    _dllToGroupMap[kvp.Key] = newName;
                                if (_dllGroupMemberOrder.ContainsKey(oldName))
                                {
                                    _dllGroupMemberOrder[newName] = _dllGroupMemberOrder[oldName];
                                    _dllGroupMemberOrder.Remove(oldName);
                                }
                                SaveVirtualConfig();
                                ReloadMods();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.Message, Lang.IsUkr ? "Помилка" : "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                txtEdit.Text = oldName;
                                return;
                            }
                        }
                        else if (ke.KeyCode == Keys.Escape)
                        {
                            groupRow.Controls.Remove(txtEdit);
                            txtEdit.Dispose();
                            lblTitle.Visible = true;
                        }
                    };
                    txtEdit.LostFocus += (ls, le) => {
                        if (txtEdit.IsDisposed) return;
                        groupRow.Controls.Remove(txtEdit);
                        txtEdit.Dispose();
                        lblTitle.Visible = true;
                    };
                    groupRow.Controls.Add(txtEdit);
                    txtEdit.Focus();
                    txtEdit.SelectAll();
                };
                WireSelectTooltip(groupRow);
                _modListPanel.Controls.Add(groupRow);

                if (!groupInfo.IsExpanded) continue;

                for (int si = 0; si < groupMods.Count; si++)
                {
                    var subMod = groupMods[si];
                    int memberRowIdx = RegisterSelectRow(subMod);
                    var childRow = new ModRowControl(true);
                    childRow.Bind(subMod, "", IsModSelected(subMod));
                    childRow.RowMouseClick += (m, e) => {
                        if (e.Button != MouseButtons.Left) return;
                        HandleRightPanelSelect(m, memberRowIdx);
                        AfterSelectionChanged();
                    };
                    childRow.StateClick += m => {
                        OnModStateButtonClick(m);
                        RenderMods();
                        RenderFolders();
                        UpdateControlPanel();
                    };
                    WireSelectTooltip(childRow);
                    _modListPanel.Controls.Add(childRow);
                }
            }

            _modListPanel.ResumeLayout(true);
            _rightHeader.Text = _selectedFolder.ToUpper() + " (" + filteredMods.Count + ")";
            UpdateControlPanel();
        }

        public void OnModStateButtonClick(ModItem item)
        {
            try
            {
                string oldPath = item.FullPath;
                string dir = Path.GetDirectoryName(oldPath);
                string newPath;
                if (item.IsDisabled)
                {
                    string fileName = Path.GetFileName(oldPath);
                    if (fileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                        fileName = fileName.Substring(0, fileName.Length - 9);
                    newPath = Path.Combine(dir, fileName);
                    if (item.IsFolder)
                    {
                        if (Directory.Exists(oldPath)) Directory.Move(oldPath, newPath);
                    }
                    else if (File.Exists(oldPath))
                    {
                        File.Move(oldPath, newPath);
                    }
                    item.IsDisabled = false;
                    item.FullPath = newPath;
                }
                else
                {
                    newPath = Path.Combine(dir, Path.GetFileName(oldPath) + ".disabled");
                    if (item.IsFolder)
                    {
                        if (Directory.Exists(oldPath)) Directory.Move(oldPath, newPath);
                    }
                    else if (File.Exists(oldPath))
                    {
                        File.Move(oldPath, newPath);
                    }
                    item.IsDisabled = true;
                    item.FullPath = newPath;
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        public void ToggleSingleModWithWarning(ModItem mod, bool targetDisable, string groupName)
        {
            if (targetDisable) 
            {
                string warnMsg = Lang.IsUkr 
                    ? "Увага! Ви вимикаєте окремий компонент '" + mod.CleanName + "'. Через це мод [" + groupName + "] може працювати некоректно. Продовжити?"
                    : "Warning! You are disabling a component '" + mod.CleanName + "'. The mod [" + groupName + "] might function incorrectly. Continue?";
                
                string title = Lang.IsUkr ? "Попередження" : "Warning";
                if (MessageBox.Show(warnMsg, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }
            ToggleSingleModSilently(mod, targetDisable);
        }

        public void ToggleSingleModSilently(ModItem mod, bool targetDisable)
        {
            try
            {
                string oldPath = mod.FullPath;
                string newPath;
                if (!targetDisable && mod.IsDisabled)
                {
                    newPath = oldPath.Substring(0, oldPath.Length - 9);
                    if (mod.IsFolder) Directory.Move(oldPath, newPath);
                    else File.Move(oldPath, newPath);
                    mod.IsDisabled = false;
                    mod.FullPath = newPath;
                }
                else if (targetDisable && !mod.IsDisabled)
                {
                    newPath = oldPath + ".disabled";
                    if (mod.IsFolder) Directory.Move(oldPath, newPath);
                    else File.Move(oldPath, newPath);
                    mod.IsDisabled = true;
                    mod.FullPath = newPath;
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        void BtnGrpDelete_Click(object sender, EventArgs e)
        {
            var grp = GetSelectedDllGroup();
            if (grp == null) return;

            List<ModItem> toDelete = (_selectedRightItem is DllGroup)
                ? _mods.Where(m => m.DllGroupName == grp.Name).ToList()
                : _selectedMods.Where(m => m.DllGroupName == grp.Name).ToList();
            if (toDelete.Count == 0) return;

            string confirmMsg = Lang.IsUkr
                ? "Видалити обрані DLL (" + toDelete.Count + " шт.) назавжди?"
                : "Delete selected DLLs (" + toDelete.Count + " pcs) permanently?";
            if (MessageBox.Show(confirmMsg, Lang.IsUkr ? "Підтвердження" : "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                foreach (var mod in toDelete)
                {
                    if (File.Exists(mod.FullPath)) File.Delete(mod.FullPath);
                    _modFolderMap.Remove(mod.CleanName);
                    _dllToGroupMap.Remove(mod.CleanName);
                    if (_dllGroupMemberOrder.ContainsKey(grp.Name))
                        _dllGroupMemberOrder[grp.Name].Remove(mod.CleanName);
                }
                if (!_mods.Any(m => m.DllGroupName == grp.Name && !toDelete.Contains(m)))
                    _dllGroups.RemoveAll(g => g.Name == grp.Name);
                _selectedMods.Clear();
                _selectedRightItem = null;
                SaveVirtualConfig();
                ReloadMods();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        void BtnGrpUngroup_Click(object sender, EventArgs e)
        {
            var grp = GetSelectedDllGroup();
            if (grp == null) return;

            var targets = (_selectedRightItem is DllGroup)
                ? _mods.Where(m => m.DllGroupName == grp.Name).ToList()
                : _selectedMods.Where(m => m.DllGroupName == grp.Name).ToList();
            if (targets.Count == 0) return;

            foreach (var m in targets)
            {
                _dllToGroupMap.Remove(m.CleanName);
                if (_dllGroupMemberOrder.ContainsKey(grp.Name))
                    _dllGroupMemberOrder[grp.Name].Remove(m.CleanName);
            }
            if (!_dllToGroupMap.Values.Contains(grp.Name))
            {
                _dllGroups.RemoveAll(g => g.Name == grp.Name);
                _dllGroupMemberOrder.Remove(grp.Name);
            }
            _selectedMods.Clear();
            _selectedRightItem = null;
            SaveVirtualConfig();
            ReloadMods();
        }

        void MoveDllGroup(DllGroup g, int direction)
        {
            int idx = _dllGroups.IndexOf(g);
            if (idx < 0) return;
            int newIdx = idx + direction;
            if (newIdx < 0 || newIdx >= _dllGroups.Count) return;
            _dllGroups.RemoveAt(idx);
            _dllGroups.Insert(newIdx, g);
        }

        void MoveGroupMembers(string gName, int direction)
        {
            if (!_dllGroupMemberOrder.ContainsKey(gName)) return;
            var order = _dllGroupMemberOrder[gName];
            var selected = _selectedMods.Where(m => m.DllGroupName == gName).Select(m => m.CleanName).ToHashSet();
            if (selected.Count == 0) return;

            if (direction < 0)
            {
                for (int i = 1; i < order.Count; i++)
                {
                    if (selected.Contains(order[i]) && !selected.Contains(order[i - 1]))
                    {
                        string tmp = order[i - 1];
                        order[i - 1] = order[i];
                        order[i] = tmp;
                    }
                }
            }
            else
            {
                for (int i = order.Count - 2; i >= 0; i--)
                {
                    if (selected.Contains(order[i]) && !selected.Contains(order[i + 1]))
                    {
                        string tmp = order[i + 1];
                        order[i + 1] = order[i];
                        order[i] = tmp;
                    }
                }
            }
        }

        void BtnGrpUp_Click(object sender, EventArgs e)
        {
            var grp = GetSelectedDllGroup();
            if (grp == null)
            {
                if (_selectedMods.Count > 0)
                {
                    string gn = _selectedMods.First().DllGroupName;
                    if (!string.IsNullOrEmpty(gn) && _selectedMods.All(m => m.DllGroupName == gn))
                        MoveGroupMembers(gn, -1);
                }
                return;
            }
            if (_selectedMods.Count > 0)
                MoveGroupMembers(grp.Name, -1);
            else
                MoveDllGroup(grp, -1);
            SaveVirtualConfig();
            RenderMods();
        }

        void BtnGrpDown_Click(object sender, EventArgs e)
        {
            var grp = GetSelectedDllGroup();
            if (grp == null)
            {
                if (_selectedMods.Count > 0)
                {
                    string gn = _selectedMods.First().DllGroupName;
                    if (!string.IsNullOrEmpty(gn) && _selectedMods.All(m => m.DllGroupName == gn))
                        MoveGroupMembers(gn, 1);
                }
                return;
            }
            if (_selectedMods.Count > 0)
                MoveGroupMembers(grp.Name, 1);
            else
                MoveDllGroup(grp, 1);
            SaveVirtualConfig();
            RenderMods();
        }

        void BtnRenameFolder_Click(object sender, EventArgs e)
        {
            if (_selectedFolder == _virtualFolders[0]) return;

            string oldName = _selectedFolder;
            string prompt = Lang.IsUkr ? "Нова назва папки:" : "New folder name:";
            using (var dlg = new InputDialog(prompt))
            {
                if (dlg.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.Result))
                {
                    string newName = dlg.Result.Trim();
                    if (string.IsNullOrWhiteSpace(newName) || newName.Length > 100)
                    {
                        MessageBox.Show(Lang.IsUkr ? "Назва папки не може бути порожньою і має бути не більше 100 символів." : "Folder name cannot be empty and must be no more than 100 characters.", Lang.IsUkr ? "Помилка" : "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    {
                        MessageBox.Show(Lang.IsUkr ? "Назва папки містить некоректні символи." : "Folder name contains invalid characters.", Lang.IsUkr ? "Помилка" : "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    if (_virtualFolders.Contains(newName) && newName != oldName)
                    {
                        MessageBox.Show(Lang.IsUkr ? "Папка з такою назвою вже існує." : "Folder with this name already exists.", Lang.IsUkr ? "Помилка" : "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    try
                    {
                        var keys = _modFolderMap.Where(kvp => kvp.Value == oldName).Select(kvp => kvp.Key).ToList();
                        foreach (var k in keys) _modFolderMap[k] = newName;
                        int idx = _virtualFolders.IndexOf(oldName);
                        _virtualFolders[idx] = newName;
                        if (_selectedFolder == oldName) _selectedFolder = newName;
                        SaveVirtualConfig();
                        ReloadMods();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Lang.IsUkr ? "Помилка" : "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        void BtnMoveAndDeleteFolder_Click(object sender, EventArgs e)
        {
            if (_selectedFolder == _virtualFolders[0] || _virtualFolders.Count <= 1) return;

            var menu = new ContextMenuStrip();
            menu.BackColor = Color.FromArgb(21, 27, 19);
            menu.ForeColor = Color.FromArgb(214, 208, 188);
            menu.RenderMode = ToolStripRenderMode.System;

            var header = menu.Items.Add(Lang.IsUkr ? "Перенести в папку:" : "Move to folder:");
            header.Enabled = false;

            foreach (var folder in _virtualFolders)
            {
                if (folder == _selectedFolder) continue;
                if (folder == _virtualFolders[0]) continue;

                var item = menu.Items.Add(folder);
                item.Click += (s, ev) => {
                    try
                    {
                        var folderMods = _mods.Where(m => _modFolderMap.ContainsKey(m.CleanName) && _modFolderMap[m.CleanName] == _selectedFolder).ToList();
                        foreach (var m in folderMods)
                        {
                            _modFolderMap[m.CleanName] = folder;
                        }

                        string deleteConfirmMsg = Lang.IsUkr ? "Видалити порожню папку \"" + _selectedFolder + "\"?" : "Delete empty folder \"" + _selectedFolder + "\"?";
                        string deleteConfirmTitle = Lang.IsUkr ? "Підтвердження" : "Confirm";

                        if (MessageBox.Show(deleteConfirmMsg, deleteConfirmTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            _virtualFolders.Remove(_selectedFolder);
                            _selectedFolder = _virtualFolders[0];
                        }

                        SaveVirtualConfig();
                        ReloadMods();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Lang.IsUkr ? "Помилка" : "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };
            }

            menu.Show(_btnMoveAndDeleteFolder, new Point(0, _btnMoveAndDeleteFolder.Height));
        }

        void UpdateControlPanel()
        {
            bool groupCtx = IsGroupContextActive();
            _btnGrpDelete.Visible = groupCtx;
            _btnGrpUngroup.Visible = groupCtx;
            _btnGrpAssignFolder.Visible = groupCtx;
            _btnGrpUp.Visible = groupCtx;
            _btnGrpDown.Visible = groupCtx;

            bool hasSelection = _selectedMods.Count > 0;
            bool folderSelected = _selectedFolder != _virtualFolders[0];

            _btnMassToggle.Visible = hasSelection && !groupCtx && !folderSelected;
            _btnMassDelete.Visible = hasSelection && !groupCtx && !folderSelected;
            _btnAssignFolder.Visible = hasSelection && !groupCtx && !folderSelected;

            bool hasOnlyDllSelected = hasSelection && _selectedMods.All(m => !m.IsFolder);
            _btnManageDllGroups.Visible = hasOnlyDllSelected && !groupCtx && !folderSelected;

            _btnRenameFolder.Visible = folderSelected && !hasSelection && !groupCtx;
            _btnMoveAndDeleteFolder.Visible = folderSelected && !hasSelection && !groupCtx && _virtualFolders.Count > 1;

            if (hasSelection)
            {
                _btnMassToggle.Text = Lang.IsUkr ? "УВІМК/ВИМК (" + _selectedMods.Count + ")" : "ENABLE/DISABLE (" + _selectedMods.Count + ")";
            }

            _statusBar.Text = Lang.IsUkr
                ? "Усього: " + _mods.Count + " | Виділено: " + _selectedMods.Count
                : "Total: " + _mods.Count + " | Selected: " + _selectedMods.Count;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Space && !IsTextInputFocused())
            {
                ToggleFocusedSelection();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void UpdateLocalization()
        {
            _leftHeader.Text = Lang.IsUkr ? "ВІРТУАЛЬНІ ПАПКИ" : "VIRTUAL FOLDERS";
            _btnNewFolder.Text = Lang.IsUkr ? "+ НОВА ПАПКА" : "+ NEW FOLDER";
            _btnRefresh.Text = Lang.IsUkr ? "↺ ОНОВИТИ" : "↺ REFRESH";
            _btnOpenFolder.Text = Lang.IsUkr ? "📁 ПРОВІДНИК PLUGINS" : "📁 OPEN PLUGINS";
            _btnPlay.Text = Lang.IsUkr ? "▶  ЗАПУСТИТИ ГРУ" : "▶  PLAY GAME";
            _btnPlayNoMods.Text = Lang.IsUkr ? "БЕЗ МОДІВ" : "NO MODS";
            if (_txtModSearch != null)
                _txtModSearch.PlaceholderText = Lang.IsUkr ? "Пошук мода..." : "Search mods...";
            _btnAssignFolder.Text = Lang.IsUkr ? "📁 ПРИВ'ЯЗАТИ ДО ПАПКИ" : "📁 ASSIGN TO FOLDER";
            _btnMassDelete.Text = Lang.IsUkr ? "ВИДАЛИТИ" : "DELETE";
            _btnLang.Text = Lang.IsUkr ? "Мова: UA" : "Lang: EN";
            _btnManageDllGroups.Text = Lang.IsUkr ? "ГРУПУВАННЯ DLL" : "DLL GROUPS";
            _btnGrpDelete.Text = Lang.IsUkr ? "ВИДАЛИТИ" : "DELETE";
            _btnGrpUngroup.Text = Lang.IsUkr ? "РОЗФОРМУВАТИ" : "UNGROUP";
            _btnGrpAssignFolder.Text = "📁";
            _btnGrpUp.Text = "▲";
            _btnGrpDown.Text = "▼";
            _btnRenameFolder.Text = Lang.IsUkr ? "ПЕРЕЙМЕНУВАТИ" : "RENAME";
            _btnMoveAndDeleteFolder.Text = Lang.IsUkr ? "ПЕРЕНЕСТИ ВСІ ФАЙЛИ" : "MOVE ALL FILES";
            _toolTip.SetToolTip(_btnGrpAssignFolder, Lang.IsUkr ? "Прив'язати об'єднані DLL до віртуальної папки" : "Assign grouped DLLs to a virtual folder");
            _toolTip.SetToolTip(_btnGrpUp, Lang.IsUkr ? "Підняти вище (групу або файли в групі)" : "Move up (group or files in group)");
            _toolTip.SetToolTip(_btnGrpDown, Lang.IsUkr ? "Опустити нижче (групу або файли в групі)" : "Move down (group or files in group)");
            _toolTip.SetToolTip(_btnRenameFolder, Lang.IsUkr ? "Перейменувати віртуальну папку" : "Rename virtual folder");
            _toolTip.SetToolTip(_btnMoveAndDeleteFolder, Lang.IsUkr ? "Перенести файли в іншу папку" : "Move files to another folder");

            ReloadMods();
        }

        void BtnMassToggle_Click(object sender, EventArgs e)
        {
            foreach (var mod in _selectedMods) ToggleSingleModSilently(mod, !mod.IsDisabled);
            ReloadMods();
        }

        void BtnMassDelete_Click(object sender, EventArgs e)
        {
            string confirmMsg = Lang.IsUkr ? "Видалити обрані моди (" + _selectedMods.Count + " шт.) назавжди?" : "Delete selected mods (" + _selectedMods.Count + " pcs) permanently?";
            string confirmTitle = Lang.IsUkr ? "Підтвердження" : "Confirm";
            
            if (MessageBox.Show(confirmMsg, confirmTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            
            try
            {
                foreach (var mod in _selectedMods)
                {
                    if (mod.IsFolder && Directory.Exists(mod.FullPath)) Directory.Delete(mod.FullPath, true);
                    else if (!mod.IsFolder && File.Exists(mod.FullPath)) File.Delete(mod.FullPath);
                    _modFolderMap.Remove(mod.CleanName);
                    _dllToGroupMap.Remove(mod.CleanName);
                }
                SaveVirtualConfig();
                ReloadMods();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        void ApplyFolderAssignment(IEnumerable<ModItem> targets, string targetFolder)
        {
            foreach (var m in targets)
            {
                if (!string.IsNullOrEmpty(m.DllGroupName))
                {
                    foreach (var sib in _mods.Where(x => x.DllGroupName == m.DllGroupName))
                    {
                        if (targetFolder == _virtualFolders[0]) _modFolderMap.Remove(sib.CleanName);
                        else _modFolderMap[sib.CleanName] = targetFolder;
                    }
                }
                else
                {
                    if (targetFolder == _virtualFolders[0]) _modFolderMap.Remove(m.CleanName);
                    else _modFolderMap[m.CleanName] = targetFolder;
                }
            }
        }

        void ShowAssignFolderMenu(Control anchor, List<ModItem> targets)
        {
            if (targets == null || targets.Count == 0) return;
            var menu = new ContextMenuStrip();
            foreach (var f in _virtualFolders)
            {
                string targetFolder = f;
                string displayName = (targetFolder == _virtualFolders[0]) ? (Lang.IsUkr ? "[ОЧИСТИТИ ПРИВ'ЯЗКУ]" : "[CLEAR ASSIGNMENT]") : targetFolder;
                var item = menu.Items.Add(displayName);
                item.Click += (s, ev) => {
                    ApplyFolderAssignment(targets, targetFolder);
                    SaveVirtualConfig();
                    ReloadMods();
                };
            }
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        void BtnAssignFolder_Click(object sender, EventArgs e)
        {
            ShowAssignFolderMenu(_btnAssignFolder, _selectedMods.ToList());
        }

        void BtnGrpAssignFolder_Click(object sender, EventArgs e)
        {
            ShowAssignFolderMenu(_btnGrpAssignFolder, GetGroupActionTargets());
        }

        void BtnNewFolder_Click(object sender, EventArgs e)
        {
            string prompt = Lang.IsUkr ? "Введіть назву віртуальної папки:" : "Enter virtual folder name:";
            using (var dlg = new InputDialog(prompt))
            {
                if (dlg.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.Result))
                {
                    string name = dlg.Result.Trim();
                    if (!_virtualFolders.Contains(name))
                    {
                        _virtualFolders.Add(name);
                        _selectedFolder = name;
                        SaveVirtualConfig();
                        ReloadMods();
                    }
                }
            }
        }

        void BtnManageDllGroups_Click(object sender, EventArgs e)
        {
            if (_selectedMods.Count == 0 || _selectedMods.Any(m => m.IsFolder)) return;

            var dllMods = _selectedMods.Where(m => !m.IsFolder).ToList();
            if (dllMods.Count == 0) return;

            bool anyInGroup = dllMods.Any(m => !string.IsNullOrEmpty(m.DllGroupName));
            bool anyNotInGroup = dllMods.Any(m => string.IsNullOrEmpty(m.DllGroupName));

            var menu = new ContextMenuStrip();
            menu.BackColor = Color.FromArgb(21, 27, 19);
            menu.ForeColor = Color.FromArgb(214, 208, 188);
            menu.RenderMode = ToolStripRenderMode.System;

            if (anyNotInGroup)
            {
                if (_dllGroups.Count > 0)
                {
                    var addHeader = menu.Items.Add(Lang.IsUkr ? "Додати до групи:" : "Add to group:");
                    addHeader.Enabled = false;
                    foreach (var grp in _dllGroups)
                    {
                        var item = menu.Items.Add(grp.Name);
                        item.Click += (s, ev) => {
                            var toAdd = dllMods.Where(m => string.IsNullOrEmpty(m.DllGroupName)).ToList();
                            foreach (var m in toAdd)
                            {
                                _dllToGroupMap[m.CleanName] = grp.Name;
                                m.DllGroupName = grp.Name;
                            }
                            var memberNames = _dllToGroupMap.Where(kvp => kvp.Value == grp.Name).Select(kvp => kvp.Key);
                            EnsureMemberOrder(grp.Name, memberNames);
                            SaveVirtualConfig();
                            ReloadMods();
                        };
                    }
                }

                var createItem = menu.Items.Add(Lang.IsUkr ? "+ Створити нову DLL-групу" : "+ Create new DLL group");
                createItem.Click += (s, ev) => {
                    string prompt = Lang.IsUkr ? "Назва нової групи:" : "Name for the new group:";
                    using (var dlg = new InputDialog(prompt))
                    {
                        if (dlg.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.Result))
                        {
                            string gName = dlg.Result.Trim();
                            if (!_dllGroups.Any(g => g.Name == gName)) _dllGroups.Add(new DllGroup { Name = gName });

                            var toAdd = dllMods.Where(m => string.IsNullOrEmpty(m.DllGroupName)).ToList();
                            foreach (var m in toAdd)
                            {
                                _dllToGroupMap[m.CleanName] = gName;
                                m.DllGroupName = gName;
                            }
                            var memberNames = _dllToGroupMap.Where(kvp => kvp.Value == gName).Select(kvp => kvp.Key);
                            EnsureMemberOrder(gName, memberNames);
                            SaveVirtualConfig();
                            ReloadMods();
                        }
                    }
                };
            }

            if (anyInGroup)
            {
                if (anyNotInGroup) menu.Items.Add(new ToolStripSeparator());

                var removeItem = menu.Items.Add(Lang.IsUkr ? "❌ Видалити з групи" : "❌ Remove from group");
                removeItem.Click += (s, ev) => {
                    var toRemove = dllMods.Where(m => !string.IsNullOrEmpty(m.DllGroupName)).ToList();
                    foreach (var m in toRemove)
                    {
                        string groupName = m.DllGroupName;
                        _dllToGroupMap.Remove(m.CleanName);
                        m.DllGroupName = "";
                        if (_dllGroupMemberOrder.ContainsKey(groupName))
                            _dllGroupMemberOrder[groupName].Remove(m.CleanName);
                    }
                    SaveVirtualConfig();
                    ReloadMods();
                };

                if (_dllGroups.Count > 1)
                {
                    var moveHeader = menu.Items.Add(Lang.IsUkr ? "Перенести в іншу групу:" : "Move to another group:");
                    moveHeader.Enabled = false;
                    foreach (var grp in _dllGroups)
                    {
                        var item = menu.Items.Add(grp.Name);
                        var targetGroup = grp.Name;
                        item.Click += (s, ev) => {
                            var toMove = dllMods.Where(m => !string.IsNullOrEmpty(m.DllGroupName) && m.DllGroupName != targetGroup).ToList();
                            foreach (var m in toMove)
                            {
                                string oldGroup = m.DllGroupName;
                                _dllToGroupMap[m.CleanName] = targetGroup;
                                m.DllGroupName = targetGroup;
                                if (_dllGroupMemberOrder.ContainsKey(oldGroup))
                                    _dllGroupMemberOrder[oldGroup].Remove(m.CleanName);
                            }
                            var memberNames = _dllToGroupMap.Where(kvp => kvp.Value == targetGroup).Select(kvp => kvp.Key);
                            EnsureMemberOrder(targetGroup, memberNames);
                            SaveVirtualConfig();
                            ReloadMods();
                        };
                    }
                }

                var createNewItem = menu.Items.Add(Lang.IsUkr ? "+ Створити нову DLL-групу" : "+ Create new DLL group");
                createNewItem.Click += (s, ev) => {
                    string prompt = Lang.IsUkr ? "Назва нової групи:" : "Name for the new group:";
                    using (var dlg = new InputDialog(prompt))
                    {
                        if (dlg.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.Result))
                        {
                            string gName = dlg.Result.Trim();
                            if (!_dllGroups.Any(g => g.Name == gName)) _dllGroups.Add(new DllGroup { Name = gName });

                            var toMove = dllMods.Where(m => !string.IsNullOrEmpty(m.DllGroupName)).ToList();
                            foreach (var m in toMove)
                            {
                                string oldGroup = m.DllGroupName;
                                _dllToGroupMap[m.CleanName] = gName;
                                m.DllGroupName = gName;
                                if (_dllGroupMemberOrder.ContainsKey(oldGroup))
                                    _dllGroupMemberOrder[oldGroup].Remove(m.CleanName);
                            }
                            var memberNames = _dllToGroupMap.Where(kvp => kvp.Value == gName).Select(kvp => kvp.Key);
                            EnsureMemberOrder(gName, memberNames);
                            SaveVirtualConfig();
                            ReloadMods();
                        }
                    }
                };
            }

            menu.Show(_btnManageDllGroups, new Point(0, _btnManageDllGroups.Height));
        }

        void BuildUI()
        {
            _toolTip = new ToolTip { AutoPopDelay = 10000, InitialDelay = 350, ReshowDelay = 150, ShowAlways = true };

            Size = new Size(880, 640);
            MinimumSize = new Size(880, 640);
            Text = "VespicMM";
            BackColor = C_BG; ForeColor = C_TEXT;
            Font = UiTheme.FontUi(9f);
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            _logoHeader = new Label {
                Text = "Vespic Mod Manager",
                Font = UiTheme.FontUi(17f, FontStyle.Bold),
                ForeColor = C_AMBER,
                Location = new Point(12, 10),
                Size = new Size(500, 35), 
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(_logoHeader);

            _btnManageDllGroups = new Button {
                Location = new Point(660, 12),
                Size = new Size(172, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(180, 25, 45, 25),
                ForeColor = Color.LightSteelBlue,
                Font = new Font("Consolas", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Visible = false
            };
            _btnManageDllGroups.FlatAppearance.BorderColor = C_BORDER;
            _btnManageDllGroups.Click += BtnManageDllGroups_Click;
            Controls.Add(_btnManageDllGroups);

            _leftHeader = new Label { Font = UiTheme.FontUi(9.5f, FontStyle.Bold), ForeColor = C_AMBER, Location = new Point(12, 52), Size = new Size(256, 36), TextAlign = ContentAlignment.MiddleCenter, BackColor = C_PANEL2 };
            _folderList = new CustomListPanel { Location = new Point(12, 88), Size = new Size(256, 400), AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent };
            Controls.Add(_leftHeader); Controls.Add(_folderList);

            _rightHeader = new Label { Font = UiTheme.FontUi(9.5f, FontStyle.Bold), ForeColor = C_AMBER, Location = new Point(282, 52), Size = new Size(570, 36), TextAlign = ContentAlignment.MiddleCenter, BackColor = C_PANEL2 };
            Controls.Add(_rightHeader);

            _txtModSearch = new TextBox {
                Location = new Point(282, 88),
                Size = new Size(570, 26),
                BackColor = UiTheme.InputBg,
                ForeColor = C_TEXT,
                BorderStyle = BorderStyle.FixedSingle,
                Font = UiTheme.FontUi(9.5f)
            };
            _txtModSearch.TextChanged += (s, e) => {
                _modSearchFilter = _txtModSearch.Text;
                RenderMods();
            };
            if (_toolTip != null)
                _toolTip.SetToolTip(_txtModSearch, Lang.IsUkr ? "Фільтр за назвою мода, папки [DIR] або групи [📦]" : "Filter by mod name, [DIR] folder or [📦] group");
            _modListPanel = new CustomListPanel { Location = new Point(282, 118), Size = new Size(570, 370), AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent };
            Controls.Add(_txtModSearch); Controls.Add(_modListPanel);
            KeyPreview = true;

            _btnGrpDelete = new Button { Location = new Point(282, 492), Size = new Size(88, 32), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(180, 45, 20, 20), ForeColor = Color.Tomato, Visible = false, Cursor = Cursors.Hand, Font = new Font("Consolas", 8.5f, FontStyle.Bold) };
            _btnGrpUngroup = new Button { Location = new Point(374, 492), Size = new Size(108, 32), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(180, 30, 45, 60), ForeColor = Color.LightBlue, Visible = false, Cursor = Cursors.Hand, Font = new Font("Consolas", 8.5f, FontStyle.Bold) };
            _btnGrpAssignFolder = new Button { Location = new Point(486, 492), Size = new Size(40, 32), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(180, 25, 33, 23), ForeColor = C_AMBER, Visible = false, Cursor = Cursors.Hand, Font = new Font("Consolas", 11f, FontStyle.Bold) };
            _btnGrpUp = new Button { Location = new Point(530, 492), Size = new Size(34, 32), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(160, 20, 26, 18), ForeColor = C_AMBER, Visible = false, Cursor = Cursors.Hand, Font = new Font("Consolas", 12f, FontStyle.Bold) };
            _btnGrpDown = new Button { Location = new Point(568, 492), Size = new Size(34, 32), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(160, 20, 26, 18), ForeColor = C_AMBER, Visible = false, Cursor = Cursors.Hand, Font = new Font("Consolas", 12f, FontStyle.Bold) };
            _btnGrpDelete.FlatAppearance.BorderColor = C_BORDER;
            _btnGrpUngroup.FlatAppearance.BorderColor = C_BORDER;
            _btnGrpAssignFolder.FlatAppearance.BorderColor = C_BORDER;
            _btnGrpUp.FlatAppearance.BorderColor = C_BORDER;
            _btnGrpDown.FlatAppearance.BorderColor = C_BORDER;
            _btnGrpDelete.Click += BtnGrpDelete_Click;
            _btnGrpUngroup.Click += BtnGrpUngroup_Click;
            _btnGrpAssignFolder.Click += BtnGrpAssignFolder_Click;
            _btnGrpUp.Click += BtnGrpUp_Click;
            _btnGrpDown.Click += BtnGrpDown_Click;
            Controls.Add(_btnGrpDelete); Controls.Add(_btnGrpUngroup); Controls.Add(_btnGrpAssignFolder); Controls.Add(_btnGrpUp); Controls.Add(_btnGrpDown);

            _btnMassToggle = new Button { Location = new Point(282, 492), Size = new Size(150, 32), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(180, 30, 45, 60), ForeColor = Color.LightBlue, Visible = false, Cursor = Cursors.Hand };
            _btnAssignFolder = new Button { Location = new Point(438, 492), Size = new Size(190, 32), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(180, 25, 33, 23), ForeColor = C_AMBER, Visible = false, Cursor = Cursors.Hand };
            _btnMassDelete = new Button { Location = new Point(634, 492), Size = new Size(96, 32), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(180, 45, 20, 20), ForeColor = Color.Tomato, Visible = false, Cursor = Cursors.Hand };
            _btnMassToggle.FlatAppearance.BorderColor = C_BORDER;
            _btnAssignFolder.FlatAppearance.BorderColor = C_BORDER;
            _btnMassDelete.FlatAppearance.BorderColor = C_BORDER;
            
            _btnMassToggle.Click += BtnMassToggle_Click;
            _btnMassDelete.Click += BtnMassDelete_Click;
            _btnAssignFolder.Click += BtnAssignFolder_Click;
            Controls.Add(_btnMassToggle); Controls.Add(_btnAssignFolder); Controls.Add(_btnMassDelete);
            _toolTip.SetToolTip(_btnAssignFolder, Lang.IsUkr ? "Прив'язати виділені моди до віртуальної папки" : "Assign selected mods to a virtual folder");

            _btnNewFolder = new Button { Location = new Point(12, 496), Size = new Size(130, 32), ForeColor = C_AMBER, Text = "+ FOLDER" };
            _btnRefresh = new Button { Location = new Point(148, 496), Size = new Size(120, 32), ForeColor = C_TEXT, Text = "↺ REFRESH" };
            _btnOpenFolder = new Button { Location = new Point(12, 536), Size = new Size(266, 34), ForeColor = C_TEXT };
            UiTheme.StyleFlatButton(_btnNewFolder, UiTheme.BtnNeutral, C_AMBER);
            UiTheme.StyleFlatButton(_btnRefresh, UiTheme.BtnNeutral, C_TEXT);
            UiTheme.StyleFlatButton(_btnOpenFolder, UiTheme.BtnNeutral, C_TEXT);

            _btnRenameFolder = new Button { Location = new Point(282, 492), Size = new Size(150, 32), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(180, 30, 45, 60), ForeColor = Color.LightBlue, Visible = false, Cursor = Cursors.Hand, Font = new Font("Consolas", 8.5f, FontStyle.Bold) };
            _btnMoveAndDeleteFolder = new Button { Location = new Point(438, 492), Size = new Size(190, 32), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(180, 45, 20, 20), ForeColor = Color.Tomato, Visible = false, Cursor = Cursors.Hand, Font = new Font("Consolas", 8.5f, FontStyle.Bold) };
            _btnRenameFolder.FlatAppearance.BorderColor = C_BORDER;
            _btnMoveAndDeleteFolder.FlatAppearance.BorderColor = C_BORDER;
            _btnRenameFolder.Click += BtnRenameFolder_Click;
            _btnMoveAndDeleteFolder.Click += BtnMoveAndDeleteFolder_Click;
            Controls.Add(_btnRenameFolder); Controls.Add(_btnMoveAndDeleteFolder);

            _btnPlay = new Button { Location = new Point(282, 536), Size = new Size(410, 34), ForeColor = Color.White, Font = UiTheme.FontUi(10f, FontStyle.Bold), Text = "▶  PLAY" };
            UiTheme.StyleFlatButton(_btnPlay, UiTheme.Play, Color.White);
            _btnPlayNoMods = new Button { Location = new Point(698, 536), Size = new Size(154, 34), Font = UiTheme.FontUi(9f, FontStyle.Bold) };
            UiTheme.StyleFlatButton(_btnPlayNoMods, UiTheme.BtnNeutral, C_AMBER);
            _btnLang = new Button { Location = new Point(758, 496), Size = new Size(94, 32), ForeColor = C_DIM };
            UiTheme.StyleFlatButton(_btnLang, UiTheme.BtnNeutral, C_DIM);
            _btnLang.Click += (s, e) => {
                Lang.IsUkr = !Lang.IsUkr;
                SaveVirtualConfig();
                UpdateLocalization();
            };
            Controls.Add(_btnLang);

            _btnNewFolder.Click += BtnNewFolder_Click;
            _btnRefresh.Click += (s, e) => ReloadMods(true);
            _btnOpenFolder.Click += (s, e) => { if (Directory.Exists(_pluginsDir)) Process.Start("explorer.exe", _pluginsDir); };
            _btnPlay.Click += (s, e) => {
                if (string.IsNullOrEmpty(_gameExe) || !File.Exists(_gameExe)) return;
                try { Process.Start(new ProcessStartInfo { FileName = _gameExe, WorkingDirectory = Path.GetDirectoryName(_gameExe) }); Application.Exit(); } catch (Exception ex) { MessageBox.Show(ex.Message); }
            };
            _btnPlayNoMods.Click += (s, e) => LaunchGameWithoutMods();
            if (_toolTip != null)
                _toolTip.SetToolTip(_btnPlayNoMods, Lang.IsUkr
                    ? "Запустити гру без модів (тимчасово вимкне всі моди; після закриття гри увімкне знову)"
                    : "Launch without mods (temporarily disables all mods; re-enables after game exits)");

            Controls.Add(_btnNewFolder); Controls.Add(_btnRefresh); Controls.Add(_btnOpenFolder); Controls.Add(_btnPlay); Controls.Add(_btnPlayNoMods);

            _statusBar = new Label { Font = UiTheme.FontUi(8.5f), ForeColor = C_DIM, Location = new Point(12, 582), Size = new Size(840, 20), BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft };
            Controls.Add(_statusBar);
        }
    }

    public class GroupContainerForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private readonly MainForm _main;
        private readonly string _groupName;
        private CustomListPanel _listPanel;
        private Label _lblHeader;

        public GroupContainerForm(MainForm main, string groupName)
        {
            _main = main;
            _groupName = groupName;

            int useDark = 1;
            DwmSetWindowAttribute(this.Handle, 20, ref useDark, sizeof(int));
            try { this.Icon = main.Icon; } catch { }

            Text = (Lang.IsUkr ? "Контейнер Мода: " : "Mod Container: ") + groupName.ToUpper();
            Size = new Size(500, 430);
            BackColor = MainForm.C_BG;
            ForeColor = MainForm.C_TEXT;
            Font = new Font("Consolas", 10f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            BuildUI();
            RenderGroupFiles();
        }

        void BuildUI()
        {
            _lblHeader = new Label { Text = (Lang.IsUkr ? "КОМПОНЕНТИ ГРУПИ: " : "GROUP COMPONENTS: ") + _groupName.ToUpper(), Font = new Font("Consolas", 10f, FontStyle.Bold), ForeColor = MainForm.C_AMBER, Location = new Point(12, 12), Size = new Size(460, 25), TextAlign = ContentAlignment.MiddleLeft };
            Controls.Add(_lblHeader);

            _listPanel = new CustomListPanel { Location = new Point(12, 45), Size = new Size(460, 280), AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            Controls.Add(_listPanel);

            var btnClose = new Button { Text = Lang.IsUkr ? "ЗАКРИТИ" : "CLOSE", Location = new Point(352, 340), Size = new Size(120, 32), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(160, 20, 26, 18), ForeColor = MainForm.C_TEXT, Cursor = Cursors.Hand };
            btnClose.FlatAppearance.BorderColor = MainForm.C_BORDER;
            btnClose.Click += (s, e) => this.Close();
            Controls.Add(btnClose);

            var lblHint = new Label { Text = Lang.IsUkr ? "* ПКМ на назві файлу — перенести в іншу групу" : "* RMB on filename — transfer component to another group", Font = new Font("Consolas", 8f, FontStyle.Italic), ForeColor = MainForm.C_DIM, Location = new Point(12, 348), Size = new Size(330, 20) };
            Controls.Add(lblHint);
        }

        void RenderGroupFiles()
        {
            _listPanel.Controls.Clear();
            _listPanel.SuspendLayout();

            var groupMods = _main._mods.Where(m => m.DllGroupName == _groupName).ToList();

            foreach (var mod in groupMods)
            {
                var p = new Panel { Size = new Size(424, 38), BackColor = MainForm.C_ITEM_BG, Margin = new Padding(6, 4, 0, 0) };
                
                var btnState = new Label { Text = mod.IsDisabled ? "○" : "●", Font = new Font("Consolas", 12f, FontStyle.Bold), ForeColor = mod.IsDisabled ? MainForm.C_RED : MainForm.C_GREEN, Location = new Point(6, 8), Size = new Size(22, 22), TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };
                var cm = mod;
                btnState.Click += (s, e) => {
                    _main.OnModStateButtonClick(cm);
                    RenderGroupFiles();
                    _main.ReloadMods();
                };

                var lblName = new Label { Text = mod.CleanName, Font = new Font("Consolas", 9f, mod.IsDisabled ? FontStyle.Regular : FontStyle.Bold), ForeColor = mod.IsDisabled ? MainForm.C_DIM : MainForm.C_TEXT, Location = new Point(32, 10), Size = new Size(330, 20), AutoEllipsis = true };
                
                var btnDel = new Label { Text = "×", Font = new Font("Consolas", 14f, FontStyle.Bold), ForeColor = MainForm.C_RED, Location = new Point(394, 6), Size = new Size(22, 22), TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };
                btnDel.Click += (s, e) => {
                    if (MessageBox.Show(Lang.IsUkr ? "Видалити " + cm.CleanName + " назавжди?" : "Delete " + cm.CleanName + " permanently?", "VespicMM", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        try {
                            if (File.Exists(cm.FullPath)) File.Delete(cm.FullPath);
                            _main._modFolderMap.Remove(cm.CleanName);
                            _main._dllToGroupMap.Remove(cm.CleanName);
                            _main.SaveVirtualConfig();
                            _main.ReloadMods();
                            RenderGroupFiles();
                        } catch (Exception ex) { MessageBox.Show(ex.Message); }
                    }
                };

                p.Controls.Add(btnState); p.Controls.Add(lblName); p.Controls.Add(btnDel);

                p.MouseClick += (s, e) => { if (e.Button == MouseButtons.Right) ShowTransferMenu(p, cm); };
                lblName.MouseClick += (s, e) => { if (e.Button == MouseButtons.Right) ShowTransferMenu(lblName, cm); };

                _listPanel.Controls.Add(p);
            }

            _listPanel.ResumeLayout();
        }

        void ShowTransferMenu(Control anchor, ModItem targetMod)
        {
            var menu = new ContextMenuStrip();
            var header = menu.Items.Add(Lang.IsUkr ? "--- ПЕРЕНЕСТИ В ІНШУ ГРУПУ ---" : "--- TRANSFER TO ANOTHER GROUP ---");
            header.Enabled = false;

            foreach (var g in _main._dllGroups.Where(x => x.Name != _groupName))
            {
                var item = menu.Items.Add("[📦] " + g.Name.ToUpper());
                item.Click += (s, e) => {
                    _main._dllToGroupMap[targetMod.CleanName] = g.Name;
                    _main.SaveVirtualConfig();
                    _main.ReloadMods();
                    RenderGroupFiles();
                };
            }

            var detachItem = menu.Items.Add(Lang.IsUkr ? "❌ Вилучити з групи (зробити окремим)" : "❌ Remove from group (make standalone)");
            detachItem.Click += (s, e) => {
                _main._dllToGroupMap.Remove(targetMod.CleanName);
                _main.SaveVirtualConfig();
                _main.ReloadMods();
                RenderGroupFiles();
            };

            menu.Show(anchor, anchor.PointToClient(Cursor.Position));
        }
    }

    public class InputDialog : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private TextBox _box;
        public string Result => _box.Text;

        public InputDialog(string prompt)
        {
            int useDark = 1;
            DwmSetWindowAttribute(this.Handle, 20, ref useDark, sizeof(int));

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
            if (File.Exists(iconPath))
            {
                try { this.Icon = new Icon(iconPath); } catch { }
            }

            Text = Lang.IsUkr ? "Введення" : "Input"; 
            Size = new Size(400, 140);
            FormBorderStyle = FormBorderStyle.FixedDialog; 
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false; MinimizeBox = false;
            BackColor = Color.FromArgb(21, 27, 19); 
            ForeColor = Color.FromArgb(214, 208, 188);

            var lbl = new Label { Text = prompt, Left = 15, Top = 15, Width = 350, Height = 20, Font = new Font("Consolas", 9.5f) };
            _box = new TextBox { Left = 15, Top = 40, Width = 350, BackColor = Color.FromArgb(13, 17, 13), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 10f) };
            
            var ok = new Button { Text = "OK", Left = 180, Top = 70, Width = 80, Height = 26, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            var cancel = new Button { Text = Lang.IsUkr ? "Скасувати" : "Cancel", Left = 270, Top = 70, Width = 95, Height = 26, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            
            ok.FlatAppearance.BorderColor = Color.FromArgb(68, 58, 22);
            cancel.FlatAppearance.BorderColor = Color.FromArgb(68, 58, 22);

            this.Controls.Add(lbl);
            this.Controls.Add(_box);
            this.Controls.Add(ok);
            this.Controls.Add(cancel);

            this.AcceptButton = ok;
            this.CancelButton = cancel;
        }
    }
}