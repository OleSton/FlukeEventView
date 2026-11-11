using System;
using System.Windows.Forms;

namespace FlukeEventView
{
    static class Program
    {
        /// <summary>
        /// Главная точка входа для приложения Fluke EventView EV101.
        /// Атрибут STAThread обязателен для корректной работы системных диалогов открытия/сохранения файлов Windows Forms.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Включаем визуальные стили операционной системы Windows для элементов управления
            Application.EnableVisualStyles();
            // Используем современный рендеринг текста GDI+ (TextRenderer)
            Application.SetCompatibleTextRenderingDefault(false);
            // Запускаем главную форму приложения
            Application.Run(new Form1());
        }
    }
}
