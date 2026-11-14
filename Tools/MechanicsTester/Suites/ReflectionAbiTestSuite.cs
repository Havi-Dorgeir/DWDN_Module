using System;
using System.IO;
using System.Reflection;

namespace DWDN_MechanicsTester.Suites
{
    public static class ReflectionAbiTestSuite
    {
        private static bool _resolverRegistered = false;

        public static TestSuiteResult Run(string clientBinDir, string serverBinDir, string modClientDll, string modServerDll)
        {
            var suite = new TestSuiteResult
            {
                SuiteName = "Engine ABI & Reflection Target Compatibility",
                Description = "Проверка существования вызываемых через рефлексию методов TaleWorlds на клиенте и сервере"
            };

            // Регистрируем безопасный резолвер зависимостей для сборок Bannerlord
            if (!_resolverRegistered)
            {
                _resolverRegistered = true;
                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                {
                    try
                    {
                        var asmName = new AssemblyName(args.Name).Name + ".dll";
                        var clientPath = Path.Combine(clientBinDir, asmName);
                        if (File.Exists(clientPath)) return Assembly.LoadFrom(clientPath);
                        var serverPath = Path.Combine(serverBinDir, asmName);
                        if (File.Exists(serverPath)) return Assembly.LoadFrom(serverPath);
                    }
                    catch { }
                    return null;
                };
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();

            RunTest(suite, "Test_Client_Engine_Assemblies_Exist", () =>
            {
                Assert.IsTrue(Directory.Exists(clientBinDir), $"Клиентская директория bin не найдена: {clientBinDir}");
                string mbPath = Path.Combine(clientBinDir, "TaleWorlds.MountAndBlade.dll");
                string corePath = Path.Combine(clientBinDir, "TaleWorlds.Core.dll");
                string enginePath = Path.Combine(clientBinDir, "TaleWorlds.Engine.dll");

                Assert.IsTrue(File.Exists(mbPath), "TaleWorlds.MountAndBlade.dll должен существовать");
                Assert.IsTrue(File.Exists(corePath), "TaleWorlds.Core.dll должен существовать");
                Assert.IsTrue(File.Exists(enginePath), "TaleWorlds.Engine.dll должен существовать");
            });

            RunTest(suite, "Test_Server_Engine_Assemblies_Exist", () =>
            {
                Assert.IsTrue(Directory.Exists(serverBinDir), $"Серверная директория bin не найдена: {serverBinDir}");
                string mbServerPath = Path.Combine(serverBinDir, "TaleWorlds.MountAndBlade.dll");
                Assert.IsTrue(File.Exists(mbServerPath), "Серверный TaleWorlds.MountAndBlade.dll должен существовать");
            });

            RunTest(suite, "Test_DWDN_Compiled_DLLs_Presence", () =>
            {
                Assert.IsTrue(File.Exists(modClientDll), $"Клиентская сборка мода должна быть скомпилирована: {modClientDll}");
                Assert.IsTrue(File.Exists(modServerDll), $"Серверная сборка мода (.NET 8) должна присутствовать: {modServerDll}");
            });

            RunTest(suite, "Test_Reflection_Target_Agent_SetMaximumSpeedLimit", () =>
            {
                string mbPath = Path.Combine(clientBinDir, "TaleWorlds.MountAndBlade.dll");
                if (!File.Exists(mbPath)) return;

                var asm = Assembly.LoadFrom(mbPath);
                var agentType = asm.GetType("TaleWorlds.MountAndBlade.Agent");
                Assert.IsNotNull(agentType, "Тип TaleWorlds.MountAndBlade.Agent обязан существовать");

                var method = agentType!.GetMethod("SetMaximumSpeedLimit", BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(method, "Метод Agent.SetMaximumSpeedLimit обязан присутствовать в сборке движка (для ограничения шага упряжного коня)");
            });

            RunTest(suite, "Test_Reflection_Target_GameNetwork_Methods", () =>
            {
                string mbPath = Path.Combine(clientBinDir, "TaleWorlds.MountAndBlade.dll");
                if (!File.Exists(mbPath)) return;

                var asm = Assembly.LoadFrom(mbPath);
                var gnType = asm.GetType("TaleWorlds.MountAndBlade.GameNetwork");
                Assert.IsNotNull(gnType, "Тип TaleWorlds.MountAndBlade.GameNetwork обязан присутствовать");

                // Проверяем ключевые свойства GameNetwork
                var isClientProp = gnType!.GetProperty("IsClient", BindingFlags.Public | BindingFlags.Static);
                var isServerProp = gnType.GetProperty("IsServer", BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(isClientProp, "Свойство GameNetwork.IsClient должно существовать");
                Assert.IsNotNull(isServerProp, "Свойство GameNetwork.IsServer должно существовать");
            });

            sw.Stop();
            suite.DurationMs = sw.Elapsed.TotalMilliseconds;
            return suite;
        }

        private static void RunTest(TestSuiteResult suite, string testName, Action testAction)
        {
            var tc = new TestCaseResult { Name = testName, SuiteName = suite.SuiteName };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                testAction();
                tc.Status = TestStatus.Passed;
                tc.Message = "PASSED";
            }
            catch (Exception ex)
            {
                tc.Status = TestStatus.Failed;
                tc.Message = ex.Message;
                tc.StackTrace = ex.StackTrace;
            }
            finally
            {
                sw.Stop();
                tc.DurationMs = sw.Elapsed.TotalMilliseconds;
                suite.Results.Add(tc);
            }
        }
    }
}
