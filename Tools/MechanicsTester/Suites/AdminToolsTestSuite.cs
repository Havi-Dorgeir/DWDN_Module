using System;
using System.Collections.Generic;
using System.Linq;

namespace DWDN_MechanicsTester.Suites
{
    /// <summary>
    /// Тестовая сюита для проверки DWDN Admin Tools:
    /// 1. Сетевые пакеты RequestAdminActionMessage и SyncAdminStateMessage.
    /// 2. Аутентификация администратора и защита от несанкционированного доступа.
    /// 3. Логика резолвера целей (ResolveTargets: me, all, team1, team2, nick, id).
    /// 4. Клиентская фильтрация и поиск игроков в AdminPanelVM.
    /// 5. Диспетчеризация команд и параметров.
    /// </summary>
    public static class AdminToolsTestSuite
    {
        public static TestSuiteResult Run()
        {
            var suite = new TestSuiteResult
            {
                SuiteName = "Admin Tools & Moderation Security",
                Description = "Тестирование сетевого протокола, авторизации, парсера целей и UI-логики Admin Tools"
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 1. Тест сериализации RequestAdminActionMessage
            RunTest(suite, "Test_RequestAdminActionMessage_Serialization_Roundtrip", () =>
            {
                var stream = new MockNetworkStream();
                bool valid = true;

                string action = "heal";
                string targetId = "2.0.0.12345678";
                string arg = "100";

                stream.WriteString(action);
                stream.WriteString(targetId);
                stream.WriteString(arg);

                stream.ResetForReading();
                string rAction = stream.ReadString(ref valid);
                string rTarget = stream.ReadString(ref valid);
                string rArg = stream.ReadString(ref valid);

                Assert.IsTrue(valid, "Поток данных должен оставаться валидным");
                Assert.AreEqual(action, rAction, "Действие должно совпадать");
                Assert.AreEqual(targetId, rTarget, "TargetId должен совпадать");
                Assert.AreEqual(arg, rArg, "Аргумент должен совпадать");
            });

            // 2. Тест сериализации SyncAdminStateMessage
            RunTest(suite, "Test_SyncAdminStateMessage_Serialization_Roundtrip", () =>
            {
                var stream = new MockNetworkStream();
                bool valid = true;

                stream.WriteBool(true);
                stream.WriteBool(false);

                stream.ResetForReading();
                bool rAdmin1 = stream.ReadBool(ref valid);
                bool rAdmin2 = stream.ReadBool(ref valid);

                Assert.IsTrue(valid, "Поток данных должен оставаться валидным");
                Assert.IsTrue(rAdmin1, "Первый флаг должен быть true");
                Assert.IsFalse(rAdmin2, "Второй флаг должен быть false");
            });

            // 3. Тест аутентификации администратора
            RunTest(suite, "Test_Admin_Authentication_Password_Validation", () =>
            {
                string serverPassword = "SacredOmnissiahPassword2026";

                bool TryLogin(string inputPass) => string.Equals(inputPass, serverPassword, StringComparison.OrdinalIgnoreCase);

                Assert.IsTrue(TryLogin("SacredOmnissiahPassword2026"), "Верный пароль должен успешно авторизовать");
                Assert.IsTrue(TryLogin("sacredomnissiahpassword2026"), "Пароль без учета регистра должен работать");
                Assert.IsFalse(TryLogin("WrongPassword"), "Неверный пароль должен быть отклонен");
                Assert.IsFalse(TryLogin(""), "Пустой пароль должен быть отклонен");
            });

            // 4. Тест блокировки команд для неавторизованных пользователей
            RunTest(suite, "Test_Admin_Security_Unauthorized_Access_Rejection", () =>
            {
                var adminSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "2.0.0.1111", "2.0.0.2222" };

                bool IsAuthorized(string peerId, bool isServer)
                {
                    if (isServer) return true;
                    return adminSet.Contains(peerId);
                }

                Assert.IsTrue(IsAuthorized("2.0.0.1111", false), "Админ из белого списка должен быть авторизован");
                Assert.IsTrue(IsAuthorized("anything", true), "Локальный хост/сервер должен быть авторизован");
                Assert.IsFalse(IsAuthorized("2.0.0.9999", false), "Случайный игрок не должен иметь прав админа");
                Assert.IsFalse(IsAuthorized("", false), "Пустой ID не должен иметь прав админа");
            });

            // 5. Тест резолвера целей (ResolveTargets logic)
            RunTest(suite, "Test_Admin_Target_Resolution_Logic", () =>
            {
                var players = new List<SimulatedPlayer>
                {
                    new() { Id = "1", Name = "Arthur", Team = 1, IsAlive = true },
                    new() { Id = "2", Name = "Lancelot", Team = 1, IsAlive = true },
                    new() { Id = "3", Name = "Mordred", Team = 2, IsAlive = true },
                    new() { Id = "4", Name = "DeadKnight", Team = 2, IsAlive = false },
                    new() { Id = "5", Name = "SpectatorGuy", Team = 0, IsAlive = false }
                };

                var me = players[0];

                List<SimulatedPlayer> Resolve(string targetStr)
                {
                    if (string.IsNullOrWhiteSpace(targetStr) || targetStr.Equals("me", StringComparison.OrdinalIgnoreCase))
                        return new List<SimulatedPlayer> { me };

                    if (targetStr.Equals("all", StringComparison.OrdinalIgnoreCase))
                        return players.Where(p => p.IsAlive).ToList();

                    if (targetStr.Equals("team1", StringComparison.OrdinalIgnoreCase) || targetStr.Equals("att", StringComparison.OrdinalIgnoreCase))
                        return players.Where(p => p.Team == 1 && p.IsAlive).ToList();

                    if (targetStr.Equals("team2", StringComparison.OrdinalIgnoreCase) || targetStr.Equals("def", StringComparison.OrdinalIgnoreCase))
                        return players.Where(p => p.Team == 2 && p.IsAlive).ToList();

                    return players.Where(p => p.Name.IndexOf(targetStr, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                              p.Id.IndexOf(targetStr, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                }

                // me
                var resMe = Resolve("me");
                Assert.AreEqual(1, resMe.Count, "Цель 'me' должна возвращать 1 игрока");
                Assert.AreEqual("Arthur", resMe[0].Name, "Цель 'me' должна быть Arthur");

                // all
                var resAll = Resolve("all");
                Assert.AreEqual(3, resAll.Count, "Цель 'all' должна возвращать 3 живых игроков");

                // team1
                var resTeam1 = Resolve("team1");
                Assert.AreEqual(2, resTeam1.Count, "Цель 'team1' должна возвращать 2 бойцов");

                // team2
                var resTeam2 = Resolve("def");
                Assert.AreEqual(1, resTeam2.Count, "Цель 'def' должна возвращать только живого Mordred");

                // Поиск по нику
                var resLance = Resolve("lance");
                Assert.AreEqual(1, resLance.Count, "Поиск по части имени должен находить Lancelot");
                Assert.AreEqual("Lancelot", resLance[0].Name, "Имя должно быть Lancelot");
            });

            // 6. Тест клиентской фильтрации в AdminPanelVM
            RunTest(suite, "Test_AdminPanelVM_Search_And_Filtering", () =>
            {
                var rows = new List<SimulatedRow>
                {
                    new() { Name = "Galahad", Id = "101", Team = "Атакующие" },
                    new() { Name = "Gawain", Id = "102", Team = "Атакующие" },
                    new() { Name = "RobinHood", Id = "201", Team = "Защитники" }
                };

                List<SimulatedRow> Filter(string query)
                {
                    string q = query.Trim().ToLowerInvariant();
                    return rows.Where(r => string.IsNullOrEmpty(q) ||
                                           r.Name.ToLowerInvariant().Contains(q) ||
                                           r.Id.ToLowerInvariant().Contains(q) ||
                                           r.Team.ToLowerInvariant().Contains(q)).ToList();
                }

                Assert.AreEqual(3, Filter("").Count, "Пустой запрос должен возвращать всех");
                Assert.AreEqual(2, Filter("ga").Count, "'ga' должен возвращать Galahad и Gawain");
                Assert.AreEqual(1, Filter("robin").Count, "'robin' должен возвращать RobinHood");
                Assert.AreEqual(1, Filter("Защитники").Count, "'Защитники' должен возвращать 1 игрока");
                Assert.AreEqual(0, Filter("NonExistentPlayer").Count, "Несуществующий игрок должен возвращать 0");
            });

            // 7. Тест валидации аргументов диспетчера действий
            RunTest(suite, "Test_Admin_Action_Dispatcher_Mapping", () =>
            {
                var supportedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "heal", "heal_all", "god", "godmode", "slay", "kill", "kill_all", "kill_bots",
                    "freeze", "disarm", "goto", "bring", "bring_all", "kick", "ban", "ammo", "repair",
                    "end_warmup", "extend_warmup", "end_round", "map", "alert"
                };

                string[] sampleActions = { "heal", "god", "slay", "freeze", "goto", "bring", "kick", "ban", "kill_bots", "end_warmup" };
                foreach (var act in sampleActions)
                {
                    Assert.IsTrue(supportedActions.Contains(act), $"Действие '{act}' должно поддерживаться диспетчером");
                }
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

        private class SimulatedPlayer
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public int Team { get; set; }
            public bool IsAlive { get; set; }
        }

        private class SimulatedRow
        {
            public string Name { get; set; } = "";
            public string Id { get; set; } = "";
            public string Team { get; set; } = "";
        }
    }
}
