# Builds dc.exe and shot.exe with the C# compiler that ships with Windows (.NET Framework 4).
$ErrorActionPreference = 'Stop'
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
Push-Location $PSScriptRoot
try {
    & $csc /nologo /optimize /out:dc.exe dc.cs
    & $csc /nologo /optimize /r:System.Drawing.dll /out:shot.exe shot.cs
} finally {
    Pop-Location
}
