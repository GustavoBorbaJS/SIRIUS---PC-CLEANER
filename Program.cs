using System;
using System.Windows.Forms;

namespace ProcessCleaner
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            try
            {
                Application.Run(new ProcessCleanerForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro fatal: {ex.Message}\n\n{ex.StackTrace}", 
                    "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}