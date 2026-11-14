$ErrorActionPreference = "Stop"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " [DWDN] Sacred Sync Ritual: DWDN_TRY -> DaewdenM (Dedicated Server)" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 0. Directories
$masterDir     = "E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\DWDN_TRY"
$serverDir     = "E:\SteamLibrary\steamapps\common\Mount & Blade II Dedicated Server\Modules\DaewdenM"
$serverDir2    = "E:\SteamLibrary\steamapps\common\Mount & Blade II Dedicated Server\Modules\DWDN_TRY"
$serverBinDir  = "E:\SteamLibrary\steamapps\common\Mount & Blade II Dedicated Server\bin\Win64_Shipping_Server"
$banAiDir      = "d:\banAImod\DWDN_TRY"

$masterClientDll = "$masterDir\bin\Win64_Shipping_Client\DWDN_Script.dll"
$masterClientPdb = "$masterDir\bin\Win64_Shipping_Client\DWDN_Script.pdb"
$masterServerDll = "$masterDir\bin\Win64_Shipping_Server\DWDN_Script.Server.dll"
$masterServerPdb = "$masterDir\bin\Win64_Shipping_Server\DWDN_Script.Server.pdb"

# 1. Stop any background server processes holding file locks
$procs = Get-Process -Name "*DedicatedCustomServer*", "*Bannerlord*" -ErrorAction SilentlyContinue
if ($procs) {
    Write-Host "Terminating active game/server processes to release DLL locks..." -ForegroundColor Yellow
    $procs | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}

# 2. Ensure target directories exist
@(
    "$serverDir\bin\Win64_Shipping_Client",
    "$serverDir\bin\Win64_Shipping_Server",
    "$serverDir2\bin\Win64_Shipping_Client",
    "$serverDir2\bin\Win64_Shipping_Server",
    "$serverBinDir",
    "$banAiDir\bin\Win64_Shipping_Client"
) | ForEach-Object {
    if (-not (Test-Path $_)) { New-Item -ItemType Directory -Force -Path $_ | Out-Null }
}

# 3. Deploy DLLs & PDBs to Dedicated Server (DaewdenM + DWDN_TRY + server bin)
Write-Host "Deploying server binaries..." -ForegroundColor Gray
Copy-Item $masterClientDll "$serverDir\bin\Win64_Shipping_Client\DWDN_Script.dll" -Force
Copy-Item $masterClientDll "$serverDir2\bin\Win64_Shipping_Client\DWDN_Script.dll" -Force
Copy-Item $masterClientDll "$serverBinDir\DWDN_Script.dll" -Force
if (Test-Path $masterClientPdb) { 
    Copy-Item $masterClientPdb "$serverDir\bin\Win64_Shipping_Client\DWDN_Script.pdb" -Force 
    Copy-Item $masterClientPdb "$serverDir2\bin\Win64_Shipping_Client\DWDN_Script.pdb" -Force 
    Copy-Item $masterClientPdb "$serverBinDir\DWDN_Script.pdb" -Force 
}

Copy-Item $masterServerDll "$serverDir\bin\Win64_Shipping_Client\DWDN_Script.Server.dll" -Force
Copy-Item $masterServerDll "$serverDir\bin\Win64_Shipping_Server\DWDN_Script.Server.dll" -Force
Copy-Item $masterServerDll "$serverDir2\bin\Win64_Shipping_Client\DWDN_Script.Server.dll" -Force
Copy-Item $masterServerDll "$serverDir2\bin\Win64_Shipping_Server\DWDN_Script.Server.dll" -Force
Copy-Item $masterServerDll "$serverBinDir\DWDN_Script.Server.dll" -Force
if (Test-Path $masterServerPdb) {
    Copy-Item $masterServerPdb "$serverDir\bin\Win64_Shipping_Client\DWDN_Script.Server.pdb" -Force
    Copy-Item $masterServerPdb "$serverDir\bin\Win64_Shipping_Server\DWDN_Script.Server.pdb" -Force
    Copy-Item $masterServerPdb "$serverDir2\bin\Win64_Shipping_Client\DWDN_Script.Server.pdb" -Force
    Copy-Item $masterServerPdb "$serverDir2\bin\Win64_Shipping_Server\DWDN_Script.Server.pdb" -Force
    Copy-Item $masterServerPdb "$serverBinDir\DWDN_Script.Server.pdb" -Force
}

# 4. Mirror scripts from Master DWDN_TRY to Server and banAiDir
Write-Host "Mirroring scripts across modules..." -ForegroundColor Gray
Copy-Item "$masterDir\ModuleData\scripts\*" "$serverDir\ModuleData\scripts\" -Recurse -Force
Copy-Item "$masterDir\ModuleData\scripts\*" "$serverDir2\ModuleData\scripts\" -Recurse -Force
Copy-Item "$masterDir\ModuleData\scripts\*" "$banAiDir\ModuleData\scripts\" -Recurse -Force

# 5. Mirror SubModule.xml
Write-Host "Mirroring SubModule.xml..." -ForegroundColor Gray
Copy-Item "$masterDir\SubModule.xml" "$serverDir\SubModule.xml" -Force
Copy-Item "$masterDir\SubModule.xml" "$serverDir2\SubModule.xml" -Force
Copy-Item "$masterDir\SubModule.xml" "$banAiDir\SubModule.xml" -Force

# 6. Mirror XMLs
Write-Host "Mirroring XML data..." -ForegroundColor Gray
$xmlFiles = @(
    "mpclassdivisions.xml",
    "mpcharacters.xml",
    "mpcultures.xml",
    "crafting_pieces.xml",
    "crafting_templates.xml",
    "weapon_descriptions.xml",
    "artillery_config.xml"
)
foreach ($xml in $xmlFiles) {
    $srcXml = "$masterDir\ModuleData\$xml"
    if (Test-Path $srcXml) {
        Copy-Item $srcXml "$serverDir\ModuleData\$xml" -Force
        Copy-Item $srcXml "$serverDir2\ModuleData\$xml" -Force
        Copy-Item $srcXml "$banAiDir\ModuleData\$xml" -Force
    }
}

# 7. Mirror mpitems (weapons, armors, shields, banners)
if (Test-Path "$masterDir\ModuleData\mpitems") {
    Write-Host "Mirroring mpitems directory..." -ForegroundColor Gray
    New-Item -ItemType Directory -Force -Path "$serverDir\ModuleData\mpitems" | Out-Null
    New-Item -ItemType Directory -Force -Path "$serverDir2\ModuleData\mpitems" | Out-Null
    New-Item -ItemType Directory -Force -Path "$banAiDir\ModuleData\mpitems" | Out-Null
    Copy-Item "$masterDir\ModuleData\mpitems\*" "$serverDir\ModuleData\mpitems\" -Recurse -Force
    Copy-Item "$masterDir\ModuleData\mpitems\*" "$serverDir2\ModuleData\mpitems\" -Recurse -Force
    Copy-Item "$masterDir\ModuleData\mpitems\*" "$banAiDir\ModuleData\mpitems\" -Recurse -Force
}

Write-Host "Sync successful! Master DWDN_TRY -> DaewdenM (Dedicated Server) fully deployed." -ForegroundColor Green
