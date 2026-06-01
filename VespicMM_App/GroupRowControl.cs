using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CULauncher
{
    public class GroupRowControl : Panel
    {
        readonly Label _btnState;
        readonly Label _btnExp;
        readonly Label _lblTitle;
        readonly Label _lblFolder;

        public DllGroup Group { get; private set; }

                public event Action<DllGroup, MouseEventArgs> RowMouseClick;
        public event Action ExpandClick;
        public event Action StateClick;
        public event Action TitleDoubleClick;

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

        public GroupRowControl()
        {
            // Enhanced double-buffering for transparency
            SetStyle(ControlStyles.AllPaintingInWmPaint | 
                     ControlStyles.UserPaint | 
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(564, 42);
            Margin = new Padding(0, 4, 0, 2);
            Cursor = Cursors.Hand;
            BackColor = UiTheme.GroupHeader;

            _btnState = new Label {
                Font = UiTheme.FontMono(12f, FontStyle.Bold),
                Location = new Point(8, 10), Size = new Size(22, 22),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            _btnState.Click += (s, e) => StateClick?.Invoke();

            _btnExp = new Label {
                Font = UiTheme.FontMono(10f, FontStyle.Bold),
                ForeColor = UiTheme.Text,
                Location = new Point(34, 12), Size = new Size(22, 20),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            _btnExp.Click += (s, e) => ExpandClick?.Invoke();

            _lblTitle = new Label {
                Font = UiTheme.FontMono(10f, FontStyle.Bold),
                Location = new Point(58, 12), Size = new Size(270, 20),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            _lblTitle.DoubleClick += (s, e) => TitleDoubleClick?.Invoke();

            _lblFolder = new Label {
                Font = UiTheme.FontMono(8f, FontStyle.Italic),
                ForeColor = UiTheme.Amber,
                Location = new Point(340, 14), Size = new Size(165, 20),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.TopRight
            };

            Controls.Add(_btnState);
            Controls.Add(_btnExp);
            Controls.Add(_lblTitle);
            Controls.Add(_lblFolder);

            MouseClick += ForwardClick;
            _lblTitle.MouseClick += ForwardClick;
        }

        void ForwardClick(object s, MouseEventArgs e)
        {
            if (Group != null) RowMouseClick?.Invoke(Group, e);
        }

        public void Bind(DllGroup group, IList<ModItem> members, string virtualFolder, bool selected)
        {
            Group = group;
            bool allOff = members.All(m => m.IsDisabled);
            bool anyOn = members.Any(m => !m.IsDisabled);
            bool broken = members.Any(m => m.IsDisabled) && anyOn;

            _btnState.Text = allOff ? "○" : (broken ? "◑" : "●");
            _btnState.ForeColor = allOff ? UiTheme.Red : (broken ? UiTheme.Amber : UiTheme.Green);
            _btnExp.Text = group.IsExpanded ? "▼" : "►";
            _lblTitle.Text = "[📦] " + group.Name.ToUpper();
            _lblTitle.ForeColor = broken ? UiTheme.Amber : UiTheme.Text;
            _lblFolder.Text = string.IsNullOrEmpty(virtualFolder) ? "" : "[" + virtualFolder + "]";
            SetSelected(selected);
        }

        public void SetSelected(bool selected)
        {
            BackColor = selected ? UiTheme.Selection : UiTheme.GroupHeader;
        }
    }
}
