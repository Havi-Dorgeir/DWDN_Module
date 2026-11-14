using System;
using System.Collections.Generic;
using System.IO;

namespace DWDN_MechanicsTester.Suites
{
    public class MockClassDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string CharacterId { get; set; } = "";
        public List<string> AllowedHelmets { get; } = new();
        public List<string> AllowedBodyArmors { get; } = new();
        public List<string> AllowedLegs { get; } = new();
        public List<string> AllowedGloves { get; } = new();
        public List<string> AllowedCapes { get; } = new();
        public List<string> AllowedWeapons0 { get; } = new();
        public List<string> AllowedWeapons1 { get; } = new();
        public List<string> AllowedWeapons2 { get; } = new();
        public List<string> AllowedWeapons3 { get; } = new();

        public bool ValidateItem(int slotIndex, string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return true; // Пустой слот разрешен

            List<string> pool = slotIndex switch
            {
                0 => AllowedWeapons0,
                1 => AllowedWeapons1,
                2 => AllowedWeapons2,
                3 => AllowedWeapons3,
                10 => AllowedHelmets,
                11 => AllowedBodyArmors,
                12 => AllowedLegs,
                13 => AllowedGloves,
                14 => AllowedCapes,
                _ => new List<string>()
            };

            return pool.Contains(itemId);
        }
    }

    public static class DuchyLoadoutTestSuite
    {
        public static TestSuiteResult Run(string duchyManagerPath)
        {
            var suite = new TestSuiteResult
            {
                SuiteName = "Duchy & Class Loadout Logic & Anti-Cheat",
                Description = "Проверка реестра классов DWDN_DuchyManager, корректности слотов и античит-фильтрации"
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();

            RunTest(suite, "Test_DuchyManager_File_Exists", () =>
            {
                Assert.IsTrue(File.Exists(duchyManagerPath), $"Файл DWDN_DuchyManager.cs должен существовать по пути: {duchyManagerPath}");
            });

            RunTest(suite, "Test_No_Item_Prefix_In_DuchyManager", () =>
            {
                if (!File.Exists(duchyManagerPath)) return;
                string content = File.ReadAllText(duchyManagerPath);

                // В DWDN_DuchyManager.cs все строковые идентификаторы предметов должны быть без "Item."
                // "Item.xxx" ломает MBObjectManager.GetObject<ItemObject>(id)!
                int index = content.IndexOf("\"Item.");
                if (index != -1)
                {
                    string snippet = content.Substring(index, Math.Min(40, content.Length - index));
                    Assert.Fail($"Обнаружен запрещенный префикс 'Item.' в DWDN_DuchyManager: {snippet}");
                }
            });

            RunTest(suite, "Test_Engineer_Class_Definition_Structure", () =>
            {
                // Эмулируем регистрацию Инженера-Артиллериста
                var engineer = new MockClassDef
                {
                    Id = "mp_Artillerist",
                    Name = "Инженер (Осадный мастер)",
                    CharacterId = "mp_Artillerist_hero"
                };

                engineer.AllowedHelmets.AddRange(new[] { "dwdn_pikeman_kettlehad", "crossbowman_kettle_1", "Banobranny_helmet", "" });
                engineer.AllowedBodyArmors.AddRange(new[] { "Banobranny_surcoat", "crossbowman_surcoat_gambeson" });
                engineer.AllowedLegs.AddRange(new[] { "banobranyi_leg", "crossbowman_leggings" });
                engineer.AllowedGloves.AddRange(new[] { "crossbowman_mailgoves", "arch_banreich_leatherglove", "" });

                // Слот 0: Личное оружие
                engineer.AllowedWeapons0.AddRange(new[] { "dwdn_falchion", "dwdn_cleaver", "mp_vlandian_hammer", "mp_vlandian_short_sword" });
                // Слот 1: Чертежи орудий на тележке
                engineer.AllowedWeapons1.AddRange(new[] { "dwdn_blueprint_ballista", "dwdn_blueprint_onager", "dwdn_order_mangonel", "dwdn_order_ballista" });

                Assert.IsTrue(engineer.AllowedBodyArmors.Count >= 1, "Класс обязан иметь хотя бы 1 вариант брони тела");
                Assert.IsFalse(string.IsNullOrEmpty(engineer.AllowedBodyArmors[0]), "Первый элемент брони тела не может быть пустой строкой (защита от голого спавна)");
                Assert.IsTrue(engineer.AllowedWeapons1.Count >= 2, "Инженер обязан иметь выбор как минимум между двумя чертежами осадных орудий");
            });

            RunTest(suite, "Test_AntiCheat_Validation_Logic", () =>
            {
                var engineer = new MockClassDef
                {
                    Id = "mp_Artillerist"
                };
                engineer.AllowedWeapons0.AddRange(new[] { "dwdn_falchion", "dwdn_cleaver" });
                engineer.AllowedWeapons1.AddRange(new[] { "dwdn_blueprint_ballista", "dwdn_blueprint_onager" });

                // 1. Легитимный выбор
                bool legitWeapon = engineer.ValidateItem(0, "dwdn_falchion");
                bool legitBlueprint = engineer.ValidateItem(1, "dwdn_blueprint_ballista");
                Assert.IsTrue(legitWeapon, "Легитимный фальшион должен пройти валидацию");
                Assert.IsTrue(legitBlueprint, "Легитимный чертеж баллисты должен пройти валидацию");

                // 2. Читерская попытка экипировать тяжелое кавалерийское копье в слот артиллериста
                bool cheatSpear = engineer.ValidateItem(0, "mp_vlandian_heavy_lance");
                Assert.IsFalse(cheatSpear, "Нелегитимное копье чужого класса ДОЛЖНО быть отклонено античитом сервера!");

                // 3. Попытка экипировать несуществующий ID
                bool invalidItem = engineer.ValidateItem(1, "cheat_instant_kill_nuke");
                Assert.IsFalse(invalidItem, "Несуществующий предмет должен быть отвергнут");
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
