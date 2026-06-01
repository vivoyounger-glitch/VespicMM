using System.Drawing;
using System.Windows.Forms;

namespace CULauncher
{
    public static class UiTheme
    {
        public static readonly Color Bg = Color.FromArgb(13, 17, 13);
        public static readonly Color PanelHeader = Color.FromArgb(200, 17, 22, 15);
        public static readonly Color Item = Color.FromArgb(90, 25, 35, 25);
        public static readonly Color GroupHeader = Color.FromArgb(110, 35, 40, 30);
        public static readonly Color GroupChild = Color.FromArgb(60, 20, 25, 20);
        public static readonly Color Border = Color.FromArgb(68, 58, 22);
        public static readonly Color Amber = Color.FromArgb(212, 168, 52);
        public static readonly Color Green = Color.FromArgb(82, 158, 58);
        public static readonly Color Red = Color.FromArgb(158, 48, 38);
        public static readonly Color Text = Color.FromArgb(214, 208, 188);
        public static readonly Color Dim = Color.FromArgb(102, 98, 82);
        public static readonly Color Selection = Color.FromArgb(160, 45, 60, 42);
        public static readonly Color InputBg = Color.FromArgb(18, 24, 18);
        public static readonly Color Play = Color.FromArgb(200, 42, 82, 30);
        public static readonly Color BtnNeutral = Color.FromArgb(160, 20, 26, 18);

        public static Font FontUi(float size = 9f, FontStyle style = FontStyle.Regular) =>
            new Font("Segoe UI", size, style);
        public static Font FontMono(float size = 9.5f, FontStyle style = FontStyle.Regular) =>
            new Font("Consolas", size, style);

        public static void StyleFlatButton(Button b, Color back, Color fore)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Border;
            b.FlatAppearance.BorderSize = 1;
            b.BackColor = back;
            b.ForeColor = fore;
            b.Cursor = Cursors.Hand;
        }
    }
}
