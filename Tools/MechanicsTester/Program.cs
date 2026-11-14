using System;
using System.IO;
using System.Windows.Forms;

namespace DWDN_MechanicsTester
{
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
            }
            catch { }

            // Проверяем CLI-режим для ИИ и автоматических пайплайнов
            bool isCli = false;
            bool isJson = false;

            foreach (var arg in args)
            {
                if (arg.Equals("--cli", StringComparison.OrdinalIgnoreCase) || arg.Equals("-c", StringComparison.OrdinalIgnoreCase))
                    isCli = true;
                if (arg.Equals("--json", StringComparison.OrdinalIgnoreCase) || arg.Equals("-j", StringComparison.OrdinalIgnoreCase))
                {
                    isCli = true;
                    isJson = true;
                }
            }

            if (isCli)
            {
                var engine = new TestEngine();
                Action<string>? logger = isJson ? null : Console.WriteLine;
                var report = engine.RunAllSuites(logger);

                if (isJson)
                {
                    Console.WriteLine(engine.ToJson(report));
                }

                return report.TotalFailed == 0 ? 0 : 1;
            }

            // GUI-режим для Создателя
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
            return 0;
        }
    }
}
