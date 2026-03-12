using System.Drawing;
using System.Windows.Forms;

namespace ProcessCleaner
{
    public static class AppStyles
    {
        public static Color PrimaryColor = Color.FromArgb(52, 152, 219);
        public static Color SecondaryColor = Color.FromArgb(46, 204, 113);
        public static Color AccentColor = Color.FromArgb(231, 76, 60);
        public static Color WarningColor = Color.FromArgb(241, 196, 15);
        public static Color DarkColor = Color.FromArgb(52, 73, 94);
        public static Color BackgroundColor = Color.FromArgb(236, 240, 241);
        
        public static Font NormalFont = new Font("Segoe UI", 9f);
        public static Font MonospaceFont = new Font("Consolas", 9f);
        public static Font BoldFont = new Font("Segoe UI", 9f, FontStyle.Bold);

        public static void ApplyModernButton(Button button, Color color)
        {
            if (button == null) return;
            
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = NormalFont;
            button.Cursor = Cursors.Hand;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(color, 0.2f);
        }
    }
}