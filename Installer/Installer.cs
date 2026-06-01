using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VespicInstaller
{
    static class Pal
    {
        public static readonly Color BG     = Color.FromArgb(13, 17, 13);
        public static readonly Color PANEL  = Color.FromArgb(21, 27, 19);
        public static readonly Color PANEL2 = Color.FromArgb(17, 22, 15);
        public static readonly Color BORDER = Color.FromArgb(68, 58, 22);
        public static readonly Color AMBER  = Color.FromArgb(212, 168, 52);
        public static readonly Color GREEN  = Color.FromArgb(82, 158, 58);
        public static readonly Color RED    = Color.FromArgb(158, 48, 38);
        public static readonly Color TEXT   = Color.FromArgb(214, 208, 188);
        public static readonly Color DIM    = Color.FromArgb(102, 98, 82);
    }

    static class Lang
    {
        public static bool IsUkr = true;

        static string T(string en, string ua) => IsUkr ? ua : en;

        public static string Title         => T("VespicMM — Installer", "VespicMM — Інсталятор");
        public static string Header        => T("MODDING SYSTEM INSTALLATION", "ВСТАНОВЛЕННЯ СИСТЕМИ МОДІВ");
        public static string SubHeader     => T("Casualties Unknown Demo", "Casualties Unknown Demo");
        public static string PathLabel     => T("Target Game Folder:", "Цільова папка гри:");
        public static string Browse        => T("BROWSE...", "ОГЛЯД...");
        public static string Install       => T("INSTALL NOW", "ВСТАНОВИТИ");
        public static string Cancel        => T("CANCEL", "СКАСУВАТИ");
        public static string LogTitle      => T("Installation Log:", "Журнал встановлення:");
        
        public static string ErrSelect     => T("Please select a valid game folder!", "Будь ласка, виберіть коректну папку гри!");
        public static string ErrNoSource   => T("Source components missing! Ensure BepInEx folder and VespicMM folder are near this installer.", "Відсутні компоненти для встановлення! Переконайтеся, що папки BepInEx та VespicMM лежать поруч з інсталятором.");
        public static string ErrGameExe    => T("CasualtiesUnknown.exe not found in this folder!", "Файл CasualtiesUnknown.exe не знайдено в цій папці!");
        public static string ErrBadPath    => T("Cannot install into Windows system folders.", "Неможливо встановлювати в системні папки Windows.");
        
        public static string StatusReady   => T("Ready to install.", "Готовий до встановлення.");
        public static string StatusDirOk   => T("Valid game directory selected.", "Знайдено правилку папку гри.");
        
        public static string LogStart      => T("Starting installation...", "Початок встановлення...");
        public static string LogCopyBep    => T("Copying BepInEx core files...", "Копіювання основних файлів BepInEx...");
        public static string LogCopyLaunch => T("Extracting VespicMM to game root...", "Розпакування файлів VespicMM у корінь гри..."); // ВИПРАВЛЕНО
        public static string LogPrepareBep => T("Preparing BepInEx folder structure...", "Підготовка структури папок BepInEx...");
        public static string LogGenPlugins => T("Ensuring BepInEx directories exist...", "Перевірка папок BepInEx...");
        public static string LogShortcut   => T("Creating dynamic shortcut on Desktop...", "Створення динамічного ярлика на Робочому столі...");
        public static string LogDone       => T("SUCCESS! Installation finished completely.", "УСПІХ! Встановлення повністю завершено.");
        
        public static string SuccessBox    => T("Installation completed successfully!\nShortcut created on your Desktop.", "Встановлення успішно завершено!\nЯрлик менеджера створено на вашому Робочому столі.");
    }

    public class InstallerForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private TextBox _txtPath;
        private ListBox _lstLog;
        private Button _btnBrowse;
        private Button _btnInstall;
        private Button _btnCancel;
        private Button _btnLang;
        private Label _lblHeader;
        private Label _lblSubHeader;
        private Label _lblPathHint;
        private Label _lblLogHint;

        public InstallerForm()
        {
            int useDark = 1;
            DwmSetWindowAttribute(this.Handle, 20, ref useDark, sizeof(int));

            BuildUI();
            AutoDetectPath();
            UpdateLocalization();
        }

        void BuildUI()
        {
            Size = new Size(540, 480);
            Text = Lang.Title; BackColor = Pal.BG; ForeColor = Pal.TEXT;
            Font = new Font("Consolas", 9.5f); FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;

            _lblHeader = Lbl("", 16, 12, 400, 22, Pal.AMBER);
            _lblHeader.Font = new Font("Consolas", 13f, FontStyle.Bold);
            _lblSubHeader = Lbl("", 16, 34, 400, 18, Pal.DIM);
            _lblSubHeader.Font = new Font("Consolas", 10f, FontStyle.Italic);
            Controls.Add(_lblHeader); Controls.Add(_lblSubHeader);

            _btnLang = new Button { Left = 430, Top = 14, Width = 80, Height = 26, FlatStyle = FlatStyle.Flat, ForeColor = Pal.DIM, Cursor = Cursors.Hand };
            _btnLang.FlatAppearance.BorderColor = Pal.BORDER;
            _btnLang.Click += (s, e) => { Lang.IsUkr = !Lang.IsUkr; UpdateLocalization(); };
            Controls.Add(_btnLang);

            var pnlPath = new Panel { Left = 16, Top = 65, Width = 494, Height = 72, BackColor = Pal.PANEL };
            pnlPath.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(Pal.BORDER), 0, 0, pnlPath.Width - 1, pnlPath.Height - 1);
            
            _lblPathHint = Lbl("", 12, 10, 300, 16, Pal.TEXT);
            _txtPath = new TextBox { Left = 12, Top = 32, Width = 350, BackColor = Pal.PANEL2, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 10f) };
            _btnBrowse = MkBtn("", Pal.BORDER, AnchorStyles.None);
            _btnBrowse.Left = 372; _btnBrowse.Top = 31; _btnBrowse.Height = 22; _btnBrowse.Width = 110;
            _btnBrowse.Click += BtnBrowse_Click;

            pnlPath.Controls.Add(_lblPathHint); pnlPath.Controls.Add(_txtPath); pnlPath.Controls.Add(_btnBrowse);
            Controls.Add(pnlPath);

            _lblLogHint = Lbl("", 16, 150, 400, 16, Pal.AMBER);
            _lstLog = new ListBox { Left = 16, Top = 170, Width = 494, Height = 200, BackColor = Pal.PANEL2, ForeColor = Pal.TEXT, BorderStyle = BorderStyle.FixedSingle, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 18 };
            _lstLog.DrawItem += LstLog_DrawItem;
            Controls.Add(_lblLogHint); Controls.Add(_lstLog);

            _btnInstall = MkBtn("", Pal.GREEN, AnchorStyles.Bottom);
            _btnInstall.Left = 254; _btnInstall.Top = 392; _btnInstall.Width = 120; _btnInstall.Height = 32;
            _btnInstall.Click += BtnInstall_Click;

            _btnCancel = MkBtn("", Color.FromArgb(40, 40, 40), AnchorStyles.Bottom);
            _btnCancel.Left = 390; _btnCancel.Top = 392; _btnCancel.Width = 120; _btnCancel.Height = 32;
            _btnCancel.FlatAppearance.BorderColor = Pal.BORDER;
            _btnCancel.Click += (s, e) => Close();

            Controls.Add(_btnInstall); Controls.Add(_btnCancel);
        }

        void UpdateLocalization()
        {
            Text = Lang.Title; _lblHeader.Text = Lang.Header; _lblSubHeader.Text = Lang.SubHeader;
            _lblPathHint.Text = Lang.PathLabel; _btnBrowse.Text = Lang.Browse; _lblLogHint.Text = Lang.LogTitle;
            _btnInstall.Text = Lang.Install; _btnCancel.Text = Lang.Cancel;
            _btnLang.Text = Lang.IsUkr ? "Мова: UA" : "Lang: EN";
            Log(Lang.StatusReady, false);
        }

        void AutoDetectPath()
        {
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            if (File.Exists(Path.Combine(currentDir, "CasualtiesUnknown.exe"))) { _txtPath.Text = currentDir; return; }
            string parentDir = Path.GetDirectoryName(currentDir);
            if (!string.IsNullOrEmpty(parentDir) && File.Exists(Path.Combine(parentDir, "CasualtiesUnknown.exe"))) { _txtPath.Text = parentDir; return; }
            _txtPath.Text = @"C:\Program Files (x86)\Casualties Unknown Demo";
        }

        void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = Lang.PathLabel; fbd.SelectedPath = _txtPath.Text;
                if (fbd.ShowDialog() == DialogResult.OK) { _txtPath.Text = fbd.SelectedPath; Log(Lang.StatusDirOk, false); }
            }
        }

        async void BtnInstall_Click(object sender, EventArgs e)
        {
            if (!InstallPaths.TryResolveGameFolder(_txtPath.Text, out string targetDir, out string pathError))
            {
                Msg(pathError == InstallPaths.ErrorBlocked ? Lang.ErrBadPath : Lang.ErrSelect);
                return;
            }

            string gameExePath = Path.Combine(targetDir, "CasualtiesUnknown.exe");
            if (!File.Exists(gameExePath)) { Msg(Lang.ErrGameExe); return; }

            string baseSource = AppDomain.CurrentDomain.BaseDirectory;
            string sourceBepDir = Directory.GetDirectories(baseSource, "BepInEx_win_x64*").FirstOrDefault();
            if (sourceBepDir == null) sourceBepDir = Path.Combine(baseSource, "BepInEx_win_x64_5.4.23.5");
            
            string sourceLauncherDir = Path.Combine(baseSource, "VespicMM");

            if (!Directory.Exists(sourceBepDir) || !Directory.Exists(sourceLauncherDir)) { Msg(Lang.ErrNoSource); return; }

            _btnInstall.Enabled = false; _btnBrowse.Enabled = false; _txtPath.Enabled = false;
            _lstLog.Items.Clear();
            Log(Lang.LogStart, false);

            await Task.Run(() =>
            {
                try
                {
                    // 1. Копіюємо BepInEx
                    Invoke((Action)(() => Log(Lang.LogCopyBep, false)));
                    CopyDirectory(sourceBepDir, targetDir);

                    // 2. Копіюємо менеджер модів VespicMM
                    Invoke((Action)(() => Log(Lang.LogCopyLaunch, false)));
                    CopyDirectory(sourceLauncherDir, targetDir);

                    // 3. Структура BepInEx без запуску гри (менше хибних спрацьовувань AV)
                    Invoke((Action)(() => Log(Lang.LogPrepareBep, false)));
                    EnsureBepInExDirectories(targetDir);

                    // 4. Перевірка plugins
                    Invoke((Action)(() => Log(Lang.LogGenPlugins, false)));
                    EnsureBepInExDirectories(targetDir);

                    // 5. Ярлик для VespicMM.exe
                    Invoke((Action)(() => Log(Lang.LogShortcut, false)));
                    CreateDesktopShortcut(targetDir);

                    Invoke((Action)(() => {
                        Log(Lang.LogDone, true);
                        // ВИПРАВЛЕНО: Замінено заголовок вікна MessageBox з Vespic Installer на VespicMM Installer
                        MessageBox.Show(Lang.SuccessBox, "VespicMM Installer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Application.Exit();
                    }));
                }
                catch (Exception ex)
                {
                    Invoke((Action)(() => {
                        Log($"[ERROR]: {ex.Message}", false);
                        _btnInstall.Enabled = true; _btnBrowse.Enabled = true; _txtPath.Enabled = true;
                    }));
                }
            });
        }

        static void EnsureBepInExDirectories(string gameRoot)
        {
            string[] relativeDirs =
            {
                "BepInEx",
                Path.Combine("BepInEx", "plugins"),
                Path.Combine("BepInEx", "config"),
                Path.Combine("BepInEx", "core"),
                Path.Combine("BepInEx", "patchers"),
            };

            foreach (string relative in relativeDirs)
            {
                string path = Path.Combine(gameRoot, relative);
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            }
        }

        void CopyDirectory(string source, string target)
        {
            string sourceRoot = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string targetRoot = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            foreach (string dir in Directory.GetDirectories(sourceRoot, "*", SearchOption.AllDirectories))
            {
                string relative = dir.Substring(sourceRoot.Length + 1);
                string targetSub = Path.Combine(targetRoot, relative);
                if (!Directory.Exists(targetSub)) Directory.CreateDirectory(targetSub);
            }

            foreach (string file in Directory.GetFiles(sourceRoot, "*.*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(sourceRoot.Length + 1);
                string targetFile = Path.Combine(targetRoot, relative);
                if (targetFile.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) continue;
                File.Copy(file, targetFile, true);
            }
        }

        void CreateDesktopShortcut(string gameFolderPath)
        {
            string targetExePath = Path.Combine(gameFolderPath, "VespicMM.exe");
            if (!File.Exists(targetExePath))
                throw new FileNotFoundException("VespicMM.exe not found after installation.", targetExePath);

            string desktopPath = InstallPaths.GetDesktopDirectory();
            string shortcutLocation = Path.Combine(desktopPath, "VespicMM.lnk");

            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                throw new InvalidOperationException("WScript.Shell is not available on this system.");

            object shell = Activator.CreateInstance(shellType);
            object link = null;
            try
            {
                link = shellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { shortcutLocation });

                Type linkType = link.GetType();
                linkType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { targetExePath });
                linkType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { gameFolderPath });
                linkType.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { "Casualties Unknown Mod Manager" });
                linkType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, link, new object[] { targetExePath + ",0" });
                linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
            }
            finally
            {
                if (link != null) Marshal.FinalReleaseComObject(link);
                if (shell != null) Marshal.FinalReleaseComObject(shell);
            }
        }

        void Log(string message, bool isSuccess)
        {
            string prefix = isSuccess ? "[SUCCESS] " : (message.StartsWith("[") ? "" : "[INFO] ");
            _lstLog.Items.Add($"{prefix}{message}");
            _lstLog.TopIndex = _lstLog.Items.Count - 1;
        }

        private void LstLog_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.DrawBackground();
            string txt = _lstLog.Items[e.Index].ToString();
            Color itemColor = Pal.TEXT;

            if (txt.Contains("[ERROR]")) itemColor = Pal.RED;
            else if (txt.Contains("[SUCCESS]") || txt.Contains("SUCCESS")) itemColor = Pal.GREEN;
            else if (txt.Contains("[INFO]") || txt.Contains("Ready")) itemColor = Pal.DIM;

            using (var brush = new SolidBrush(itemColor)) e.Graphics.DrawString(txt, e.Font, brush, e.Bounds);
            e.DrawFocusRectangle();
        }

        static Label Lbl(string text, int x, int y, int w, int h, Color fore)
            => new Label { Text = text, Left = x, Top = y, Width = w, Height = h, ForeColor = fore, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft };

        static Button MkBtn(string text, Color bg, AnchorStyles anchor)
        {
            var btn = new Button { Text = text, Width = 120, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = Color.White, Font = new Font("Consolas", 9f, FontStyle.Bold), Cursor = Cursors.Hand, Anchor = anchor };
            btn.FlatAppearance.BorderColor = bg; btn.FlatAppearance.BorderSize = 1;
            return btn;
        }

        // ВИПРАВЛЕНО: Тут теж оновлено заголовок попереджень на VespicMM Installer
        static void Msg(string text) => MessageBox.Show(text, "VespicMM Installer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    static class InstallPaths
    {
        public const string ErrorBlocked = "blocked";

        static readonly string[] BlockedPrefixes = BuildBlockedPrefixes();

        static string[] BuildBlockedPrefixes()
        {
            var roots = new List<string>();
            void Add(string path)
            {
                if (string.IsNullOrWhiteSpace(path)) return;
                try { roots.Add(Path.GetFullPath(path).TrimEnd('\\', '/')); }
                catch { }
            }

            Add(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            Add(Environment.GetFolderPath(Environment.SpecialFolder.System));
            string sysWow64 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64");
            if (Directory.Exists(sysWow64)) Add(sysWow64);

            return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static bool TryResolveGameFolder(string input, out string fullPath, out string error)
        {
            fullPath = null;
            error = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                error = "empty";
                return false;
            }

            try { fullPath = Path.GetFullPath(input.Trim()); }
            catch
            {
                error = "invalid";
                return false;
            }

            if (!Directory.Exists(fullPath))
            {
                error = "missing";
                return false;
            }

            if (IsBlockedInstallPath(fullPath))
            {
                error = ErrorBlocked;
                return false;
            }

            return true;
        }

        static bool IsBlockedInstallPath(string fullPath)
        {
            string path = fullPath.TrimEnd('\\', '/');
            foreach (string blocked in BlockedPrefixes)
            {
                if (path.Equals(blocked, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (path.StartsWith(blocked + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static string GetDesktopDirectory()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (!string.IsNullOrEmpty(desktopPath) && Directory.Exists(desktopPath))
                return desktopPath;

            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders"))
            {
                string rawPath = key?.GetValue("Desktop")?.ToString();
                if (!string.IsNullOrEmpty(rawPath))
                {
                    desktopPath = Environment.ExpandEnvironmentVariables(rawPath);
                    if (Directory.Exists(desktopPath)) return desktopPath;
                }
            }

            throw new InvalidOperationException("Desktop folder not found.");
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }
    }
}