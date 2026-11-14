using System;
using System.IO;
using System.Text.Json;
using DWDN_MechanicsTester.Suites;

namespace DWDN_MechanicsTester
{
    public class TestEngine
    {
        public string ClientBinDir { get; set; } = @"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client";
        public string ServerBinDir { get; set; } = @"E:\SteamLibrary\steamapps\common\Mount & Blade II Dedicated Server\bin\Win64_Shipping_Server";
        public string DwdnTryDir { get; set; } = @"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\DWDN_TRY";

        public string DuchyManagerPath => Path.Combine(DwdnTryDir, @"ModuleData\scripts\DWDN_Script\Common\Duchies\DWDN_DuchyManager.cs");
        public string ModClientDll => Path.Combine(DwdnTryDir, @"bin\Win64_Shipping_Client\DWDN_Script.dll");
        public string ModServerDll => Path.Combine(DwdnTryDir, @"bin\Win64_Shipping_Server\DWDN_Script.Server.dll");

        public TestRunReport RunAllSuites(Action<string>? logger = null)
        {
            var report = new TestRunReport();
            var totalSw = System.Diagnostics.Stopwatch.StartNew();

            logger?.Invoke("================================================================================");
            logger?.Invoke("⚡ ЗАПУСК ПОЛИГОНА ТЕСТИРОВАНИЯ МЕХАНИК DWDN (BANNERLORD TEST HARNESS)");
            logger?.Invoke("================================================================================");

            // 1. Сюита Сетевых пакетов
            logger?.Invoke("\n[1/5] Запуск NetworkTestSuite...");
            var netSuite = NetworkTestSuite.Run();
            report.Suites.Add(netSuite);
            LogSuite(logger, netSuite);

            // 2. Сюита Лодаутов и Античита
            logger?.Invoke("\n[2/5] Запуск DuchyLoadoutTestSuite...");
            var loadoutSuite = DuchyLoadoutTestSuite.Run(DuchyManagerPath);
            report.Suites.Add(loadoutSuite);
            LogSuite(logger, loadoutSuite);

            // 3. Сюита Тактической Ауры и Второго дыхания
            logger?.Invoke("\n[3/5] Запуск BannerAuraTestSuite...");
            var auraSuite = BannerAuraTestSuite.Run();
            report.Suites.Add(auraSuite);
            LogSuite(logger, auraSuite);

            // 4. Сюита Походной Артиллерийской Упряжки
            logger?.Invoke("\n[4/5] Запуск ArtilleryTrainTestSuite...");
            var artySuite = ArtilleryTrainTestSuite.Run();
            report.Suites.Add(artySuite);
            LogSuite(logger, artySuite);

            // 5. Сюита ABI и Рефлексии движка
            logger?.Invoke("\n[5/6] Запуск ReflectionAbiTestSuite...");
            var abiSuite = ReflectionAbiTestSuite.Run(ClientBinDir, ServerBinDir, ModClientDll, ModServerDll);
            report.Suites.Add(abiSuite);
            LogSuite(logger, abiSuite);

            // 6. Сюита Admin Tools и Модерации
            logger?.Invoke("\n[6/6] Запуск AdminToolsTestSuite...");
            var adminSuite = AdminToolsTestSuite.Run();
            report.Suites.Add(adminSuite);
            LogSuite(logger, adminSuite);

            totalSw.Stop();
            report.TotalDurationMs = totalSw.Elapsed.TotalMilliseconds;

            foreach (var s in report.Suites)
            {
                report.TotalTests += s.Total;
                report.TotalPassed += s.Passed;
                report.TotalFailed += s.Failed;
            }

            logger?.Invoke("\n================================================================================");
            logger?.Invoke($"ИТОГОВЫЙ ВЕРДИКТ: {(report.TotalFailed == 0 ? "УСПЕХ (ВСЕ МЕХАНИКИ ИСПРАВНЫ)" : $"ОБНАРУЖЕНО {report.TotalFailed} ОШИБОК")}");
            logger?.Invoke($"Всего тестов: {report.TotalTests} | Успешно: {report.TotalPassed} | Сбоев: {report.TotalFailed} | Время: {report.TotalDurationMs:F2} мс");
            logger?.Invoke("================================================================================");

            return report;
        }

        private void LogSuite(Action<string>? logger, TestSuiteResult suite)
        {
            if (logger == null) return;
            string icon = suite.Failed == 0 ? "✅" : "❌";
            logger($"{icon} {suite.SuiteName}: {suite.Passed}/{suite.Total} пройдено ({suite.DurationMs:F2} мс)");
            foreach (var tc in suite.Results)
            {
                string tcIcon = tc.Status == TestStatus.Passed ? "  + " : "  ! [FAIL] ";
                logger($"{tcIcon}{tc.Name} ({tc.DurationMs:F2} мс) — {tc.Message}");
                if (tc.Status == TestStatus.Failed && !string.IsNullOrEmpty(tc.StackTrace))
                {
                    logger($"      Стек: {tc.StackTrace.Trim()}");
                }
            }
        }

        public string ToJson(TestRunReport report)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            return JsonSerializer.Serialize(report, options);
        }
    }
}
