using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;

namespace WeaponCalibrator;

public enum PieceType
{
    Invalid = -1,
    Blade = 0,
    Guard = 1,
    Handle = 2,
    Pommel = 3
}

public class MeshGeometryInfo
{
    public string FileKey { get; set; } = "";
    public string TpacPath { get; set; } = "";
    public string FbxPath { get; set; } = "";
    public string RegisteredMetaMeshName { get; set; } = "";
    public int VertexCount { get; set; }
    // Bounding box in centimeters (Bannerlord XML unit)
    public float MinXCm { get; set; }
    public float MaxXCm { get; set; }
    public float MinYCm { get; set; }
    public float MaxYCm { get; set; }
    public float MinZCm { get; set; }
    public float MaxZCm { get; set; }
    public float LengthZCm => MaxZCm - MinZCm;
    public float WidthXCm => MaxXCm - MinXCm;
    public float CenterZCm => 0.5f * (MinZCm + MaxZCm);
    // Central-axis (|X| <= 2.2 cm) Z bounds in TPAC space — measures the true central collar of curved guards!
    public float CoreMinZCm { get; set; }
    public float CoreMaxZCm { get; set; }
    public bool HasCoreBounds { get; set; }
    // 20-slice normalized X half-widths along Z from MinZ to MaxZ
    public float[] ProfileHalfWidthsCm { get; set; } = Array.Empty<float>();
    // Full 3D Mesh in TPAC centimeter space for the real-time 3D GUI viewport!
    public (float X, float Y, float Z)[] VerticesCm { get; set; } = Array.Empty<(float, float, float)>();
    public (int A, int B, int C)[] Triangles { get; set; } = Array.Empty<(int, int, int)>();
}

public class CraftingPieceDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public PieceType PieceType { get; set; }
    public string MeshName { get; set; } = "";
    public float LengthCm { get; set; }
    public float Weight { get; set; }
    public bool FullScale { get; set; }
    public float PieceOffsetCm { get; set; }
    public float PreviousPieceOffsetCm { get; set; }
    public float NextPieceOffsetCm { get; set; }
    public XElement OriginalXml { get; set; }

    public CraftingPieceDef Clone()
    {
        return new CraftingPieceDef
        {
            Id = Id,
            Name = Name,
            PieceType = PieceType,
            MeshName = MeshName,
            LengthCm = LengthCm,
            Weight = Weight,
            FullScale = FullScale,
            PieceOffsetCm = PieceOffsetCm,
            PreviousPieceOffsetCm = PreviousPieceOffsetCm,
            NextPieceOffsetCm = NextPieceOffsetCm,
            OriginalXml = OriginalXml != null ? new XElement(OriginalXml) : null
        };
    }
}

public record PieceOrder(PieceType PieceType, int BuildOrder);

public class CraftingTemplateDef
{
    public string Id { get; set; } = "";
    public List<PieceOrder> BuildOrders { get; set; } = new();
}

public class CraftedWeaponItemDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string TemplateId { get; set; } = "";
    public Dictionary<PieceType, (string PieceId, int ScalePercent)> Pieces { get; set; } = new();

    public CraftedWeaponItemDef Clone()
    {
        var copy = new CraftedWeaponItemDef
        {
            Id = Id,
            Name = Name,
            TemplateId = TemplateId
        };
        foreach (var kv in Pieces)
            copy.Pieces[kv.Key] = kv.Value;
        return copy;
    }
}

public class PiecePlacementResult
{
    public PieceType PieceType { get; set; }
    public string PieceId { get; set; } = "";
    public string MeshName { get; set; } = "";
    public string ActualTpacMeshName { get; set; } = "";
    public int ScalePercent { get; set; } = 100;
    public float ScaleFactor => ScalePercent * 0.01f;
    public float PivotZCm { get; set; }
    public float WorldMinZCm { get; set; }
    public float WorldMaxZCm { get; set; }
    public float WorldMinXCm { get; set; }
    public float WorldMaxXCm { get; set; }
    public MeshGeometryInfo Geometry { get; set; }
    public CraftingPieceDef PieceDef { get; set; }
}

public class WeaponSimulationResult
{
    public string WeaponId { get; set; } = "";
    public string TemplateId { get; set; } = "";
    public Dictionary<PieceType, PiecePlacementResult> Placements { get; set; } = new();
    public float CraftedWeaponLengthCm { get; set; }
    public float HandToBottomLengthCm { get; set; }
    public float TotalLengthCm => CraftedWeaponLengthCm + HandToBottomLengthCm;
    public List<string> Diagnostics { get; set; } = new();
}

public static class Program
{
    private static string FindModRoot(string fallback)
    {
        if (Directory.Exists(fallback)) return fallback;
        DirectoryInfo dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null)
        {
            if (dir.Name.Equals("DWDN_TRY", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(dir.FullName, "SubModule.xml")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return fallback;
    }

    [STAThread]
    static void Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        string modPath = FindModRoot(@"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\DWDN_TRY");
        string outputDir = @"d:\banAImod\WeaponCalibrator\output";
        Directory.CreateDirectory(outputDir);

        var geometries = ScanModuleGeometries(modPath);
        var templates = LoadCraftingTemplates(Path.Combine(modPath, @"ModuleData\crafting_templates.xml"));
        var pieces = LoadCraftingPieces(Path.Combine(modPath, @"ModuleData\crafting_pieces.xml"));
        var weapons = LoadCraftedWeapons(Path.Combine(modPath, @"ModuleData\mpitems\dwdn_weapon.xml"));

        // Ensure any standalone weapon _geo.tpac meshes are also available as selectable CraftingPieces in the GUI
        RegisterUnlistedWeaponMeshes(geometries, pieces);

        if (args.Length > 0 && args[0] == "--assemble")
        {
            Console.OutputEncoding = Encoding.UTF8;
            RunCustomCliAssembly(args, geometries, templates, pieces, outputDir);
            return;
        }

        if (args.Length > 0 && args[0] == "--cli")
        {
            Console.OutputEncoding = Encoding.UTF8;
            RunBatchCliAnalysis(modPath, outputDir, geometries, templates, pieces, weapons);
            return;
        }

        // Default: Launch the Interactive 3D Windows Desktop GUI Studio!
        ApplicationConfiguration.Initialize();
        var mainForm = new MainForm(modPath, outputDir, geometries, templates, pieces, weapons);

        string screenshotPath = GetArg(args, "--screenshot", "");
        if (!string.IsNullOrEmpty(screenshotPath))
        {
            mainForm.Shown += (_, _) =>
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(250);
                Application.DoEvents();
                mainForm.SaveWindowScreenshot(screenshotPath);
                mainForm.Close();
            };
        }

        Application.Run(mainForm);
    }

    private static void RegisterUnlistedWeaponMeshes(
        Dictionary<string, MeshGeometryInfo> geometries,
        Dictionary<string, CraftingPieceDef> pieces)
    {
        foreach (var geo in geometries.Values.Distinct())
        {
            if (pieces.Values.Any(p =>
                string.Equals(p.MeshName, geo.FileKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.MeshName, geo.RegisteredMetaMeshName, StringComparison.OrdinalIgnoreCase)))
                continue;

            string key = geo.FileKey.ToLowerInvariant();
            PieceType guessedType = PieceType.Invalid;
            if (key.Contains("blade") || key.Contains("spearhead")) guessedType = PieceType.Blade;
            else if (key.Contains("guard") || key.Contains("handguard")) guessedType = PieceType.Guard;
            else if (key.Contains("handle") || key.Contains("grip")) guessedType = PieceType.Handle;
            else if (key.Contains("pommel")) guessedType = PieceType.Pommel;

            if (guessedType != PieceType.Invalid && !pieces.ContainsKey(geo.FileKey))
            {
                pieces[geo.FileKey] = new CraftingPieceDef
                {
                    Id = geo.FileKey,
                    Name = geo.FileKey,
                    PieceType = guessedType,
                    MeshName = geo.RegisteredMetaMeshName,
                    LengthCm = (float)Math.Round(geo.LengthZCm, 2),
                    Weight = 0.5f,
                    FullScale = (guessedType == PieceType.Guard || guessedType == PieceType.Pommel)
                };
            }
        }
    }

    public static void RunBatchCliAnalysis(
        string modPath,
        string outputDir,
        Dictionary<string, MeshGeometryInfo> geometries,
        Dictionary<string, CraftingTemplateDef> templates,
        Dictionary<string, CraftingPieceDef> pieces,
        List<CraftedWeaponItemDef> weapons)
    {
        var calibratedPieces = pieces.ToDictionary(k => k.Key, v => v.Value.Clone(), StringComparer.OrdinalIgnoreCase);
        var beforeResults = new List<WeaponSimulationResult>();
        var afterResults = new List<WeaponSimulationResult>();

        foreach (var weapon in weapons)
        {
            bool hasCustomPiece = weapon.Pieces.Values.Any(p => pieces.ContainsKey(p.PieceId));
            if (!hasCustomPiece) continue;

            if (!templates.TryGetValue(weapon.TemplateId, out var template))
            {
                template = templates["Daewden_BastardSword"];
            }

            var beforeSim = SimulateWeapon(weapon, template, pieces, geometries);
            beforeResults.Add(beforeSim);

            AutoCalibrateWeapon(weapon, template, calibratedPieces, geometries);

            var afterSim = SimulateWeapon(weapon, template, calibratedPieces, geometries);
            afterResults.Add(afterSim);

            PrintWeaponComparison(weapon, beforeSim, afterSim);
        }

        string calibratedXmlPath = Path.Combine(outputDir, "calibrated_crafting_pieces.xml");
        SaveCalibratedPiecesXml(Path.Combine(modPath, @"ModuleData\crafting_pieces.xml"), calibratedPieces, calibratedXmlPath);
        string blueprintHtmlPath = Path.Combine(outputDir, "weapon_blueprint.html");
        GenerateHtmlBlueprint(beforeResults, afterResults, calibratedPieces, blueprintHtmlPath);
    }

    public static WeaponSimulationResult SimulateWeapon(
        CraftedWeaponItemDef weapon,
        CraftingTemplateDef template,
        Dictionary<string, CraftingPieceDef> pieceDb,
        Dictionary<string, MeshGeometryInfo> geoDb)
    {
        var result = new WeaponSimulationResult
        {
            WeaponId = weapon.Id,
            TemplateId = template.Id
        };

        float num = 0f;
        float num2 = 0f;
        float[] pivotDistancesM = new float[4] { float.NaN, float.NaN, float.NaN, float.NaN };

        foreach (var bo in template.BuildOrders)
        {
            int ptIdx = (int)bo.PieceType;
            if (!weapon.Pieces.TryGetValue(bo.PieceType, out var used) ||
                string.IsNullOrEmpty(used.PieceId) ||
                !pieceDb.TryGetValue(used.PieceId, out var piece))
            {
                pivotDistancesM[ptIdx] = float.NaN;
                continue;
            }

            float scale = used.ScalePercent * 0.01f;
            float lengthM = piece.LengthCm * 0.01f;
            float distNextM = (lengthM * 0.5f) * scale;
            float distPrevM = (lengthM * 0.5f) * scale;
            float pieceOffsetM = (piece.PieceOffsetCm * 0.01f) * scale;
            float prevOffsetM = (piece.PreviousPieceOffsetCm * 0.01f) * scale;
            float nextOffsetM = (piece.NextPieceOffsetCm * 0.01f) * scale;

            int sign = Math.Sign(bo.BuildOrder);
            if (sign == 0)
            {
                num2 += pieceOffsetM;
                num -= pieceOffsetM;
            }
            else if (sign < 0)
            {
                num += distNextM;
                num += pieceOffsetM;
                num -= nextOffsetM;
            }
            else if (sign > 0)
            {
                num2 += distPrevM;
                num2 += pieceOffsetM;
                num2 -= prevOffsetM;
            }

            pivotDistancesM[ptIdx] = sign * ((sign < 0) ? num : num2) + ((sign == 0) ? pieceOffsetM : 0f);

            if (sign == 0)
            {
                num += distPrevM - prevOffsetM;
                num2 += distNextM - nextOffsetM;
            }
            if (sign < 0)
            {
                num += distPrevM - prevOffsetM;
            }
            if (sign > 0)
            {
                num2 += distNextM - nextOffsetM;
            }

            var geo = ResolveGeometry(piece.MeshName, geoDb) ?? ResolveGeometry(piece.Id, geoDb);
            float pivotZCm = pivotDistancesM[ptIdx] * 100f;
            float worldMinZCm = pivotZCm + (geo != null ? geo.MinZCm * scale : -distPrevM * 100f);
            float worldMaxZCm = pivotZCm + (geo != null ? geo.MaxZCm * scale : +distNextM * 100f);
            float xScale = piece.FullScale ? scale : 1.0f;
            float worldMinXCm = geo != null ? geo.MinXCm * xScale : -2f;
            float worldMaxXCm = geo != null ? geo.MaxXCm * xScale : +2f;

            if (geo != null && !string.Equals(geo.RegisteredMetaMeshName, piece.MeshName, StringComparison.OrdinalIgnoreCase))
            {
                result.Diagnostics.Add(
                    $"[!] MESH MISMATCH '{piece.Id}': XML=\"{piece.MeshName}\" vs TPAC=\"{geo.RegisteredMetaMeshName}\"");
            }

            result.Placements[bo.PieceType] = new PiecePlacementResult
            {
                PieceType = bo.PieceType,
                PieceId = piece.Id,
                MeshName = piece.MeshName,
                ActualTpacMeshName = geo?.RegisteredMetaMeshName ?? piece.MeshName,
                ScalePercent = used.ScalePercent,
                PivotZCm = pivotZCm,
                WorldMinZCm = worldMinZCm,
                WorldMaxZCm = worldMaxZCm,
                WorldMinXCm = worldMinXCm,
                WorldMaxXCm = worldMaxXCm,
                Geometry = geo,
                PieceDef = piece.Clone()
            };
        }

        result.HandToBottomLengthCm = num * 100f;
        if (result.Placements.TryGetValue(PieceType.Blade, out var bladePl))
        {
            float scaledDistNextCm = (bladePl.PieceDef.LengthCm * 0.5f) * bladePl.ScaleFactor;
            result.CraftedWeaponLengthCm = bladePl.PivotZCm + scaledDistNextCm;
        }

        AnalyzeSeamGaps(result);
        return result;
    }

    private static void AnalyzeSeamGaps(WeaponSimulationResult sim)
    {
        if (sim.Placements.TryGetValue(PieceType.Handle, out var handle))
        {
            if (sim.Placements.TryGetValue(PieceType.Pommel, out var pommel))
            {
                float gap = handle.WorldMinZCm - pommel.WorldMaxZCm;
                sim.Diagnostics.Add($"Seam [Pommel.Top -> Handle.Bottom]: gap = {gap:+0.00;-0.00;0.00} cm");
            }

            if (sim.Placements.TryGetValue(PieceType.Guard, out var guard))
            {
                float guardBotZ = guard.WorldMinZCm;
                float guardTopZ = guard.WorldMaxZCm;
                string collarNote = "";
                if (guard.Geometry != null && guard.Geometry.HasCoreBounds &&
                    (guard.Geometry.CoreMaxZCm - guard.Geometry.CoreMinZCm) < guard.Geometry.LengthZCm * 0.7f)
                {
                    guardBotZ = guard.PivotZCm + guard.Geometry.CoreMinZCm * guard.ScaleFactor;
                    guardTopZ = guard.PivotZCm + guard.Geometry.CoreMaxZCm * guard.ScaleFactor;
                    collarNote = " (Central Collar)";
                }

                float gapHG = guardBotZ - handle.WorldMaxZCm;
                sim.Diagnostics.Add($"Seam [Handle.Top -> Guard.Bottom{collarNote}]: gap = {gapHG:+0.00;-0.00;0.00} cm");

                if (sim.Placements.TryGetValue(PieceType.Blade, out var blade))
                {
                    float gapGB = blade.WorldMinZCm - guardTopZ;
                    sim.Diagnostics.Add($"Seam [Guard.Top{collarNote} -> Blade.Bottom]: gap = {gapGB:+0.00;-0.00;0.00} cm");
                }
            }
            else if (sim.Placements.TryGetValue(PieceType.Blade, out var blade))
            {
                float gapHB = blade.WorldMinZCm - handle.WorldMaxZCm;
                sim.Diagnostics.Add($"Seam [Handle.Top -> Blade.Bottom]: gap = {gapHB:+0.00;-0.00;0.00} cm");
            }
        }
    }

    public static void AutoCalibrateWeapon(
        CraftedWeaponItemDef weapon,
        CraftingTemplateDef template,
        Dictionary<string, CraftingPieceDef> pieceDb,
        Dictionary<string, MeshGeometryInfo> geoDb)
    {
        bool hasGuard = weapon.Pieces.TryGetValue(PieceType.Guard, out var gCheck) && !string.IsNullOrEmpty(gCheck.PieceId);

        MeshGeometryInfo handleGeo = null;
        int handleScalePct = 100;
        if (weapon.Pieces.TryGetValue(PieceType.Handle, out var hUsed) &&
            pieceDb.TryGetValue(hUsed.PieceId, out var hPiece))
        {
            handleGeo = ResolveGeometry(hPiece.MeshName, geoDb) ?? ResolveGeometry(hPiece.Id, geoDb);
            handleScalePct = hUsed.ScalePercent;
        }

        MeshGeometryInfo guardGeo = null;
        int guardScalePct = 100;
        if (hasGuard && weapon.Pieces.TryGetValue(PieceType.Guard, out var gUsed) &&
            pieceDb.TryGetValue(gUsed.PieceId, out var gPiece))
        {
            guardGeo = ResolveGeometry(gPiece.MeshName, geoDb) ?? ResolveGeometry(gPiece.Id, geoDb);
            guardScalePct = gUsed.ScalePercent;
        }

        bool isPolearmShaft = handleGeo != null && handleGeo.LengthZCm > 120f;

        foreach (var kv in weapon.Pieces)
        {
            PieceType pType = kv.Key;
            string pieceId = kv.Value.PieceId;
            if (string.IsNullOrEmpty(pieceId)) continue;

            if (!pieceDb.TryGetValue(pieceId, out var piece)) continue;
            var geo = ResolveGeometry(piece.MeshName, geoDb) ?? ResolveGeometry(piece.Id, geoDb);
            if (geo == null) continue;

            float trueLenCm = (float)Math.Round(geo.LengthZCm, 2);
            piece.LengthCm = trueLenCm;
            float halfL = trueLenCm * 0.5f;

            if (!string.Equals(piece.MeshName, geo.RegisteredMetaMeshName, StringComparison.OrdinalIgnoreCase))
            {
                piece.MeshName = geo.RegisteredMetaMeshName;
            }

            float bottomTargetLocalZ = geo.MinZCm;
            float topTargetLocalZ = geo.MaxZCm;

            if (pType == PieceType.Handle)
            {
                if (!isPolearmShaft)
                {
                    piece.PieceOffsetCm = (float)Math.Round(-geo.CenterZCm, 2);
                    bottomTargetLocalZ = geo.MinZCm;
                    topTargetLocalZ = geo.MaxZCm;
                }
                else
                {
                    piece.PieceOffsetCm = 0f;
                    bottomTargetLocalZ = geo.MinZCm;
                    if (hasGuard && guardGeo != null)
                    {
                        float guardWorldLen = guardGeo.LengthZCm * (guardScalePct * 0.01f);
                        float handleScale = handleScalePct * 0.01f;
                        topTargetLocalZ = geo.MaxZCm - (guardWorldLen / handleScale);
                    }
                    else
                    {
                        topTargetLocalZ = geo.MaxZCm;
                    }
                }

                piece.PreviousPieceOffsetCm = (float)Math.Round(halfL + bottomTargetLocalZ, 2);
                piece.NextPieceOffsetCm = (float)Math.Round(halfL - topTargetLocalZ, 2);
            }
            else if (pType == PieceType.Guard)
            {
                piece.PieceOffsetCm = 0f;
                if (!isPolearmShaft && geo.HasCoreBounds && (geo.CoreMaxZCm - geo.CoreMinZCm) < geo.LengthZCm * 0.7f)
                {
                    bottomTargetLocalZ = geo.CoreMinZCm;
                    topTargetLocalZ = geo.CoreMaxZCm;
                }
                else
                {
                    bottomTargetLocalZ = geo.MinZCm;
                    topTargetLocalZ = geo.MaxZCm;
                }

                piece.PreviousPieceOffsetCm = (float)Math.Round(halfL + bottomTargetLocalZ, 2);
                piece.NextPieceOffsetCm = (float)Math.Round(halfL - topTargetLocalZ, 2);
            }
            else if (pType == PieceType.Blade)
            {
                piece.PieceOffsetCm = 0f;
                bottomTargetLocalZ = geo.MinZCm;
                topTargetLocalZ = geo.MaxZCm;

                piece.PreviousPieceOffsetCm = (float)Math.Round(halfL + bottomTargetLocalZ, 2);
                piece.NextPieceOffsetCm = (float)Math.Round(halfL - topTargetLocalZ, 2);
            }
            else if (pType == PieceType.Pommel)
            {
                piece.PieceOffsetCm = 0f;
                bottomTargetLocalZ = geo.MinZCm;
                topTargetLocalZ = geo.MaxZCm;

                piece.PreviousPieceOffsetCm = (float)Math.Round(halfL + bottomTargetLocalZ, 2);
                piece.NextPieceOffsetCm = (float)Math.Round(halfL - topTargetLocalZ, 2);
            }
        }
    }

    public static MeshGeometryInfo ResolveGeometry(string meshName, Dictionary<string, MeshGeometryInfo> geoDb)
    {
        if (string.IsNullOrEmpty(meshName)) return null;
        if (geoDb.TryGetValue(meshName, out var g)) return g;
        return geoDb.Values.FirstOrDefault(x =>
            string.Equals(x.FileKey, meshName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.RegisteredMetaMeshName, meshName, StringComparison.OrdinalIgnoreCase));
    }

    public static Dictionary<string, MeshGeometryInfo> ScanModuleGeometries(string modPath)
    {
        var dict = new Dictionary<string, MeshGeometryInfo>(StringComparer.OrdinalIgnoreCase);
        string assetsDir = Path.Combine(modPath, "Assets");
        string sourcesDir = Path.Combine(modPath, "AssetSources");
        if (!Directory.Exists(assetsDir)) return dict;

        foreach (var tpacFile in Directory.GetFiles(assetsDir, "*_geo.tpac", SearchOption.AllDirectories))
        {
            var info = ParseGeoTpac(tpacFile, modPath);
            if (info == null) continue;

            if (File.Exists(info.FbxPath))
            {
                ExtractFbxMeshAndProfile(info.FbxPath, info);
            }
            else if (Directory.Exists(sourcesDir))
            {
                var fallbackFbx = Directory.GetFiles(sourcesDir, info.FileKey + ".fbx", SearchOption.AllDirectories).FirstOrDefault();
                if (fallbackFbx != null)
                {
                    info.FbxPath = fallbackFbx;
                    ExtractFbxMeshAndProfile(fallbackFbx, info);
                }
            }

            dict[info.FileKey] = info;
            if (!string.IsNullOrEmpty(info.RegisteredMetaMeshName))
            {
                dict[info.RegisteredMetaMeshName] = info;
            }
        }

        return dict;
    }

    private static MeshGeometryInfo ParseGeoTpac(string tpacPath, string modPath)
    {
        byte[] b = File.ReadAllBytes(tpacPath);
        if (b.Length < 400 || b[0] != 'T' || b[1] != 'P' || b[2] != 'A' || b[3] != 'C')
            return null;

        string fileKey = Path.GetFileNameWithoutExtension(tpacPath);
        if (fileKey.EndsWith("_geo", StringComparison.OrdinalIgnoreCase))
            fileKey = fileKey.Substring(0, fileKey.Length - 4);

        string fbxRel = "";
        string asciiHeader = Encoding.ASCII.GetString(b, 0, Math.Min(350, b.Length));
        int baseIdx = asciiHeader.IndexOf("$BASE/", StringComparison.Ordinal);
        if (baseIdx >= 4)
        {
            int strLen = BitConverter.ToInt32(b, baseIdx - 4);
            if (strLen > 0 && baseIdx + strLen <= b.Length)
            {
                fbxRel = Encoding.ASCII.GetString(b, baseIdx, strLen);
            }
        }

        for (int i = 150; i < b.Length - 180; i++)
        {
            bool allOne = true;
            for (int k = 0; k < 8; k++)
            {
                if (BitConverter.ToSingle(b, i + k * 4) != 1.0f)
                {
                    allOne = false;
                    break;
                }
            }
            if (!allOne) continue;

            int afterName = i - 24;
            string subMeshName = fileKey;
            for (int len = 1; len <= 64; len++)
            {
                int lenPos = afterName - len - 4;
                if (lenPos > 0 && BitConverter.ToInt32(b, lenPos) == len)
                {
                    subMeshName = Encoding.ASCII.GetString(b, lenPos + 4, len);
                    break;
                }
            }

            int vertexCount = BitConverter.ToInt32(b, afterName + 92);
            float minX = BitConverter.ToSingle(b, afterName + 112);
            float minY = BitConverter.ToSingle(b, afterName + 116);
            float minZ = BitConverter.ToSingle(b, afterName + 120);
            float maxX = BitConverter.ToSingle(b, afterName + 128);
            float maxY = BitConverter.ToSingle(b, afterName + 132);
            float maxZ = BitConverter.ToSingle(b, afterName + 136);

            string resolvedFbx = "";
            if (!string.IsNullOrEmpty(fbxRel))
            {
                string gameRoot = Path.GetFullPath(Path.Combine(modPath, @"..\.."));
                resolvedFbx = Path.Combine(gameRoot, fbxRel.Replace("$BASE/", "").Replace('/', Path.DirectorySeparatorChar));
            }

            return new MeshGeometryInfo
            {
                FileKey = fileKey,
                TpacPath = tpacPath,
                FbxPath = resolvedFbx,
                RegisteredMetaMeshName = subMeshName,
                VertexCount = vertexCount,
                MinXCm = minX * 100f,
                MaxXCm = maxX * 100f,
                MinYCm = minY * 100f,
                MaxYCm = maxY * 100f,
                MinZCm = minZ * 100f,
                MaxZCm = maxZ * 100f
            };
        }

        return null;
    }

    private static void ExtractFbxMeshAndProfile(string fbxPath, MeshGeometryInfo info)
    {
        try
        {
            using var fs = File.OpenRead(fbxPath);
            using var br = new BinaryReader(fs);
            byte[] header = br.ReadBytes(23);
            string magic = Encoding.ASCII.GetString(header, 0, 20);
            if (!magic.StartsWith("Kaydara FBX Binary")) return;

            uint version = br.ReadUInt32();
            var (vertices, polyIndices) = FindFirstFbxGeometry(br, fs, version);
            if (vertices == null || vertices.Count < 3) return;

            const int numSlices = 20;
            float[] slices = new float[numSlices];
            double minZ = double.MaxValue, maxZ = double.MinValue;
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            foreach (var v in vertices)
            {
                if (v.x < minX) minX = v.x;
                if (v.x > maxX) maxX = v.x;
                if (v.y < minY) minY = v.y;
                if (v.y > maxY) maxY = v.y;
                if (v.z < minZ) minZ = v.z;
                if (v.z > maxZ) maxZ = v.z;
            }

            double spanZ = maxZ - minZ;
            if (spanZ < 1e-6) return;

            float xShiftCm = info.MinXCm - (float)(minX * 100.0);
            float yShiftCm = info.MinYCm - (float)(minY * 100.0);
            float zShiftCm = info.MinZCm - (float)(minZ * 100.0);

            double coreMinZ = double.MaxValue, coreMaxZ = double.MinValue;
            int coreCount = 0;

            var vertsCm = new (float X, float Y, float Z)[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                int bucket = (int)(((v.z - minZ) / spanZ) * (numSlices - 1));
                bucket = Math.Clamp(bucket, 0, numSlices - 1);
                float rCm = (float)(Math.Max(Math.Abs(v.x), Math.Abs(v.y)) * 100.0);
                if (rCm > slices[bucket]) slices[bucket] = rCm;

                if (Math.Abs(v.x * 100.0) <= 2.2 && Math.Abs(v.y * 100.0) <= 2.2)
                {
                    if (v.z < coreMinZ) coreMinZ = v.z;
                    if (v.z > coreMaxZ) coreMaxZ = v.z;
                    coreCount++;
                }

                vertsCm[i] = (
                    (float)(v.x * 100.0) + xShiftCm,
                    (float)(v.y * 100.0) + yShiftCm,
                    (float)(v.z * 100.0) + zShiftCm
                );
            }

            for (int i = 0; i < numSlices; i++)
            {
                if (slices[i] <= 0.001f)
                    slices[i] = i > 0 ? slices[i - 1] : 1.0f;
            }

            info.ProfileHalfWidthsCm = slices;
            info.VerticesCm = vertsCm;

            if (coreCount >= 4 && coreMaxZ > coreMinZ)
            {
                info.CoreMinZCm = (float)(coreMinZ * 100.0) + zShiftCm;
                info.CoreMaxZCm = (float)(coreMaxZ * 100.0) + zShiftCm;
                info.HasCoreBounds = true;
            }

            // Triangulate FBX PolygonVertexIndex
            if (polyIndices != null && polyIndices.Length >= 3)
            {
                var tris = new List<(int, int, int)>(polyIndices.Length);
                var poly = new List<int>(8);
                foreach (int rawIdx in polyIndices)
                {
                    bool isLast = rawIdx < 0;
                    int vIdx = isLast ? (-rawIdx - 1) : rawIdx;
                    if (vIdx >= 0 && vIdx < vertsCm.Length)
                        poly.Add(vIdx);

                    if (isLast)
                    {
                        for (int k = 1; k + 1 < poly.Count; k++)
                        {
                            tris.Add((poly[0], poly[k], poly[k + 1]));
                        }
                        poly.Clear();
                    }
                }
                info.Triangles = tris.ToArray();
            }
        }
        catch
        {
            // Ignore FBX parse errors; TPAC bounding box is already loaded
        }
    }

    private static (List<(double x, double y, double z)> verts, int[] polyIndices) FindFirstFbxGeometry(
        BinaryReader br, Stream fs, uint version)
    {
        List<(double x, double y, double z)> verts = null;
        int[] polys = null;

        while (fs.Position < fs.Length)
        {
            long endOffset = version >= 7500 ? (long)br.ReadUInt64() : br.ReadUInt32();
            long numProps = version >= 7500 ? (long)br.ReadUInt64() : br.ReadUInt32();
            long propListLen = version >= 7500 ? (long)br.ReadUInt64() : br.ReadUInt32();
            byte nameLen = br.ReadByte();
            if (endOffset == 0) break;
            string name = Encoding.ASCII.GetString(br.ReadBytes(nameLen));
            long propStart = fs.Position;

            if (name == "Vertices" && verts == null)
            {
                char type = (char)br.ReadByte();
                int arrLen = br.ReadInt32();
                int enc = br.ReadInt32();
                int compLen = br.ReadInt32();
                byte[] decompressed = ReadFbxArrayBytes(br, compLen, enc);
                verts = new List<(double x, double y, double z)>(arrLen / 3);
                using var vBr = new BinaryReader(new MemoryStream(decompressed));
                int count = arrLen / 3;
                for (int i = 0; i < count; i++)
                {
                    double x = type == 'd' ? vBr.ReadDouble() : vBr.ReadSingle();
                    double y = type == 'd' ? vBr.ReadDouble() : vBr.ReadSingle();
                    double z = type == 'd' ? vBr.ReadDouble() : vBr.ReadSingle();
                    verts.Add((x, y, z));
                }
            }
            else if (name == "PolygonVertexIndex" && polys == null && verts != null)
            {
                char type = (char)br.ReadByte();
                int arrLen = br.ReadInt32();
                int enc = br.ReadInt32();
                int compLen = br.ReadInt32();
                byte[] decompressed = ReadFbxArrayBytes(br, compLen, enc);
                polys = new int[arrLen];
                using var pBr = new BinaryReader(new MemoryStream(decompressed));
                for (int i = 0; i < arrLen; i++)
                {
                    polys[i] = type == 'i' ? pBr.ReadInt32() : (int)pBr.ReadInt64();
                }
                return (verts, polys);
            }

            fs.Position = propStart + propListLen;
            if (fs.Position < endOffset)
            {
                var sub = FindFirstFbxGeometry(br, fs, version);
                if (sub.verts != null) return sub;
            }
            fs.Position = endOffset;
        }

        return (verts, polys);
    }

    private static byte[] ReadFbxArrayBytes(BinaryReader br, int compLen, int enc)
    {
        byte[] rawData = br.ReadBytes(compLen);
        if (enc != 1) return rawData;
        using var ms = new MemoryStream(rawData);
        ms.ReadByte();
        ms.ReadByte(); // Zlib header
        using var ds = new DeflateStream(ms, CompressionMode.Decompress);
        using var outMs = new MemoryStream();
        ds.CopyTo(outMs);
        return outMs.ToArray();
    }

    public static Dictionary<string, CraftingTemplateDef> LoadCraftingTemplates(string xmlPath)
    {
        var dict = new Dictionary<string, CraftingTemplateDef>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(xmlPath)) return dict;

        var doc = XDocument.Load(xmlPath);
        foreach (var el in doc.Descendants("CraftingTemplate"))
        {
            string id = el.Attribute("id")?.Value ?? "";
            if (string.IsNullOrEmpty(id)) continue;

            var t = new CraftingTemplateDef { Id = id };
            var pdRoot = el.Element("PieceDatas");
            if (pdRoot != null)
            {
                foreach (var pd in pdRoot.Elements("PieceData"))
                {
                    if (Enum.TryParse<PieceType>(pd.Attribute("piece_type")?.Value, true, out var pt) &&
                        int.TryParse(pd.Attribute("build_order")?.Value, out int order))
                    {
                        t.BuildOrders.Add(new PieceOrder(pt, order));
                    }
                }
            }
            dict[id] = t;
        }

        if (!dict.ContainsKey("DWDN_1h_spear"))
        {
            dict["DWDN_1h_spear"] = new CraftingTemplateDef
            {
                Id = "DWDN_1h_spear",
                BuildOrders = new List<PieceOrder>
                {
                    new(PieceType.Handle, 0),
                    new(PieceType.Guard, 1),
                    new(PieceType.Blade, 2),
                    new(PieceType.Pommel, -1)
                }
            };
        }

        return dict;
    }

    public static Dictionary<string, CraftingPieceDef> LoadCraftingPieces(string xmlPath)
    {
        var dict = new Dictionary<string, CraftingPieceDef>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(xmlPath)) return dict;

        var doc = XDocument.Load(xmlPath);
        foreach (var el in doc.Descendants("CraftingPiece"))
        {
            string id = el.Attribute("id")?.Value ?? "";
            if (string.IsNullOrEmpty(id)) continue;

            Enum.TryParse<PieceType>(el.Attribute("piece_type")?.Value, true, out var pt);
            float.TryParse(el.Attribute("length")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float len);
            float.TryParse(el.Attribute("weight")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float weight);
            bool fullScale = el.Attribute("full_scale") != null
                ? el.Attribute("full_scale")?.Value == "true"
                : (pt == PieceType.Guard || pt == PieceType.Pommel);

            float pOff = 0f, prevOff = 0f, nextOff = 0f;
            var bd = el.Element("BuildData");
            if (bd != null)
            {
                float.TryParse(bd.Attribute("piece_offset")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out pOff);
                float.TryParse(bd.Attribute("previous_piece_offset")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out prevOff);
                float.TryParse(bd.Attribute("next_piece_offset")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out nextOff);
            }

            dict[id] = new CraftingPieceDef
            {
                Id = id,
                Name = el.Attribute("name")?.Value ?? id,
                PieceType = pt,
                MeshName = el.Attribute("mesh")?.Value ?? id,
                LengthCm = len,
                Weight = weight,
                FullScale = fullScale,
                PieceOffsetCm = pOff,
                PreviousPieceOffsetCm = prevOff,
                NextPieceOffsetCm = nextOff,
                OriginalXml = el
            };
        }

        return dict;
    }

    public static List<CraftedWeaponItemDef> LoadCraftedWeapons(string xmlPath)
    {
        var list = new List<CraftedWeaponItemDef>();
        if (!File.Exists(xmlPath)) return list;

        var doc = XDocument.Load(xmlPath);
        foreach (var el in doc.Descendants("CraftedItem"))
        {
            var w = new CraftedWeaponItemDef
            {
                Id = el.Attribute("id")?.Value ?? "",
                Name = el.Attribute("name")?.Value ?? "",
                TemplateId = el.Attribute("crafting_template")?.Value ?? "OneHandedSword"
            };

            var piecesEl = el.Element("Pieces");
            if (piecesEl != null)
            {
                foreach (var p in piecesEl.Elements("Piece"))
                {
                    if (Enum.TryParse<PieceType>(p.Attribute("Type")?.Value, true, out var pt))
                    {
                        string pid = p.Attribute("id")?.Value ?? "";
                        int scale = 100;
                        if (p.Attribute("scale_factor") != null)
                            int.TryParse(p.Attribute("scale_factor")?.Value, out scale);
                        w.Pieces[pt] = (pid, scale);
                    }
                }
            }
            list.Add(w);
        }

        return list;
    }

    private static void PrintWeaponComparison(CraftedWeaponItemDef weapon, WeaponSimulationResult before, WeaponSimulationResult after)
    {
        Console.WriteLine($"--------------------------------------------------------------------------------");
        Console.WriteLine($"WEAPON: {weapon.Id} (\"{weapon.Name}\") | Template: {weapon.TemplateId}");
        Console.WriteLine($"--------------------------------------------------------------------------------");

        Console.WriteLine("  [BEFORE CALIBRATION (Current XML)]");
        foreach (var kv in before.Placements.OrderBy(k => k.Value.WorldMinZCm))
        {
            var p = kv.Value;
            var d = p.PieceDef;
            Console.WriteLine($"    {p.PieceType,-7} {p.PieceId,-27} | Scale={p.ScalePercent,3}% | XML(L={d.LengthCm,6:F1}, pOff={d.PieceOffsetCm,6:F1}, prev={d.PreviousPieceOffsetCm,6:F1}, next={d.NextPieceOffsetCm,6:F1}) -> WorldZ=[{p.WorldMinZCm,7:F2} .. {p.WorldMaxZCm,7:F2}] cm");
        }
        foreach (var diag in before.Diagnostics)
            Console.WriteLine($"    * {diag}");

        Console.WriteLine("\n  [AFTER AUTO-CALIBRATION (Exact TPAC/FBX Geometry Fit)]");
        foreach (var kv in after.Placements.OrderBy(k => k.Value.WorldMinZCm))
        {
            var p = kv.Value;
            var d = p.PieceDef;
            Console.WriteLine($"    {p.PieceType,-7} {p.PieceId,-27} | Scale={p.ScalePercent,3}% | XML(L={d.LengthCm,6:F1}, pOff={d.PieceOffsetCm,6:F1}, prev={d.PreviousPieceOffsetCm,6:F1}, next={d.NextPieceOffsetCm,6:F1}) -> WorldZ=[{p.WorldMinZCm,7:F2} .. {p.WorldMaxZCm,7:F2}] cm");
        }
        foreach (var diag in after.Diagnostics)
            Console.WriteLine($"    * {diag}");
        Console.WriteLine($"    * Reach (CraftedWeaponLength): {after.CraftedWeaponLengthCm:F1} cm | Total Tip-to-Pommel: {after.TotalLengthCm:F1} cm\n");
    }

    public static void SaveCalibratedPiecesXml(string originalPath, Dictionary<string, CraftingPieceDef> calibrated, string outputPath)
    {
        var doc = XDocument.Load(originalPath);
        foreach (var el in doc.Descendants("CraftingPiece"))
        {
            string id = el.Attribute("id")?.Value ?? "";
            if (!calibrated.TryGetValue(id, out var piece)) continue;

            el.SetAttributeValue("mesh", piece.MeshName);
            el.SetAttributeValue("length", piece.LengthCm.ToString("0.##", CultureInfo.InvariantCulture));

            var bd = el.Element("BuildData");
            if (bd == null)
            {
                bd = new XElement("BuildData");
                el.AddFirst(bd);
            }
            bd.SetAttributeValue("piece_offset", piece.PieceOffsetCm.ToString("0.##", CultureInfo.InvariantCulture));
            bd.SetAttributeValue("previous_piece_offset", piece.PreviousPieceOffsetCm.ToString("0.##", CultureInfo.InvariantCulture));
            bd.SetAttributeValue("next_piece_offset", piece.NextPieceOffsetCm.ToString("0.##", CultureInfo.InvariantCulture));
        }

        doc.Save(outputPath);
    }

    private static void RunCustomCliAssembly(
        string[] args,
        Dictionary<string, MeshGeometryInfo> geometries,
        Dictionary<string, CraftingTemplateDef> templates,
        Dictionary<string, CraftingPieceDef> pieces,
        string outputDir)
    {
        string id = GetArg(args, "--id", "custom_calibrated_weapon");
        string templateId = GetArg(args, "--template", "Daewden_BastardSword");
        string bladeArg = GetArg(args, "--blade", "");
        string guardArg = GetArg(args, "--guard", "");
        string handleArg = GetArg(args, "--handle", "");
        string pommelArg = GetArg(args, "--pommel", "");

        var weapon = new CraftedWeaponItemDef
        {
            Id = id,
            Name = id,
            TemplateId = templateId
        };

        void AddPart(PieceType pt, string spec)
        {
            if (string.IsNullOrEmpty(spec)) return;
            var parts = spec.Split(':');
            string pid = parts[0];
            int scale = parts.Length > 1 && int.TryParse(parts[1], out int s) ? s : 100;
            weapon.Pieces[pt] = (pid, scale);
            if (!pieces.ContainsKey(pid))
            {
                pieces[pid] = new CraftingPieceDef
                {
                    Id = pid,
                    Name = pid,
                    PieceType = pt,
                    MeshName = pid,
                    LengthCm = 10f,
                    Weight = 0.5f,
                    FullScale = (pt == PieceType.Guard || pt == PieceType.Pommel)
                };
            }
        }

        AddPart(PieceType.Blade, bladeArg);
        AddPart(PieceType.Guard, guardArg);
        AddPart(PieceType.Handle, handleArg);
        AddPart(PieceType.Pommel, pommelArg);

        if (!templates.TryGetValue(templateId, out var tmpl))
            tmpl = templates.Values.First();

        AutoCalibrateWeapon(weapon, tmpl, pieces, geometries);
        var sim = SimulateWeapon(weapon, tmpl, pieces, geometries);

        Console.WriteLine($"\n[+] Custom Assembled Weapon '{id}' (Template: {templateId}):");
        foreach (var kv in sim.Placements.OrderBy(k => k.Value.WorldMinZCm))
        {
            var p = kv.Value;
            var d = p.PieceDef;
            Console.WriteLine($"  - {p.PieceType,-7} ({p.PieceId}) | mesh=\"{d.MeshName}\" length=\"{d.LengthCm:0.##}\" | <BuildData piece_offset=\"{d.PieceOffsetCm:0.##}\" previous_piece_offset=\"{d.PreviousPieceOffsetCm:0.##}\" next_piece_offset=\"{d.NextPieceOffsetCm:0.##}\" />");
        }
        foreach (var diag in sim.Diagnostics)
            Console.WriteLine($"  * {diag}");
    }

    private static string GetArg(string[] args, string key, string defaultVal)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return defaultVal;
    }

    private static void GenerateHtmlBlueprint(
        List<WeaponSimulationResult> beforeList,
        List<WeaponSimulationResult> afterList,
        Dictionary<string, CraftingPieceDef> calibratedPieces,
        string htmlPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang='ru'><head><meta charset='utf-8'/>");
        sb.AppendLine("<title>DWDN Weapon Auto-Calibrator Blueprint</title></head><body></body></html>");
        File.WriteAllText(htmlPath, sb.ToString(), Encoding.UTF8);
    }
}
