# Compile Nettoyage PC (C#) avec Roslyn, contre les références de .NET Framework 4.8
param([string]$Sortie = 'NettoyagePC.exe', [string[]]$Sources = @(), [string]$Manifeste = '', [string]$Icone = '', [string]$Version = '4.0.0.0', [switch]$Debogage)
$ErrorActionPreference = 'Stop'
$ref = '/usr/lib/mono/4.8-api'
$facades = Join-Path $ref 'Facades'
$noms = @('mscorlib', 'System', 'System.Core', 'System.Drawing', 'System.Windows.Forms', 'System.Management', 'System.Web.Extensions', 'Microsoft.VisualBasic', 'System.Xml', 'System.Xml.Linq', 'Microsoft.CSharp', 'System.ServiceProcess', 'System.Runtime.Serialization', 'System.Data', 'System.Security')
$refs = New-Object System.Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]
foreach ($n in $noms) { $refs.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile((Join-Path $ref "$n.dll"))) }
if (Test-Path $facades) { foreach ($f in Get-ChildItem $facades -Filter *.dll) { if ($f.Name -match '^(System\.Runtime|netstandard|System\.ValueTuple)\.dll$') { $refs.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($f.FullName)) } } }
if ($Sources.Count -eq 0) { $Sources = @(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Recurse -Filter *.cs | ForEach-Object FullName) }
$opts = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::new([Microsoft.CodeAnalysis.CSharp.LanguageVersion]::CSharp10)
$arbres = [System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
foreach ($s in $Sources) { $arbres.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($s), $opts, $s, [Text.Encoding]::UTF8)) }
$niveau = if ($Debogage) { [Microsoft.CodeAnalysis.OptimizationLevel]::Debug } else { [Microsoft.CodeAnalysis.OptimizationLevel]::Release }
$copt = [Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::WindowsApplication).WithOptimizationLevel($niveau).WithPlatform([Microsoft.CodeAnalysis.Platform]::AnyCpu).WithAllowUnsafe($true).WithWarningLevel(4)
$comp = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create([IO.Path]::GetFileNameWithoutExtension($Sortie), $arbres, $refs, $copt)
$diag = $comp.GetDiagnostics() | Where-Object { $_.Severity -ge [Microsoft.CodeAnalysis.DiagnosticSeverity]::Warning -and $_.Id -notin @('CS1701', 'CS1702', 'CS8019') }
$erreurs = @($diag | Where-Object { $_.Severity -eq 'Error' })
foreach ($d in ($diag | Select-Object -First 80)) { $d.ToString() }
if ($erreurs.Count -gt 0) { Write-Host ("{0} erreur(s)" -f $erreurs.Count); exit 1 }
$man = $null; $ico = $null
if ($Manifeste) { $man = [IO.File]::OpenRead($Manifeste) }
if ($Icone) { $ico = [IO.File]::OpenRead($Icone) }
$res = $comp.CreateDefaultWin32Resources($true, ($null -eq $man), $man, $ico)
# Textes légaux intégrés au programme (ressources « legal.<nom>.txt »)
if (-not ('RessourcesNP' -as [type])) { Add-Type -TypeDefinition 'public static class RessourcesNP { public static System.Func<System.IO.Stream> De(byte[] b) { return () => new System.IO.MemoryStream(b, false); } }' }
$ressources = [System.Collections.Generic.List[Microsoft.CodeAnalysis.ResourceDescription]]::new()
$dossierLegal = Join-Path $PSScriptRoot 'legal'
if (Test-Path $dossierLegal) { foreach ($f in Get-ChildItem $dossierLegal -Filter *.txt) { $ressources.Add([Microsoft.CodeAnalysis.ResourceDescription]::new('legal.' + $f.Name, [RessourcesNP]::De([IO.File]::ReadAllBytes($f.FullName)), $true)) } }
$fs = [IO.File]::Create($Sortie)
$pdb = $null
try { $r = $comp.Emit($fs, $pdb, $null, $res, $ressources) } finally { $fs.Dispose(); $res.Dispose(); if ($man) { $man.Dispose() }; if ($ico) { $ico.Dispose() } }
if (-not $r.Success) { foreach ($d in $r.Diagnostics) { $d.ToString() }; exit 1 }
Write-Host ("Compilé : {0} ({1:N0} octets)" -f $Sortie, (Get-Item $Sortie).Length)
