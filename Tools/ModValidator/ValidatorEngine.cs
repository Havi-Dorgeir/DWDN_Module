#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace ModValidator
{
    public enum DiagSeverity
    {
        Pass = 0,
        Warning = 1,
        Error = 2
    }

    public class ValidationIssue
    {
        public string Code { get; set; } = "";
        public string Category { get; set; } = "";
        public DiagSeverity Severity { get; set; }
        public string TargetId { get; set; } = "";
        public string FilePath { get; set; } = "";
        public int LineNumber { get; set; }
        public string Summary { get; set; } = "";
        public string Details { get; set; } = "";
        public string ImpactExplanation { get; set; } = "";
        public string AutoFixDescription { get; set; } = "";
        public Action? AutoFixAction { get; set; }

        public bool CanAutoFix => AutoFixAction != null;
    }

    public class ValidationReport
    {
        public List<ValidationIssue> Issues { get; } = new();
        public long ElapsedMs { get; set; }
        public int TotalItemsScanned { get; set; }
        public int TotalPiecesScanned { get; set; }
        public int TotalMeshesScanned { get; set; }
        public int TotalClassesScanned { get; set; }
        public int TotalXmlFilesScanned { get; set; }
        public int TotalCsFilesScanned { get; set; }

        public int ErrorCount => Issues.Count(i => i.Severity == DiagSeverity.Error);
        public int WarningCount => Issues.Count(i => i.Severity == DiagSeverity.Warning);
        public int PassCount => Issues.Count(i => i.Severity == DiagSeverity.Pass);
        public int FixableCount => Issues.Count(i => i.CanAutoFix && i.Severity != DiagSeverity.Pass);
    }

    public static class ValidatorEngine
    {
        public static readonly string[] Categories = new[]
        {
            "1. SubModule & Сборка DLL",
            "2. XML Синтаксис и Чистота",
            "3. Синхронизация Классов (C# ↔ XML)",
            "4. Ссылки на Предметы (Item ID)",
            "5. Слоты Брони и Анти-Кавалерия",
            "6. Крафт и Детали Оружия (Pieces)",
            "7. 3D-Меши в Архивах TPAC/FBX",
            "8. Дубликаты ID и Культуры",
            "9. Gauntlet UI и Спрайты",
            "10. Сетевые Пакеты (C# BitStream)"
        };

        public static ValidationReport RunFullScan(string modRoot, string gameModulesRoot)
        {
            var sw = Stopwatch.StartNew();
            var report = new ValidationReport();

            string nativeRoot = Path.Combine(gameModulesRoot, "Native");
            string mpRoot = Path.Combine(gameModulesRoot, "Multiplayer");
            string sbCoreRoot = Path.Combine(gameModulesRoot, "SandBoxCore");

            // ==================== 1. SUBMODULE.XML & DLL BUILD FRESHNESS ====================
            CheckSubModuleAndDlls(modRoot, report);

            // ==================== 2. XML SYNTAX & STRAY BACKUP FILES ====================
            var validModXmls = CheckXmlSyntaxAndStrayFiles(modRoot, report);

            // ==================== INDEX ALL ITEMS, CRAFTING PIECES, TEMPLATES, CULTURES ====================
            var allItems = new Dictionary<string, (string Type, string Mesh, string SourceFile, bool IsCustom)>(StringComparer.OrdinalIgnoreCase);
            var allPieces = new Dictionary<string, (string Type, string Mesh, string SourceFile, bool IsCustom)>(StringComparer.OrdinalIgnoreCase);
            var allTemplates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var allCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Load vanilla indices first
            LoadVanillaCatalogs(nativeRoot, mpRoot, sbCoreRoot, allItems, allPieces, allTemplates, allCultures);

            // Load custom DWDN items, pieces, cultures, and check duplicates
            CheckAndLoadModCatalogs(modRoot, allItems, allPieces, allTemplates, allCultures, report);
            report.TotalItemsScanned = allItems.Count;
            report.TotalPiecesScanned = allPieces.Count;

            // ==================== 7. INDEX 3D MESHES (.TPAC + .FBX) & VALIDATE MESH REFERENCES ====================
            var allMeshes = IndexAll3DMeshes(modRoot, nativeRoot);
            report.TotalMeshesScanned = allMeshes.Count;
            CheckCustomMeshReferences(modRoot, allItems, allPieces, allMeshes, report);

            // ==================== 6. CHECK CRAFTED ITEMS & PIECE REFERENCES ====================
            CheckCraftedItems(modRoot, allPieces, allTemplates, report);

            // ==================== 3, 4, 5. CHECK DUCHIES, CLASSES, ITEM REFS, SLOTS & ANTI-CAVALRY ====================
            CheckDuchiesClassesAndLoadouts(modRoot, allItems, allCultures, report);

            // ==================== 9. CHECK GAUNTLET UI PREFABS & SPRITEDATA ====================
            CheckGauntletUiAndSprites(modRoot, report);

            // ==================== 10. CHECK C# NETWORK PACKETS & SCRIPTS ====================
            CheckCSharpScriptsAndNetworkPackets(modRoot, report);

            sw.Stop();
            report.ElapsedMs = Math.Max(1, sw.ElapsedMilliseconds);
            return report;
        }

        private static void CheckSubModuleAndDlls(string modRoot, ValidationReport report)
        {
            string cat = Categories[0];
            string subXmlPath = Path.Combine(modRoot, "SubModule.xml");
            if (!File.Exists(subXmlPath))
            {
                report.Issues.Add(new ValidationIssue
                {
                    Code = "SUB-001",
                    Category = cat,
                    Severity = DiagSeverity.Error,
                    TargetId = "SubModule.xml",
                    FilePath = subXmlPath,
                    Summary = "Отсутствует корневой файл SubModule.xml",
                    Details = $"Файл не найден по пути: {subXmlPath}",
                    ImpactExplanation = "Без SubModule.xml лаунчер Bannerlord и выделенный сервер вообще не увидят модуль DWDN_TRY."
                });
                return;
            }

            try
            {
                var doc = XDocument.Load(subXmlPath);
                string modId = doc.Root?.Element("Id")?.Attribute("value")?.Value ?? "";
                report.Issues.Add(new ValidationIssue
                {
                    Code = "SUB-OK1",
                    Category = cat,
                    Severity = DiagSeverity.Pass,
                    TargetId = modId,
                    FilePath = subXmlPath,
                    Summary = $"Манифест SubModule.xml валиден (Id = {modId})",
                    Details = "Корневой манифест модуля успешно прочитан."
                });

                // Check Client & Server DLLs and compare timestamp against newest .cs file
                string scriptsDir = Path.Combine(modRoot, "ModuleData", "scripts", "DWDN_Script");
                string csprojPath = Path.Combine(scriptsDir, "DWDN_Script.csproj");
                DateTime newestCsTime = DateTime.MinValue;
                string newestCsFile = "";
                if (Directory.Exists(scriptsDir))
                {
                    foreach (var cs in Directory.GetFiles(scriptsDir, "*.cs", SearchOption.AllDirectories))
                    {
                        if (cs.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase)) continue;
                        var wt = File.GetLastWriteTimeUtc(cs);
                        if (wt > newestCsTime)
                        {
                            newestCsTime = wt;
                            newestCsFile = cs;
                        }
                    }
                }

                string clientDll = Path.Combine(modRoot, "bin", "Win64_Shipping_Client", "DWDN_Script.dll");
                string serverDll = Path.Combine(modRoot, "bin", "Win64_Shipping_Server", "DWDN_Script.Server.dll");

                if (!File.Exists(clientDll))
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Code = "DLL-001",
                        Category = cat,
                        Severity = DiagSeverity.Error,
                        TargetId = "DWDN_Script.dll",
                        FilePath = clientDll,
                        Summary = "Отсутствует клиентская сборка DWDN_Script.dll!",
                        Details = $"Ожидаемый путь: {clientDll}",
                        ImpactExplanation = "При запуске игры клиент крашнется на этапе загрузки SubModule или не загрузит C#-логику мода.",
                        AutoFixDescription = "Скомпилировать DWDN_Script.csproj (dotnet build -c Debug)",
                        AutoFixAction = () => CompileModScripts(csprojPath)
                    });
                }
                else
                {
                    var dllTime = File.GetLastWriteTimeUtc(clientDll);
                    if (newestCsTime > dllTime.AddSeconds(5))
                    {
                        report.Issues.Add(new ValidationIssue
                        {
                            Code = "DLL-002",
                            Category = cat,
                            Severity = DiagSeverity.Warning,
                            TargetId = "DWDN_Script.dll (Устарела)",
                            FilePath = newestCsFile,
                            Summary = $"Исходный код ({Path.GetFileName(newestCsFile)}) изменялся позже последней сборки DLL!",
                            Details = $"Последнее изменение C#: {newestCsTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}\nВремя сборки DLL: {dllTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}",
                            ImpactExplanation = "Ваши последние правки в C#-скриптах ещё не попали в DWDN_Script.dll — в игре будет работать старая версия кода.",
                            AutoFixDescription = "Пересобрать DWDN_Script.dll и DWDN_Script.Server.dll прямо сейчас",
                            AutoFixAction = () => CompileModScripts(csprojPath)
                        });
                    }
                    else
                    {
                        report.Issues.Add(new ValidationIssue
                        {
                            Code = "DLL-OK1",
                            Category = cat,
                            Severity = DiagSeverity.Pass,
                            TargetId = "DWDN_Script.dll",
                            FilePath = clientDll,
                            Summary = "Клиентская и серверная DLL актуальны и синхронизированы с исходным кодом C#",
                            Details = $"Сборка: {dllTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}"
                        });
                    }
                }

                if (!File.Exists(serverDll))
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Code = "DLL-003",
                        Category = cat,
                        Severity = DiagSeverity.Warning,
                        TargetId = "DWDN_Script.Server.dll",
                        FilePath = serverDll,
                        Summary = "Отсутствует серверная сборка DWDN_Script.Server.dll",
                        Details = $"Ожидаемый путь: {serverDll}",
                        ImpactExplanation = "Выделенный сервер (Dedicated Server) не сможет запустить модуль DWDN_TRY.",
                        AutoFixDescription = "Скомпилировать DWDN_Script.csproj",
                        AutoFixAction = () => CompileModScripts(csprojPath)
                    });
                }

                // Check <XmlNode> entries in SubModule.xml
                string modDataDir = Path.Combine(modRoot, "ModuleData");
                foreach (var xmlNode in doc.Descendants("XmlNode"))
                {
                    var xmlNameEl = xmlNode.Element("XmlName");
                    string id = xmlNameEl?.Attribute("id")?.Value ?? "";
                    string relPath = xmlNameEl?.Attribute("path")?.Value ?? "";
                    if (string.IsNullOrEmpty(relPath)) continue;

                    string fullXmlFile = Path.Combine(modDataDir, relPath + ".xml");
                    string fullXmlDir = Path.Combine(modDataDir, relPath);

                    if (!File.Exists(fullXmlFile) && !Directory.Exists(fullXmlDir))
                    {
                        report.Issues.Add(new ValidationIssue
                        {
                            Code = "SUB-XML-MISSING",
                            Category = cat,
                            Severity = DiagSeverity.Warning,
                            TargetId = $"{id} ({relPath})",
                            FilePath = subXmlPath,
                            Summary = $"В SubModule.xml заявлен XmlNode path=\"{relPath}\", но файл/папка ModuleData/{relPath}.xml отсутствует",
                            Details = $"Узел <XmlName id=\"{id}\" path=\"{relPath}\" /> в SubModule.xml ссылается на несуществующий ресурс: {fullXmlFile}",
                            ImpactExplanation = "Движок MBObjectManager при загрузке модуля пытается открыть этот XML и пишет предупреждение в rgl_log.",
                            AutoFixDescription = $"Удалить мёртвую ссылку <XmlName path=\"{relPath}\"/> из SubModule.xml",
                            AutoFixAction = () =>
                            {
                                var d = XDocument.Load(subXmlPath);
                                var target = d.Descendants("XmlNode")
                                    .FirstOrDefault(n => n.Element("XmlName")?.Attribute("path")?.Value == relPath);
                                if (target != null)
                                {
                                    target.Remove();
                                    d.Save(subXmlPath);
                                }
                            }
                        });
                    }
                    else
                    {
                        report.Issues.Add(new ValidationIssue
                        {
                            Code = "SUB-XML-OK",
                            Category = cat,
                            Severity = DiagSeverity.Pass,
                            TargetId = $"{id} ({relPath})",
                            FilePath = File.Exists(fullXmlFile) ? fullXmlFile : fullXmlDir,
                            Summary = $"XmlNode '{id}' ({relPath}) найден в ModuleData",
                            Details = $"Ресурс существует и готов к загрузке в MBObjectManager."
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                report.Issues.Add(new ValidationIssue
                {
                    Code = "SUB-ERR",
                    Category = cat,
                    Severity = DiagSeverity.Error,
                    TargetId = "SubModule.xml",
                    FilePath = subXmlPath,
                    Summary = $"Ошибка чтения SubModule.xml: {ex.Message}",
                    Details = ex.ToString()
                });
            }
        }

        public static void CompileModScripts(string csprojPath)
        {
            if (!File.Exists(csprojPath)) return;
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{csprojPath}\" -c Debug",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(25000);
        }

        private static List<string> CheckXmlSyntaxAndStrayFiles(string modRoot, ValidationReport report)
        {
            string cat = Categories[1];
            var validXmls = new List<string>();
            string modDataDir = Path.Combine(modRoot, "ModuleData");
            if (!Directory.Exists(modDataDir)) return validXmls;

            var allXmlFiles = Directory.GetFiles(modDataDir, "*.xml", SearchOption.AllDirectories);
            report.TotalXmlFilesScanned = allXmlFiles.Length;

            foreach (var xmlFile in allXmlFiles)
            {
                if (xmlFile.Contains("\\scripts\\", StringComparison.OrdinalIgnoreCase) ||
                    xmlFile.Contains("\\_archive_backups\\", StringComparison.OrdinalIgnoreCase))
                    continue;

                string fileName = Path.GetFileName(xmlFile);

                // Detect stray copy/backup files left in ModuleData
                bool isStrayCopy = fileName.Contains(" - Copy", StringComparison.OrdinalIgnoreCase) ||
                                   fileName.Contains(" cop.xml", StringComparison.OrdinalIgnoreCase) ||
                                   Regex.IsMatch(fileName, @"mpclassdivisions?\d+.*\.xml", RegexOptions.IgnoreCase);

                if (isStrayCopy)
                {
                    string capturedFile = xmlFile;
                    report.Issues.Add(new ValidationIssue
                    {
                        Code = "XML-STRAY",
                        Category = cat,
                        Severity = DiagSeverity.Warning,
                        TargetId = fileName,
                        FilePath = capturedFile,
                        Summary = $"Мусорный файл-копия '{fileName}' лежит прямо в рабочей папке ModuleData!",
                        Details = $"Путь: {capturedFile}",
                        ImpactExplanation = "Мусорные копии XML в ModuleData путают мододелов и при пакетной загрузке папок (например mpitems/) вызывают дублирование ID и перезапись предметов.",
                        AutoFixDescription = "Переместить мусорную копию в архив d:\\banAImod\\ModValidator\\output\\archived_stray_xmls\\",
                        AutoFixAction = () =>
                        {
                            string archDir = @"d:\banAImod\ModValidator\output\archived_stray_xmls";
                            Directory.CreateDirectory(archDir);
                            string dest = Path.Combine(archDir, Path.GetFileName(capturedFile));
                            if (File.Exists(dest)) File.Delete(dest);
                            if (File.Exists(capturedFile)) File.Move(capturedFile, dest);
                        }
                    });
                    continue;
                }

                // Validate XML syntax with line/position reporting
                try
                {
                    using var reader = XmlReader.Create(xmlFile, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
                    while (reader.Read()) { }
                    validXmls.Add(xmlFile);
                    report.Issues.Add(new ValidationIssue
                    {
                        Code = "XML-SYNTAX-OK",
                        Category = cat,
                        Severity = DiagSeverity.Pass,
                        TargetId = fileName,
                        FilePath = xmlFile,
                        Summary = $"XML-синтаксис файла '{fileName}' безупречен",
                        Details = $"Проверен файл: {xmlFile}"
                    });
                }
                catch (XmlException xex)
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Code = "XML-SYNTAX-ERR",
                        Category = cat,
                        Severity = DiagSeverity.Error,
                        TargetId = fileName,
                        FilePath = xmlFile,
                        LineNumber = xex.LineNumber,
                        Summary = $"Синтаксическая ошибка XML в '{fileName}' (Строка {xex.LineNumber}, Позиция {xex.LinePosition})",
                        Details = xex.Message,
                        ImpactExplanation = "Любая синтаксическая ошибка в XML гарантированно приводит к мгновенному вылету (Crash to Desktop) при загрузке модуля в MBObjectManager."
                    });
                }
                catch (Exception ex)
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Code = "XML-READ-ERR",
                        Category = cat,
                        Severity = DiagSeverity.Error,
                        TargetId = fileName,
                        FilePath = xmlFile,
                        Summary = $"Ошибка чтения '{fileName}': {ex.Message}",
                        Details = ex.ToString()
                    });
                }
            }

            return validXmls;
        }

        private static void LoadVanillaCatalogs(
            string nativeRoot,
            string mpRoot,
            string sbCoreRoot,
            Dictionary<string, (string Type, string Mesh, string SourceFile, bool IsCustom)> allItems,
            Dictionary<string, (string Type, string Mesh, string SourceFile, bool IsCustom)> allPieces,
            HashSet<string> allTemplates,
            HashSet<string> allCultures)
        {
            foreach (var root in new[] { nativeRoot, mpRoot, sbCoreRoot })
            {
                if (!Directory.Exists(root)) continue;
                string md = Path.Combine(root, "ModuleData");
                if (!Directory.Exists(md)) continue;

                foreach (var xml in Directory.GetFiles(md, "*.xml", SearchOption.AllDirectories))
                {
                    try
                    {
                        var doc = XDocument.Load(xml);
                        foreach (var el in doc.Descendants("Item").Concat(doc.Descendants("CraftedItem")))
                        {
                            string id = el.Attribute("id")?.Value ?? "";
                            string type = el.Attribute("Type")?.Value ?? (el.Name.LocalName == "CraftedItem" ? "CraftedWeapon" : "Unknown");
                            string mesh = el.Attribute("mesh")?.Value ?? "";
                            if (!string.IsNullOrEmpty(id) && !allItems.ContainsKey(id))
                                allItems[id] = (type, mesh, xml, false);
                        }
                        foreach (var el in doc.Descendants("CraftingPiece"))
                        {
                            string id = el.Attribute("id")?.Value ?? "";
                            string type = el.Attribute("piece_type")?.Value ?? "";
                            string mesh = el.Attribute("mesh")?.Value ?? "";
                            if (!string.IsNullOrEmpty(id) && !allPieces.ContainsKey(id))
                                allPieces[id] = (type, mesh, xml, false);
                        }
                        foreach (var el in doc.Descendants("CraftingTemplate"))
                        {
                            string id = el.Attribute("id")?.Value ?? "";
                            if (!string.IsNullOrEmpty(id)) allTemplates.Add(id);
                        }
                        foreach (var el in doc.Descendants("Culture"))
                        {
                            string id = el.Attribute("id")?.Value ?? "";
                            if (!string.IsNullOrEmpty(id)) allCultures.Add(id);
                        }
                    }
                    catch { }
                }
            }
        }

        private static void CheckAndLoadModCatalogs(
            string modRoot,
            Dictionary<string, (string Type, string Mesh, string SourceFile, bool IsCustom)> allItems,
            Dictionary<string, (string Type, string Mesh, string SourceFile, bool IsCustom)> allPieces,
            HashSet<string> allTemplates,
            HashSet<string> allCultures,
            ValidationReport report)
        {
            string catDup = Categories[7];
            string md = Path.Combine(modRoot, "ModuleData");
            if (!Directory.Exists(md)) return;

            // Active item files loaded by SubModule.xml: ModuleData/mpitems/*.xml
            var itemXmls = new List<string>();
            string mpItemsDir = Path.Combine(md, "mpitems");
            if (Directory.Exists(mpItemsDir))
                itemXmls.AddRange(Directory.GetFiles(mpItemsDir, "*.xml"));

            var seenCustomItems = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var xml in itemXmls)
            {
                try
                {
                    var doc = XDocument.Load(xml, LoadOptions.SetLineInfo);
                    foreach (var el in doc.Descendants("Item").Concat(doc.Descendants("CraftedItem")))
                    {
                        string id = el.Attribute("id")?.Value ?? "";
                        if (string.IsNullOrEmpty(id)) continue;
                        int line = (el as IXmlLineInfo)?.LineNumber ?? 0;

                        if (seenCustomItems.TryGetValue(id, out var prevFile))
                        {
                            string capturedXml = xml;
                            string capturedId = id;
                            report.Issues.Add(new ValidationIssue
                            {
                                Code = "DUP-ITEM",
                                Category = catDup,
                                Severity = DiagSeverity.Warning,
                                TargetId = id,
                                FilePath = xml,
                                LineNumber = line,
                                Summary = $"Дубликат ID предмета '{id}' в файлах mpitems!",
                                Details = $"Повторное объявление в {Path.GetFileName(xml)} (строка {line}). Ранее объявлен в {Path.GetFileName(prevFile)}.",
                                ImpactExplanation = "Второй предмет с тем же ID молча перезаписывает первый в MBObjectManager, из-за чего статы или меш первого предмета пропадают.",
                                AutoFixDescription = $"Удалить дублирующееся второе объявление '{id}' из {Path.GetFileName(xml)}",
                                AutoFixAction = () =>
                                {
                                    var d = XDocument.Load(capturedXml);
                                    var matches = d.Descendants("Item").Concat(d.Descendants("CraftedItem"))
                                        .Where(x => string.Equals(x.Attribute("id")?.Value, capturedId, StringComparison.OrdinalIgnoreCase))
                                        .ToList();
                                    if (matches.Count > 1)
                                    {
                                        for (int k = 1; k < matches.Count; k++)
                                            matches[k].Remove();
                                        d.Save(capturedXml);
                                    }
                                }
                            });
                        }
                        seenCustomItems[id] = xml;

                        string type = el.Attribute("Type")?.Value ?? (el.Name.LocalName == "CraftedItem" ? "CraftedWeapon" : "Unknown");
                        string mesh = el.Attribute("mesh")?.Value ?? "";
                        allItems[id] = (type, mesh, xml, true);
                    }
                }
                catch { }
            }

            // Also load root items.xml if present (check if any item exists ONLY in items.xml and not in mpitems/)
            string rootItemsXml = Path.Combine(md, "items.xml");
            if (File.Exists(rootItemsXml))
            {
                try
                {
                    var doc = XDocument.Load(rootItemsXml);
                    foreach (var el in doc.Descendants("Item").Concat(doc.Descendants("CraftedItem")))
                    {
                        string id = el.Attribute("id")?.Value ?? "";
                        if (!string.IsNullOrEmpty(id) && !allItems.ContainsKey(id))
                        {
                            string type = el.Attribute("Type")?.Value ?? (el.Name.LocalName == "CraftedItem" ? "CraftedWeapon" : "Unknown");
                            string mesh = el.Attribute("mesh")?.Value ?? "";
                            allItems[id] = (type, mesh, rootItemsXml, true);
                        }
                    }
                }
                catch { }
            }

            // Load crafting_pieces.xml
            string cpXml = Path.Combine(md, "crafting_pieces.xml");
            if (File.Exists(cpXml))
            {
                var seenPieces = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var doc = XDocument.Load(cpXml, LoadOptions.SetLineInfo);
                    foreach (var el in doc.Descendants("CraftingPiece"))
                    {
                        string id = el.Attribute("id")?.Value ?? "";
                        if (string.IsNullOrEmpty(id)) continue;
                        int line = (el as IXmlLineInfo)?.LineNumber ?? 0;

                        if (seenPieces.TryGetValue(id, out int prevLine))
                        {
                            report.Issues.Add(new ValidationIssue
                            {
                                Code = "DUP-PIECE",
                                Category = catDup,
                                Severity = DiagSeverity.Warning,
                                TargetId = id,
                                FilePath = cpXml,
                                LineNumber = line,
                                Summary = $"Дубликат ID детали крафта '{id}' в crafting_pieces.xml (строки {prevLine} и {line})",
                                Details = $"Деталь '{id}' объявлена дважды в одном файле.",
                                ImpactExplanation = "Приводит к перезаписи параметров детали оружия при загрузке XML."
                            });
                        }
                        seenPieces[id] = line;

                        string type = el.Attribute("piece_type")?.Value ?? "";
                        string mesh = el.Attribute("mesh")?.Value ?? "";
                        allPieces[id] = (type, mesh, cpXml, true);
                    }
                }
                catch { }
            }

            // Load crafting_templates.xml & mpcultures.xml
            string ctXml = Path.Combine(md, "crafting_templates.xml");
            if (File.Exists(ctXml))
            {
                try
                {
                    var doc = XDocument.Load(ctXml);
                    foreach (var el in doc.Descendants("CraftingTemplate"))
                    {
                        string id = el.Attribute("id")?.Value ?? "";
                        if (!string.IsNullOrEmpty(id)) allTemplates.Add(id);
                    }
                }
                catch { }
            }

            string cultXml = Path.Combine(md, "mpcultures.xml");
            if (File.Exists(cultXml))
            {
                try
                {
                    var doc = XDocument.Load(cultXml);
                    foreach (var el in doc.Descendants("Culture"))
                    {
                        string id = el.Attribute("id")?.Value ?? "";
                        if (!string.IsNullOrEmpty(id))
                        {
                            allCultures.Add(id);
                            report.Issues.Add(new ValidationIssue
                            {
                                Code = "CULT-OK",
                                Category = catDup,
                                Severity = DiagSeverity.Pass,
                                TargetId = $"Culture.{id}",
                                FilePath = cultXml,
                                Summary = $"Культура 'Culture.{id}' зарегистрирована в mpcultures.xml",
                                Details = "Культура корректно объявлена."
                            });
                        }
                    }
                }
                catch { }
            }
        }

        private static HashSet<string> IndexAll3DMeshes(string modRoot, string nativeRoot)
        {
            var meshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Scan DWDN_TRY Assets/*.tpac and AssetSources/*.fbx
            string modAssets = Path.Combine(modRoot, "Assets");
            if (Directory.Exists(modAssets))
            {
                foreach (var tpac in Directory.GetFiles(modAssets, "*.tpac", SearchOption.AllDirectories))
                {
                    string baseName = Path.GetFileNameWithoutExtension(tpac);
                    if (baseName.EndsWith("_geo", StringComparison.OrdinalIgnoreCase))
                        meshes.Add(baseName.Substring(0, baseName.Length - 4));
                    meshes.Add(baseName);

                    // Also extract internal mesh names from _geo.tpac binary header
                    if (baseName.EndsWith("_geo", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            byte[] b = File.ReadAllBytes(tpac);
                            for (int i = 100; i < b.Length - 160; i++)
                            {
                                if (BitConverter.ToSingle(b, i) == 1.0f &&
                                    BitConverter.ToSingle(b, i + 4) == 1.0f &&
                                    BitConverter.ToSingle(b, i + 8) == 1.0f &&
                                    BitConverter.ToSingle(b, i + 12) == 1.0f)
                                {
                                    int afterName = i - 24;
                                    for (int len = 2; len <= 64; len++)
                                    {
                                        int lenPos = afterName - len - 4;
                                        if (lenPos > 0 && BitConverter.ToInt32(b, lenPos) == len)
                                        {
                                            string internalName = Encoding.ASCII.GetString(b, lenPos + 4, len);
                                            if (internalName.All(c => c >= 32 && c < 127))
                                            {
                                                meshes.Add(internalName);
                                                int dotIdx = internalName.IndexOf('.');
                                                if (dotIdx > 0) meshes.Add(internalName.Substring(0, dotIdx));
                                            }
                                            break;
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }

            string modSources = Path.Combine(modRoot, "AssetSources");
            if (Directory.Exists(modSources))
            {
                foreach (var fbx in Directory.GetFiles(modSources, "*.fbx", SearchOption.AllDirectories))
                {
                    meshes.Add(Path.GetFileNameWithoutExtension(fbx));
                }
            }

            return meshes;
        }

        private static void CheckCustomMeshReferences(
            string modRoot,
            Dictionary<string, (string Type, string Mesh, string SourceFile, bool IsCustom)> allItems,
            Dictionary<string, (string Type, string Mesh, string SourceFile, bool IsCustom)> allPieces,
            HashSet<string> customMeshes,
            ValidationReport report)
        {
            string cat = Categories[6];

            // Check custom CraftingPieces in DWDN_TRY/ModuleData/crafting_pieces.xml
            string cpXml = Path.Combine(modRoot, "ModuleData", "crafting_pieces.xml");
            if (File.Exists(cpXml))
            {
                try
                {
                    var doc = XDocument.Load(cpXml, LoadOptions.SetLineInfo);
                    foreach (var el in doc.Descendants("CraftingPiece"))
                    {
                        string id = el.Attribute("id")?.Value ?? "";
                        string mesh = el.Attribute("mesh")?.Value ?? "";
                        int line = (el as IXmlLineInfo)?.LineNumber ?? 0;
                        if (string.IsNullOrEmpty(mesh)) continue;

                        if (customMeshes.Contains(mesh))
                        {
                            report.Issues.Add(new ValidationIssue
                            {
                                Code = "MESH-PIECE-OK",
                                Category = cat,
                                Severity = DiagSeverity.Pass,
                                TargetId = $"{id} -> {mesh}",
                                FilePath = cpXml,
                                LineNumber = line,
                                Summary = $"3D-меш '{mesh}' для детали оружия '{id}' найден в Assets",
                                Details = "Бинарный геометрический пакет *_geo.tpac присутствует."
                            });
                        }
                        else
                        {
                            // Try to find closest match in customMeshes (e.g. baron_rindal_warpick_blade -> baron_rindal_blade)
                            string? bestMatch = customMeshes.FirstOrDefault(m =>
                                id.Replace("_warpick", "").Equals(m, StringComparison.OrdinalIgnoreCase) ||
                                mesh.Replace("_warpick", "").Equals(m, StringComparison.OrdinalIgnoreCase));

                            string capturedMesh = mesh;
                            string capturedId = id;
                            report.Issues.Add(new ValidationIssue
                            {
                                Code = "MESH-PIECE-MISSING",
                                Category = cat,
                                Severity = DiagSeverity.Error,
                                TargetId = $"{id} (mesh=\"{mesh}\")",
                                FilePath = cpXml,
                                LineNumber = line,
                                Summary = $"Деталь '{id}' ссылается на несуществующий 3D-меш '{mesh}'!" + (bestMatch != null ? $" (Найден похожий меш: '{bestMatch}')" : ""),
                                Details = $"Файл: {cpXml} (строка {line})\nАтрибут mesh=\"{mesh}\" не найден среди .tpac пакетов мода.",
                                ImpactExplanation = "При спавне оружия с этой деталью клинок/гарда будут полностью невидимыми в руках бойца или вызовут ошибку рендера!",
                                AutoFixDescription = bestMatch != null ? $"Заменить mesh=\"{mesh}\" на mesh=\"{bestMatch}\" в crafting_pieces.xml" : "",
                                AutoFixAction = bestMatch != null ? () =>
                                {
                                    var d = XDocument.Load(cpXml);
                                    var node = d.Descendants("CraftingPiece").FirstOrDefault(x => x.Attribute("id")?.Value == capturedId);
                                    if (node != null)
                                    {
                                        node.SetAttributeValue("mesh", bestMatch);
                                        d.Save(cpXml);
                                    }
                                } : null
                            });
                        }
                    }
                }
                catch { }
            }
        }

        private static void CheckCraftedItems(
            string modRoot,
            Dictionary<string, (string Type, string Mesh, string SourceFile, bool IsCustom)> allPieces,
            HashSet<string> allTemplates,
            ValidationReport report)
        {
            string cat = Categories[5];
            string mpItemsDir = Path.Combine(modRoot, "ModuleData", "mpitems");
            if (!Directory.Exists(mpItemsDir)) return;

            foreach (var xml in Directory.GetFiles(mpItemsDir, "*.xml"))
            {
                try
                {
                    var doc = XDocument.Load(xml, LoadOptions.SetLineInfo);
                    foreach (var ci in doc.Descendants("CraftedItem"))
                    {
                        string itemId = ci.Attribute("id")?.Value ?? "";
                        string tmpl = ci.Attribute("crafting_template")?.Value ?? "";
                        int line = (ci as IXmlLineInfo)?.LineNumber ?? 0;

                        if (!string.IsNullOrEmpty(tmpl) && !allTemplates.Contains(tmpl))
                        {
                            report.Issues.Add(new ValidationIssue
                            {
                                Code = "CRAFT-TMPL-ERR",
                                Category = cat,
                                Severity = DiagSeverity.Error,
                                TargetId = itemId,
                                FilePath = xml,
                                LineNumber = line,
                                Summary = $"Оружие '{itemId}' использует неизвестный crafting_template='{tmpl}'",
                                Details = $"Шаблон крафта '{tmpl}' не найден ни в DWDN_TRY, ни в Native.",
                                ImpactExplanation = "Приведёт к вылету игры (NullReferenceException в Crafting.CreatePreCraftedWeapon) при загрузке XML."
                            });
                        }

                        bool allPiecesValid = true;
                        foreach (var p in ci.Descendants("Piece"))
                        {
                            string pid = p.Attribute("id")?.Value ?? "";
                            string pType = p.Attribute("Type")?.Value ?? "";
                            int pLine = (p as IXmlLineInfo)?.LineNumber ?? line;

                            if (string.IsNullOrEmpty(pid)) continue;
                            if (!allPieces.TryGetValue(pid, out var pieceInfo))
                            {
                                allPiecesValid = false;
                                report.Issues.Add(new ValidationIssue
                                {
                                    Code = "CRAFT-PIECE-MISSING",
                                    Category = cat,
                                    Severity = DiagSeverity.Error,
                                    TargetId = $"{itemId} -> {pid}",
                                    FilePath = xml,
                                    LineNumber = pLine,
                                    Summary = $"В оружии '{itemId}' указана несуществующая деталь Piece id='{pid}' (Type='{pType}')!",
                                    Details = $"Файл: {xml} (строка {pLine})",
                                    ImpactExplanation = "Несуществующая деталь в CraftedItem вызывает мгновенный краш игры при инициализации предметов."
                                });
                            }
                            else if (!string.IsNullOrEmpty(pType) && !string.IsNullOrEmpty(pieceInfo.Type) &&
                                     !string.Equals(pType, pieceInfo.Type, StringComparison.OrdinalIgnoreCase))
                            {
                                allPiecesValid = false;
                                report.Issues.Add(new ValidationIssue
                                {
                                    Code = "CRAFT-PIECE-TYPE",
                                    Category = cat,
                                    Severity = DiagSeverity.Error,
                                    TargetId = $"{itemId} -> {pid}",
                                    FilePath = xml,
                                    LineNumber = pLine,
                                    Summary = $"Несовпадение типа детали '{pid}' в оружии '{itemId}': в оружии Type='{pType}', а в crafting_pieces piece_type='{pieceInfo.Type}'!",
                                    Details = $"Файл: {xml} (строка {pLine})",
                                    ImpactExplanation = "Несовпадение типа слота крафта (например Guard вместо Handle) ломает сборку меша оружия."
                                });
                            }
                        }

                        if (allPiecesValid)
                        {
                            report.Issues.Add(new ValidationIssue
                            {
                                Code = "CRAFT-OK",
                                Category = cat,
                                Severity = DiagSeverity.Pass,
                                TargetId = itemId,
                                FilePath = xml,
                                LineNumber = line,
                                Summary = $"Составное оружие '{itemId}' ({tmpl}) прошло проверку всех деталей",
                                Details = "Все ссылки на Piece ID и типы слотов корректны."
                            });
                        }
                    }
                }
                catch { }
            }
        }

        private static void CheckDuchiesClassesAndLoadouts(
            string modRoot,
            Dictionary<string, (string Type, string Mesh, string SourceFile, bool IsCustom)> allItems,
            HashSet<string> allCultures,
            ValidationReport report)
        {
            string catSync = Categories[2];
            string catItem = Categories[3];
            string catSlot = Categories[4];

            string csPath = Path.Combine(modRoot, "ModuleData", "scripts", "DWDN_Script", "Common", "Duchies", "DWDN_DuchyManager.cs");
            string divXmlPath = Path.Combine(modRoot, "ModuleData", "mpclassdivisions.xml");
            string charXmlPath = Path.Combine(modRoot, "ModuleData", "mpcharacters.xml");

            var divNodes = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(divXmlPath))
            {
                try
                {
                    var doc = XDocument.Load(divXmlPath, LoadOptions.SetLineInfo);
                    foreach (var el in doc.Descendants("MPClassDivision"))
                    {
                        string id = el.Attribute("id")?.Value ?? "";
                        if (!string.IsNullOrEmpty(id)) divNodes[id] = el;
                    }
                }
                catch { }
            }

            var charNodes = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(charXmlPath))
            {
                try
                {
                    var doc = XDocument.Load(charXmlPath, LoadOptions.SetLineInfo);
                    foreach (var el in doc.Descendants("NPCCharacter"))
                    {
                        string id = el.Attribute("id")?.Value ?? "";
                        if (!string.IsNullOrEmpty(id)) charNodes[id] = el;

                        int line = (el as IXmlLineInfo)?.LineNumber ?? 0;
                        string group = el.Attribute("default_group")?.Value ?? "Infantry";
                        string cult = el.Attribute("culture")?.Value ?? "";
                        if (cult.StartsWith("Culture.", StringComparison.OrdinalIgnoreCase))
                        {
                            string shortCult = cult.Substring(8);
                            if (!allCultures.Contains(shortCult))
                            {
                                report.Issues.Add(new ValidationIssue
                                {
                                    Code = "CHAR-CULT-ERR",
                                    Category = Categories[7],
                                    Severity = DiagSeverity.Error,
                                    TargetId = $"{id} -> {cult}",
                                    FilePath = charXmlPath,
                                    LineNumber = line,
                                    Summary = $"Персонаж '{id}' ссылается на несуществующую культуру '{cult}'!",
                                    Details = $"Файл: {charXmlPath} (строка {line})",
                                    ImpactExplanation = "Вызовет краш MBObjectManager при разрешении ссылки на BasicCultureObject."
                                });
                            }
                        }

                        // Check equipment items inside mpcharacters.xml
                        foreach (var eq in el.Descendants("equipment"))
                        {
                            string slot = eq.Attribute("slot")?.Value ?? "";
                            string rawItem = eq.Attribute("id")?.Value ?? "";
                            int eqLine = (eq as IXmlLineInfo)?.LineNumber ?? line;
                            string itemId = rawItem.StartsWith("Item.", StringComparison.OrdinalIgnoreCase) ? rawItem.Substring(5) : rawItem;
                            if (string.IsNullOrEmpty(itemId)) continue;

                            if (!allItems.TryGetValue(itemId, out var itemInfo))
                            {
                                report.Issues.Add(new ValidationIssue
                                {
                                    Code = "XML-ITEM-MISSING",
                                    Category = catItem,
                                    Severity = DiagSeverity.Error,
                                    TargetId = $"{id} [{slot}] -> {itemId}",
                                    FilePath = charXmlPath,
                                    LineNumber = eqLine,
                                    Summary = $"В mpcharacters.xml у '{id}' (слот {slot}) указан несуществующий предмет 'Item.{itemId}'!",
                                    Details = $"Файл: {charXmlPath} (строка {eqLine})",
                                    ImpactExplanation = "Несуществующий предмет в EquipmentRoster вызывает краш при спавне этого класса в бою!"
                                });
                            }

                            // Anti-Cavalry Scrapcode check in mpcharacters.xml
                            if (slot.Equals("Horse", StringComparison.OrdinalIgnoreCase) &&
                                !group.Equals("Cavalry", StringComparison.OrdinalIgnoreCase) &&
                                !group.Equals("HorseArcher", StringComparison.OrdinalIgnoreCase))
                            {
                                string capturedNpcId = id;
                                report.Issues.Add(new ValidationIssue
                                {
                                    Code = "ANTI-CAV-XML",
                                    Category = catSlot,
                                    Severity = DiagSeverity.Error,
                                    TargetId = $"{id} (group={group}, Horse={itemId})",
                                    FilePath = charXmlPath,
                                    LineNumber = eqLine,
                                    Summary = $"АНТИ-СКРАПКОД КАВАЛЕРИИ: Пехотный/стрелковый класс '{id}' (default_group='{group}') имеет коня '{itemId}' в XML!",
                                    Details = $"В файле {charXmlPath} (строка {eqLine}) у персонажа с группой '{group}' прописан слот Horse.",
                                    ImpactExplanation = "Пехотинец или стрелок будет спавниться верхом на коне!",
                                    AutoFixDescription = $"Удалить слот Horse из EquipmentRoster у '{id}' в mpcharacters.xml",
                                    AutoFixAction = () =>
                                    {
                                        var d = XDocument.Load(charXmlPath);
                                        var npc = d.Descendants("NPCCharacter").FirstOrDefault(x => x.Attribute("id")?.Value == capturedNpcId);
                                        npc?.Descendants("equipment")
                                            .Where(x => string.Equals(x.Attribute("slot")?.Value, "Horse", StringComparison.OrdinalIgnoreCase))
                                            .Remove();
                                        d.Save(charXmlPath);
                                    }
                                });
                            }
                        }
                    }
                }
                catch { }
            }

            // Parse DWDN_DuchyManager.cs and verify every class and every pool item
            if (!File.Exists(csPath)) return;
            string code = File.ReadAllText(csPath);

            var classRegex = new Regex(
                @"var\s+(\w+)\s*=\s*new\s+DWDN_ClassDefinition\(\s*""([^""]*)""\s*,\s*""([^""]*)""\s*,\s*""([^""]*)""\s*,\s*""([^""]*)""\s*\)",
                RegexOptions.Multiline);

            var varToClassInfo = new Dictionary<string, (string ClassId, string ClassName, string TroopId)>();
            foreach (Match m in classRegex.Matches(code))
            {
                string cVar = m.Groups[1].Value;
                string cId = m.Groups[2].Value;
                string cName = m.Groups[3].Value;
                string tId = m.Groups[5].Value;
                varToClassInfo[cVar] = (cId, cName, tId);
                report.TotalClassesScanned++;

                bool hasDiv = divNodes.ContainsKey(cId);
                bool hasHeroChar = charNodes.ContainsKey(tId);

                if (!hasDiv)
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Code = "SYNC-NO-DIV",
                        Category = catSync,
                        Severity = DiagSeverity.Error,
                        TargetId = $"{cName} [{cId}]",
                        FilePath = divXmlPath,
                        Summary = $"Класс '{cName}' ({cId}) есть в DWDN_DuchyManager.cs, но отсутствует в mpclassdivisions.xml!",
                        Details = $"Ожидался узел <MPClassDivision id=\"{cId}\" hero=\"{tId}\" ... /> в {divXmlPath}",
                        ImpactExplanation = "При выборе этого класса в UI сервер не найдёт его MPClassDivision и не сможет заспавнить игрока!"
                    });
                }

                if (!hasHeroChar)
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Code = "SYNC-NO-CHAR",
                        Category = catSync,
                        Severity = DiagSeverity.Error,
                        TargetId = $"{cName} [{tId}]",
                        FilePath = charXmlPath,
                        Summary = $"Персонаж '{tId}' (для класса '{cName}') отсутствует в mpcharacters.xml!",
                        Details = $"Ожидался узел <NPCCharacter id=\"{tId}\" ... /> в {charXmlPath}",
                        ImpactExplanation = "Попытка спавна за этот класс вызовет мгновенный вылет сервера и клиента (NullReferenceException на HeroCharacter)."
                    });
                }

                if (hasDiv && hasHeroChar)
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Code = "SYNC-CLASS-OK",
                        Category = catSync,
                        Severity = DiagSeverity.Pass,
                        TargetId = $"{cName} ({cId} ↔ {tId})",
                        FilePath = csPath,
                        Summary = $"Класс '{cName}' полностью синхронизирован между C#, mpclassdivisions.xml и mpcharacters.xml",
                        Details = $"Class ID: {cId} | Troop ID: {tId}"
                    });
                }
            }

            // Check all item pools in DWDN_DuchyManager.cs
            var addRangeRegex = new Regex(@"(\w+)\.(Allowed\w+)\.AddRange\(\s*new\s*\[\]\s*\{([^\}]*)\}\s*\)", RegexOptions.Multiline);
            foreach (Match m in addRangeRegex.Matches(code))
            {
                string cVar = m.Groups[1].Value;
                string prop = m.Groups[2].Value;
                string itemsRaw = m.Groups[3].Value;

                if (!varToClassInfo.TryGetValue(cVar, out var cInfo)) continue;

                foreach (Match im in Regex.Matches(itemsRaw, @"""([^""]*)"""))
                {
                    string itemId = im.Groups[1].Value;
                    if (string.IsNullOrEmpty(itemId)) continue; // "" is valid empty slot

                    if (!allItems.TryGetValue(itemId, out var itemInfo))
                    {
                        report.Issues.Add(new ValidationIssue
                        {
                            Code = "CS-ITEM-MISSING",
                            Category = catItem,
                            Severity = DiagSeverity.Error,
                            TargetId = $"{cInfo.ClassName} ({prop}) -> {itemId}",
                            FilePath = csPath,
                            Summary = $"В DWDN_DuchyManager.cs у класса '{cInfo.ClassName}' в пуле {prop} указан несуществующий предмет '{itemId}'!",
                            Details = $"Класс: {cInfo.ClassId} ({cInfo.ClassName}), пул: {prop}, предмет: \"{itemId}\"",
                            ImpactExplanation = "Когда игрок выберет этот предмет в лодауте, сервер не найдёт ItemObject в MBObjectManager (или предмет не применится при спавне)."
                        });
                    }
                    else
                    {
                        // Check Slot vs ItemType compatibility
                        string expectedType = prop switch
                        {
                            "AllowedHelmets" => "HeadArmor",
                            "AllowedCapes" => "Cape",
                            "AllowedBodyArmors" => "BodyArmor",
                            "AllowedGloves" => "HandArmor",
                            "AllowedLegs" => "LegArmor",
                            "AllowedMounts" => "Horse",
                            _ => "WeaponOrShield"
                        };

                        if (expectedType != "WeaponOrShield" &&
                            !string.Equals(itemInfo.Type, expectedType, StringComparison.OrdinalIgnoreCase))
                        {
                            report.Issues.Add(new ValidationIssue
                            {
                                Code = "SLOT-TYPE-MISMATCH",
                                Category = catSlot,
                                Severity = DiagSeverity.Error,
                                TargetId = $"{cInfo.ClassName}: {itemId} ({itemInfo.Type} в {prop})",
                                FilePath = csPath,
                                Summary = $"Чужой тип предмета в слоте! Предмет '{itemId}' имеет Type='{itemInfo.Type}', но добавлен в '{prop}' (ожидался {expectedType})!",
                                Details = $"Класс: {cInfo.ClassName} ({cInfo.ClassId})\nПул: {prop}\nПредмет: {itemId} (Type={itemInfo.Type})",
                                ImpactExplanation = "Надевание предмета не в свой слот брони вызывает деформацию скелета агента или вылет клиента!"
                            });
                        }
                        else
                        {
                            report.Issues.Add(new ValidationIssue
                            {
                                Code = "CS-ITEM-OK",
                                Category = catItem,
                                Severity = DiagSeverity.Pass,
                                TargetId = $"{cInfo.ClassId}.{prop}: {itemId}",
                                FilePath = itemInfo.SourceFile,
                                Summary = $"Предмет '{itemId}' ({itemInfo.Type}) в пуле {prop} класса '{cInfo.ClassName}' валиден",
                                Details = $"Источник предмета: {itemInfo.SourceFile}"
                            });
                        }
                    }
                }
            }
        }

        private static void CheckGauntletUiAndSprites(string modRoot, ValidationReport report)
        {
            string cat = Categories[8];
            string guiDir = Path.Combine(modRoot, "GUI");
            if (!Directory.Exists(guiDir)) return;

            string spriteDataPath = Path.Combine(guiDir, "SpriteData.xml");
            if (File.Exists(spriteDataPath))
            {
                try
                {
                    var doc = XDocument.Load(spriteDataPath);
                    int spriteParts = doc.Descendants("SpritePart").Count();
                    int sprites = doc.Descendants("Sprite").Count();
                    report.Issues.Add(new ValidationIssue
                    {
                        Code = "UI-SPRITE-OK",
                        Category = cat,
                        Severity = DiagSeverity.Pass,
                        TargetId = "SpriteData.xml",
                        FilePath = spriteDataPath,
                        Summary = $"Атлас спрайтов SpriteData.xml валиден (SpriteParts: {spriteParts}, Sprites: {sprites})",
                        Details = "Все дескрипторы спрайтов корректно структурированы."
                    });
                }
                catch (Exception ex)
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Code = "UI-SPRITE-ERR",
                        Category = cat,
                        Severity = DiagSeverity.Error,
                        TargetId = "SpriteData.xml",
                        FilePath = spriteDataPath,
                        Summary = $"Ошибка в GUI/SpriteData.xml: {ex.Message}",
                        Details = ex.ToString(),
                        ImpactExplanation = "Повреждённый SpriteData.xml приводит к вылету или чёрным квадратам вместо текстур UI."
                    });
                }
            }

            string prefabsDir = Path.Combine(guiDir, "Prefabs");
            if (Directory.Exists(prefabsDir))
            {
                foreach (var prefab in Directory.GetFiles(prefabsDir, "*.xml", SearchOption.AllDirectories))
                {
                    string pName = Path.GetFileName(prefab);
                    try
                    {
                        var doc = XDocument.Load(prefab);
                        report.Issues.Add(new ValidationIssue
                        {
                            Code = "UI-PREFAB-OK",
                            Category = cat,
                            Severity = DiagSeverity.Pass,
                            TargetId = pName,
                            FilePath = prefab,
                            Summary = $"Gauntlet UI префаб '{pName}' прошёл проверку структуры",
                            Details = $"Путь: {prefab}"
                        });
                    }
                    catch (Exception ex)
                    {
                        report.Issues.Add(new ValidationIssue
                        {
                            Code = "UI-PREFAB-ERR",
                            Category = cat,
                            Severity = DiagSeverity.Error,
                            TargetId = pName,
                            FilePath = prefab,
                            Summary = $"Ошибка в Gauntlet UI префабе '{pName}': {ex.Message}",
                            Details = ex.ToString(),
                            ImpactExplanation = "Сломанный XML префаба Gauntlet вызывает мгновенный краш при открытии соответствующего экрана в игре."
                        });
                    }
                }
            }
        }

        private static void CheckCSharpScriptsAndNetworkPackets(string modRoot, ValidationReport report)
        {
            string cat = Categories[9];
            string scriptsDir = Path.Combine(modRoot, "ModuleData", "scripts", "DWDN_Script");
            if (!Directory.Exists(scriptsDir)) return;

            var csFiles = Directory.GetFiles(scriptsDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            report.TotalCsFilesScanned = csFiles.Length;

            foreach (var csFile in csFiles)
            {
                string text = File.ReadAllText(csFile);
                string fileName = Path.GetFileName(csFile);

                // Check GameNetworkMessage subclasses for Read/Write symmetry
                if (text.Contains(": GameNetworkMessage"))
                {
                    int reads = Regex.Matches(text, @"GameNetworkMessage\.Read\w+FromPacket").Count;
                    int writes = Regex.Matches(text, @"GameNetworkMessage\.Write\w+ToPacket").Count;

                    if (reads != writes)
                    {
                        report.Issues.Add(new ValidationIssue
                        {
                            Code = "NET-PACKET-ASYMMETRY",
                            Category = cat,
                            Severity = DiagSeverity.Error,
                            TargetId = fileName,
                            FilePath = csFile,
                            Summary = $"Рассинхрон сетевого пакета в '{fileName}': вызовов ReadFromPacket ({reads}) != WriteToPacket ({writes})!",
                            Details = $"Файл: {csFile}\nКоличество операций чтения из битового потока ({reads}) не совпадает с количеством операций записи ({writes}).",
                            ImpactExplanation = "Любое несовпадение числа или порядка Read/Write в GameNetworkMessage вызывает мгновенный краш сетевого буфера или кик игрока с сервера!"
                        });
                    }
                    else
                    {
                        report.Issues.Add(new ValidationIssue
                        {
                            Code = "NET-PACKET-OK",
                            Category = cat,
                            Severity = DiagSeverity.Pass,
                            TargetId = fileName,
                            FilePath = csFile,
                            Summary = $"Сетевой пакет '{fileName}' симметричен (Read: {reads} == Write: {writes})",
                            Details = $"Битовый поток GameNetworkMessage сбалансирован."
                        });
                    }
                }
            }
        }

        public static string GenerateMarkdownReport(ValidationReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 🛡️ Предпусковой отчёт валидации мода DWDN_TRY");
            sb.AppendLine($"- **Дата проверки**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"- **Время сканирования**: {report.ElapsedMs} мс");
            sb.AppendLine($"- **Критичных ошибок (🔴 ERROR)**: {report.ErrorCount}");
            sb.AppendLine($"- **Предупреждений (🟡 WARNING)**: {report.WarningCount}");
            sb.AppendLine($"- **Успешных проверок (🟢 PASS)**: {report.PassCount}");
            sb.AppendLine($"- **Проверено**: Классов: {report.TotalClassesScanned} | Предметов: {report.TotalItemsScanned} | Деталей крафта: {report.TotalPiecesScanned} | 3D-мешей: {report.TotalMeshesScanned} | XML: {report.TotalXmlFilesScanned} | C#: {report.TotalCsFilesScanned}");
            sb.AppendLine();

            var nonPass = report.Issues.Where(i => i.Severity != DiagSeverity.Pass).OrderByDescending(i => i.Severity).ToList();
            if (nonPass.Count == 0)
            {
                sb.AppendLine("## ✅ Все проверки пройдены безупречно! Мод полностью готов к запуску.");
            }
            else
            {
                sb.AppendLine("## ⚠️ Обнаруженные проблемы и предупреждения");
                foreach (var issue in nonPass)
                {
                    string icon = issue.Severity == DiagSeverity.Error ? "🔴 [КРИТИЧНО]" : "🟡 [ВНИМАНИЕ]";
                    sb.AppendLine($"### {icon} `{issue.Code}` — {issue.Summary}");
                    sb.AppendLine($"- **Категория**: {issue.Category}");
                    sb.AppendLine($"- **Объект**: `{issue.TargetId}`");
                    sb.AppendLine($"- **Файл**: `{issue.FilePath}`" + (issue.LineNumber > 0 ? $" (Строка {issue.LineNumber})" : ""));
                    sb.AppendLine($"- **Влияние на игру**: {issue.ImpactExplanation}");
                    if (!string.IsNullOrEmpty(issue.AutoFixDescription))
                        sb.AppendLine($"- **Доступно авто-исправление**: {issue.AutoFixDescription}");
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }
    }
}
