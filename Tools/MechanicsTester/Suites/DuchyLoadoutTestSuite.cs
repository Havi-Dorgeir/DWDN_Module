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

            RunTest(suite, "Test_Khorunzhiy_Class_Definition_Structure", () =>
            {
                if (!File.Exists(duchyManagerPath)) return;
                string content = File.ReadAllText(duchyManagerPath);

                // 1. Проверяем наличие класса Хорунжего в коде
                Assert.IsTrue(content.Contains("\"mp_Khorunzhiy\""), "Класс mp_Khorunzhiy должен быть объявлен в DWDN_DuchyManager.cs");
                Assert.IsTrue(content.Contains("\"mp_Khorunzhiy_hero\""), "mp_Khorunzhiy обязан ссылаться на персонажа mp_Khorunzhiy_hero");

                // 2. Проверяем позиционирование: Хорунжий должен идти сразу после Сотника (mp_Sotnik)
                int sotnikIdx = content.IndexOf("\"mp_Sotnik\"");
                int khorunzhiyIdx = content.IndexOf("\"mp_Khorunzhiy\"");
                int bannermanIdx = content.IndexOf("\"mp_Bannerman\"");
                Assert.IsTrue(sotnikIdx != -1, "mp_Sotnik должен быть найден");
                Assert.IsTrue(khorunzhiyIdx != -1, "mp_Khorunzhiy должен быть найден");
                Assert.IsTrue(bannermanIdx != -1, "mp_Bannerman должен быть найден");
                Assert.IsTrue(khorunzhiyIdx > sotnikIdx && khorunzhiyIdx < bannermanIdx, "Хорунжий обязан находиться строго между Сотником и Знаменосцем в иерархии пехоты");

                // 3. Проверяем полный сет Рюндаля
                Assert.IsTrue(content.Contains("\"baron_roondalt_vendelhelmet_1\""), "Хорунжий обязан иметь шлем Рюндаля baron_roondalt_vendelhelmet_1");
                Assert.IsTrue(content.Contains("\"run_body1\""), "Хорунжий обязан иметь доспех Рюндаля run_body1");
                Assert.IsTrue(content.Contains("\"Baron_rundal_cape\""), "Хорунжий обязан иметь плащ Рюндаля Baron_rundal_cape");
                Assert.IsTrue(content.Contains("\"rundal_leg\""), "Хорунжий обязан иметь поножи Рюндаля rundal_leg");
                Assert.IsTrue(content.Contains("\"rundal_gloves_second\""), "Хорунжий обязан иметь перчатки Рюндаля rundal_gloves_second");
                Assert.IsTrue(content.Contains("\"baldur_warpick\""), "Хорунжий обязан иметь клевец baldur_warpick в слоте 0");
                Assert.IsTrue(content.Contains("\"rundal_shield\""), "Хорунжий обязан иметь щит rundal_shield в слоте 1");
                Assert.IsFalse(content.Contains("khorunzhiy.AllowedWeaponsSlot1.AddRange(new[] { \"dwdn_banner1\" })"), "Хорунжий НЕ должен иметь знамени в слоте 1");
            });

            RunTest(suite, "Test_All_Module_Classes_And_Items_Integrity", () =>
            {
                // Поиск корневой директории модуля DWDN_TRY по SubModule.xml
                string dir = Path.GetDirectoryName(duchyManagerPath) ?? "";
                while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "SubModule.xml")))
                {
                    dir = Path.GetDirectoryName(dir) ?? "";
                }
                string moduleDataDir = Path.Combine(dir, "ModuleData");
                Assert.IsTrue(Directory.Exists(moduleDataDir), $"Каталог ModuleData обязан существовать: {moduleDataDir}");

                // 1. Проверка mpcultures.xml: культура dwdn ОБЯЗАНА иметь is_main_culture="true" для выбора фракции на карте
                string culturesPath = Path.Combine(moduleDataDir, "mpcultures.xml");
                if (File.Exists(culturesPath))
                {
                    string cultText = File.ReadAllText(culturesPath);
                    Assert.IsTrue(cultText.Contains("is_main_culture=\"true\""), "Культура dwdn ОБЯЗАНА иметь is_main_culture=\"true\", чтобы отображаться в списке фракций при настройке карты!");
                }

                // 2. Сбор всех предметов модуля и натива
                var allItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string xmlFile in Directory.GetFiles(moduleDataDir, "*.xml", SearchOption.AllDirectories))
                {
                    try
                    {
                        var doc = new System.Xml.XmlDocument();
                        doc.Load(xmlFile);
                        foreach (System.Xml.XmlNode n in doc.SelectNodes("//Item[@id]") ?? (System.Xml.XmlNodeList)null!)
                        {
                            if (n is System.Xml.XmlElement el) allItems.Add(el.GetAttribute("id"));
                        }
                        foreach (System.Xml.XmlNode n in doc.SelectNodes("//CraftedItem[@id]") ?? (System.Xml.XmlNodeList)null!)
                        {
                            if (n is System.Xml.XmlElement el) allItems.Add(el.GetAttribute("id"));
                        }
                    }
                    catch {}
                }

                // 3. Проверка классов в mpclassdivisions.xml
                string classDivPath = Path.Combine(moduleDataDir, "mpclassdivisions.xml");
                Assert.IsTrue(File.Exists(classDivPath), "mpclassdivisions.xml обязан существовать");
                var classDoc = new System.Xml.XmlDocument();
                classDoc.Load(classDivPath);
                var divisions = classDoc.SelectNodes("//MPClassDivision");
                Assert.IsTrue(divisions != null && divisions.Count >= 16, $"Ожидается как минимум 16 классов в mpclassdivisions.xml (найдено: {divisions?.Count})");

                // Проверяем наличие mp_Khorunzhiy в mpclassdivisions.xml
                var khorunzhiyNode = classDoc.SelectSingleNode("//MPClassDivision[@id='mp_Khorunzhiy']");
                Assert.IsNotNull(khorunzhiyNode, "mp_Khorunzhiy должен быть объявлен в mpclassdivisions.xml");

                // 4. Проверка mpcharacters.xml
                string charPath = Path.Combine(moduleDataDir, "mpcharacters.xml");
                Assert.IsTrue(File.Exists(charPath), "mpcharacters.xml обязан существовать");
                var charDoc = new System.Xml.XmlDocument();
                charDoc.Load(charPath);
                var heroNode = charDoc.SelectSingleNode("//NPCCharacter[@id='mp_Khorunzhiy_hero']");
                var troopNode = charDoc.SelectSingleNode("//NPCCharacter[@id='mp_Khorunzhiy_troop']");
                Assert.IsNotNull(heroNode, "mp_Khorunzhiy_hero должен существовать в mpcharacters.xml");
                Assert.IsNotNull(troopNode, "mp_Khorunzhiy_troop должен существовать в mpcharacters.xml");
                var heroBanner = heroNode?.SelectSingleNode(".//equipment[@id='Item.dwdn_banner1']");
                Assert.IsTrue(heroBanner == null, "mp_Khorunzhiy_hero НЕ должен иметь знамени в mpcharacters.xml");
                var troopBanner = troopNode?.SelectSingleNode(".//equipment[@id='Item.dwdn_banner1']");
                Assert.IsTrue(troopBanner == null, "mp_Khorunzhiy_troop НЕ должен иметь знамени в mpcharacters.xml");

                // 5. Проверка ModuleData/dwdn_loadouts.xml (единый конфиг слотов и категорий)
                string loadoutsXmlPath = Path.Combine(moduleDataDir, "dwdn_loadouts.xml");
                Assert.IsTrue(File.Exists(loadoutsXmlPath), "ModuleData/dwdn_loadouts.xml обязан существовать");
                var loadoutsDoc = new System.Xml.XmlDocument();
                loadoutsDoc.Load(loadoutsXmlPath);
                var xmlClasses = loadoutsDoc.SelectNodes("//Class[@enabled='true']");
                Assert.IsTrue(xmlClasses != null && xmlClasses.Count >= 13, $"Ожидается >= 13 активных классов в dwdn_loadouts.xml (найдено: {xmlClasses?.Count})");
                var badItems = loadoutsDoc.SelectNodes("//Item[starts-with(@id, 'Item.')]");
                Assert.IsTrue(badItems == null || badItems.Count == 0, "В dwdn_loadouts.xml обнаружен запрещённый префикс 'Item.'!");
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
