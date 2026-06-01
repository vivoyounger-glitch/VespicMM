using System;
using System.Drawing;
using System.Windows.Forms;

namespace CULauncher
{
    public class ModRowControl : Panel
    {
        readonly bool _isChild;
        readonly Label _btnState;
        readonly Label _lblName;
        readonly Label _lblFolder;

        public ModItem Mod { get; private set; }

                public event Action<ModItem, MouseEventArgs> RowMouseClick;
        public event Action<ModItem> StateClick;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // WS_EX_COMPOSITED for transparency flicker fix
                cp.ExStyle |= 0x02000000;
                return cp;
            }
        }

        public ModRowControl(bool isChild)
        {
            _isChild = isChild;
            // Enhanced double-buffering for transparency
            SetStyle(ControlStyles.AllPaintingInWmPaint | 
                     ControlStyles.UserPaint | 
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Size = isChild ? new Size(498, 36) : new Size(564, 42);
            Margin = isChild ? new Padding(38, 2, 0, 2) : new Padding(0, 4, 0, 2);
            Cursor = Cursors.Hand;
            BackColor = isChild ? UiTheme.GroupChild : UiTheme.Item;

            _btnState = new Label {
                Font = UiTheme.FontMono(isChild ? 11f : 12f, FontStyle.Bold),
                Location = new Point(isChild ? 6 : 8, isChild ? 8 : 10),
                Size = new Size(isChild ? 20 : 22, isChild ? 20 : 22),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            _btnState.Click += (s, e) => { if (Mod != null) StateClick?.Invoke(Mod); };

            _lblName = new Label {
                Font = UiTheme.FontMono(isChild ? 9f : 9.5f),
                Location = new Point(isChild ? 30 : 34, isChild ? 9 : 12),
                Size = new Size(isChild ? 300 : 330, 20),
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                Cursor = Cursors.Hand
            };

            _lblFolder = new Label {
                Font = UiTheme.FontMono(8f, FontStyle.Italic),
                ForeColor = UiTheme.Amber,
                Location = new Point(isChild ? 336 : 370, isChild ? 10 : 14),
                Size = new Size(isChild ? 138 : 135, 20),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.TopRight
            };

            Controls.Add(_btnState);
            Controls.Add(_lblName);
            if (!isChild) Controls.Add(_lblFolder);

            MouseClick += ForwardClick;
            _lblName.MouseClick += ForwardClick;
            _lblFolder.MouseClick += ForwardClick;
        }

        void ForwardClick(object s, MouseEventArgs e)
        {
            if (Mod != null) RowMouseClick?.Invoke(Mod, e);
        }

        public void Bind(ModItem mod, string virtualFolder, bool selected)
        {
            Mod = mod;
            string suffix = mod.IsFolder ? " [DIR]" : " [DLL]";
            _lblName.Text = mod.CleanName + suffix;
            _lblName.Font = UiTheme.FontMono(_isChild ? 9f : 9.5f, mod.IsDisabled ? FontStyle.Regular : FontStyle.Bold);
            _lblName.ForeColor = mod.IsDisabled ? UiTheme.Dim : UiTheme.Text;
            _btnState.Text = mod.IsDisabled ? "○" : "●";
            _btnState.ForeColor = mod.IsDisabled ? UiTheme.Red : UiTheme.Green;
            if (!_isChild)
                _lblFolder.Text = string.IsNullOrEmpty(virtualFolder) ? "" : "[" + virtualFolder + "]";
            SetSelected(selected);
        }

        public void SetSelected(bool selected)
        {
            BackColor = selected ? UiTheme.Selection : (_isChild ? UiTheme.GroupChild : UiTheme.Item);
        }
    }
}
