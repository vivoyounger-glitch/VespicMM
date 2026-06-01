using System;
using System.IO;
using System.Windows.Forms;

namespace CULauncher
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher_debug.txt");
            File.WriteAllText(logPath, "=== ЗАПУСК ЛОНЧЕРА ===\r\n");

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                File.AppendAllText(logPath, "[INFO] Ініціалізація та запуск MainForm...\r\n");
                Application.Run(new MainForm());
                
                File.AppendAllText(logPath, "[INFO] Програма завершилась штатно.\r\n");
            }
            catch (Exception ex)
            {
                string errorText = $"\r\n[КРИТИЧНА ПОМИЛКА СТАРТУ]:\r\n" +
                                   $"Тип помилки: {ex.GetType().FullName}\r\n" +
                                   $"Повідомлення: {ex.Message}\r\n" +
                                   $"Стек викликів:\r\n{ex.StackTrace}\r\n";
                
                if (ex.InnerException != null)
                {
                    errorText += $"\r\n[Внутрішня помилка]:\r\n{ex.InnerException.Message}\r\n{ex.InnerException.StackTrace}\r\n";
                }

                File.AppendAllText(logPath, errorText);
                MessageBox.Show(errorText, "Збій запуску лончера", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}