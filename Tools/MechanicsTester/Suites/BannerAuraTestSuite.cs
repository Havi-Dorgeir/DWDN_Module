using System;
using System.Collections.Generic;

namespace DWDN_MechanicsTester.Suites
{
    public class MockAgent
    {
        public int Index { get; set; }
        public bool IsAlive { get; set; } = true;
        public int TeamId { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
        public string WieldedItem { get; set; } = "";
        public bool IsBannerman { get; set; }

        public float Health { get; set; } = 100f;
        public float HealthLimit { get; set; } = 100f;
        public float LastDamageTime { get; set; } = -100f;

        // Базовые характеристики
        public int BaseArmorHead { get; set; } = 20;
        public int BaseArmorTorso { get; set; } = 30;
        public int BaseArmorArms { get; set; } = 15;
        public int BaseArmorLegs { get; set; } = 18;
        public float BaseMaxSpeed { get; set; } = 1.0f;

        // Текущие модифицированные характеристики
        public int CurrentArmorHead { get; set; } = 20;
        public int CurrentArmorTorso { get; set; } = 30;
        public int CurrentArmorArms { get; set; } = 15;
        public int CurrentArmorLegs { get; set; } = 18;
        public float CurrentMaxSpeed { get; set; } = 1.0f;

        public int CurrentAuraTier { get; set; } = 0; // 0 = нет, 1 = союзник, 2 = знаменосец

        public float DistanceTo(MockAgent other)
        {
            float dx = PosX - other.PosX;
            float dy = PosY - other.PosY;
            float dz = PosZ - other.PosZ;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }

    public class MockAuraSystem
    {
        public const float AuraRadius = 12.0f;
        public const float OocDamageCooldown = 5.0f;
        public const float OocHealInterval = 3.0f;
        public const float OocHealthCapRatio = 0.75f;

        private float _lastHealTickTime = 0f;

        public void UpdateAuras(List<MockAgent> agents, float currentTime)
        {
            // 1. Сначала определяем активных знаменосцев
            var activeBannermen = new List<MockAgent>();
            foreach (var a in agents)
            {
                if (!a.IsAlive) continue;
                bool holdsBanner = a.WieldedItem.Contains("banner");
                if (a.IsBannerman && holdsBanner)
                {
                    activeBannermen.Add(a);
                }
            }

            // 2. Для каждого живого агента вычисляем максимальный Tier
            foreach (var agent in agents)
            {
                if (!agent.IsAlive)
                {
                    agent.CurrentAuraTier = 0;
                    continue;
                }

                int bestTier = 0;

                // Проверяем, сам ли он знаменосец с активным знаменем
                if (agent.IsBannerman && agent.WieldedItem.Contains("banner"))
                {
                    bestTier = 2;
                }
                else
                {
                    // Ищем ближайшего союзного знаменосца
                    foreach (var bm in activeBannermen)
                    {
                        if (bm.TeamId == agent.TeamId && bm.DistanceTo(agent) <= AuraRadius)
                        {
                            bestTier = 1; // Защита от стакания: всегда Tier 1, даже если рядом 10 знаменосцев!
                            break;
                        }
                    }
                }

                agent.CurrentAuraTier = bestTier;

                // 3. Применяем характеристики
                int armorBonus = bestTier switch
                {
                    2 => 6, // Знаменосец
                    1 => 4, // Союзник в строю
                    _ => 0
                };

                float speedBonus = bestTier switch
                {
                    2 => 0.05f,
                    1 => 0.05f,
                    _ => 0.00f
                };

                agent.CurrentArmorHead = agent.BaseArmorHead + armorBonus;
                agent.CurrentArmorTorso = agent.BaseArmorTorso + armorBonus;
                agent.CurrentArmorArms = agent.BaseArmorArms + armorBonus;
                agent.CurrentArmorLegs = agent.BaseArmorLegs + armorBonus;
                agent.CurrentMaxSpeed = agent.BaseMaxSpeed * (1.0f + speedBonus);
            }

            // 4. Механика «Второго дыхания» (регенерация вне боя)
            if (currentTime - _lastHealTickTime >= OocHealInterval)
            {
                _lastHealTickTime = currentTime;
                foreach (var agent in agents)
                {
                    if (!agent.IsAlive || agent.CurrentAuraTier == 0) continue;

                    // Проверяем кулдаун получения урона (5 секунд)
                    if (currentTime - agent.LastDamageTime >= OocDamageCooldown)
                    {
                        float cap = agent.HealthLimit * OocHealthCapRatio; // 75%
                        if (agent.Health < cap)
                        {
                            agent.Health = Math.Min(cap, agent.Health + 1.0f);
                        }
                    }
                }
            }
        }
    }

    public static class BannerAuraTestSuite
    {
        public static TestSuiteResult Run()
        {
            var suite = new TestSuiteResult
            {
                SuiteName = "Tactical Formation Aura & Second Wind Mechanics",
                Description = "Тестирование боевой логики DWDN_BannerAuraBehavior (радиус 12м, анти-стакинг, броня и регенерация)"
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();

            RunTest(suite, "Test_Aura_Proximity_Activation_And_Deactivation", () =>
            {
                var auraSys = new MockAuraSystem();
                var bannerman = new MockAgent
                {
                    Index = 1,
                    TeamId = 1,
                    IsBannerman = true,
                    WieldedItem = "dwdn_banner1",
                    PosX = 0, PosY = 0, PosZ = 0
                };

                var ally = new MockAgent
                {
                    Index = 2,
                    TeamId = 1,
                    IsBannerman = false,
                    PosX = 8.0f, PosY = 0, PosZ = 0 // В пределах 12м
                };

                var agents = new List<MockAgent> { bannerman, ally };
                auraSys.UpdateAuras(agents, 1.0f);

                Assert.AreEqual(2, bannerman.CurrentAuraTier, "Знаменосец со знаменем в руках должен иметь Tier 2");
                Assert.AreEqual(1, ally.CurrentAuraTier, "Союзник на расстоянии 8м должен получить Tier 1");
                Assert.AreEqual(34, ally.CurrentArmorTorso, "Союзник должен получить +4 к броне торса (30 + 4 = 34)");
                Assert.AreEqual(36, bannerman.CurrentArmorTorso, "Знаменосец должен получить +6 к броне торса (30 + 6 = 36)");

                // Перемещаем союзника за пределы радиуса (15м)
                ally.PosX = 15.0f;
                auraSys.UpdateAuras(agents, 2.0f);

                Assert.AreEqual(0, ally.CurrentAuraTier, "Вне радиуса 12м аура должна погаснуть");
                Assert.AreEqual(30, ally.CurrentArmorTorso, "Броня должна вернуться к базовой 30");
            });

            RunTest(suite, "Test_Banner_Sheathed_Deactivates_Aura", () =>
            {
                var auraSys = new MockAuraSystem();
                var bannerman = new MockAgent
                {
                    Index = 1,
                    TeamId = 1,
                    IsBannerman = true,
                    WieldedItem = "dwdn_banner1",
                    PosX = 0, PosY = 0, PosZ = 0
                };
                var ally = new MockAgent
                {
                    Index = 2,
                    TeamId = 1,
                    PosX = 4.0f, PosY = 0, PosZ = 0
                };

                var agents = new List<MockAgent> { bannerman, ally };
                auraSys.UpdateAuras(agents, 1.0f);
                Assert.AreEqual(1, ally.CurrentAuraTier, "Аура активна, пока знамя в руках");

                // Знаменосец убрал флаг за спину и взял меч
                bannerman.WieldedItem = "mp_vlandian_arming_sword";
                auraSys.UpdateAuras(agents, 2.0f);

                Assert.AreEqual(0, bannerman.CurrentAuraTier, "Без знамени в руках знаменосец теряет ауру");
                Assert.AreEqual(0, ally.CurrentAuraTier, "Союзники теряют ауру, если знаменосец убрал знамя");
            });

            RunTest(suite, "Test_Anti_Stacking_Multiple_Bannermen", () =>
            {
                var auraSys = new MockAuraSystem();
                var bm1 = new MockAgent { Index = 1, TeamId = 1, IsBannerman = true, WieldedItem = "dwdn_banner1", PosX = 2f };
                var bm2 = new MockAgent { Index = 2, TeamId = 1, IsBannerman = true, WieldedItem = "dwdn_banner1", PosX = 3f };
                var ally = new MockAgent { Index = 3, TeamId = 1, PosX = 0f };

                var agents = new List<MockAgent> { bm1, bm2, ally };
                auraSys.UpdateAuras(agents, 1.0f);

                Assert.AreEqual(1, ally.CurrentAuraTier, "Аура от двух знаменосцев НЕ должна складываться (Anti-Stacking)");
                Assert.AreEqual(34, ally.CurrentArmorTorso, "Броня союзника должна быть строго +4, а не +8");
            });

            RunTest(suite, "Test_Second_Wind_OutOfCombat_Regeneration", () =>
            {
                var auraSys = new MockAuraSystem();
                var bannerman = new MockAgent { Index = 1, TeamId = 1, IsBannerman = true, WieldedItem = "dwdn_banner1" };
                var ally = new MockAgent
                {
                    Index = 2,
                    TeamId = 1,
                    Health = 50f,
                    HealthLimit = 100f,
                    LastDamageTime = 10.0f // Урон получен в t = 10.0
                };
                var agents = new List<MockAgent> { bannerman, ally };

                // 1. Проверяем в t = 13.0 (прошло 3 сек с момента урона, кулдаун 5 сек еще НЕ прошел)
                auraSys.UpdateAuras(agents, 13.0f);
                Assert.AreEqual(50f, ally.Health, "Регенерация НЕ должна работать во время боя (прошло < 5 сек с урона)");

                // 2. Проверяем в t = 16.0 (прошло 6 сек с урона и наступил тик 3 сек)
                auraSys.UpdateAuras(agents, 16.0f);
                Assert.AreEqual(51f, ally.Health, "Вне боя должно восстановиться ровно +1 HP");

                // 3. Проверяем ограничение потолка в 75% HP
                ally.Health = 74.8f;
                auraSys.UpdateAuras(agents, 19.0f);
                Assert.AreApproximatelyEqual(75.0f, ally.Health, 0.01f, "Регенерация должна подняться до 75%");

                // 4. Дальнейший тик не должен превышать 75%
                auraSys.UpdateAuras(agents, 22.0f);
                Assert.AreEqual(75.0f, ally.Health, "Регенерация 'Второе дыхание' строго ограничена 75% от HealthLimit");
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
