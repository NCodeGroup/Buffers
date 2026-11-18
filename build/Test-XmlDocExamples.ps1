#requires -version 7
<#
.SYNOPSIS
    Compiles every example in the XML documentation of src/NCode.Buffers, so each example the package ships to
    IntelliSense compiles as written.
.DESCRIPTION
    Each <code> block of a /// comment becomes a method body in artifacts/xml-examples/Examples.cs, with #line
    directives that point each error at its source line, and the script builds that file against the library. A
    <code> tag that is not alone on its /// line fails the check, so no example is skipped. An example may call
    ProcessData(ReadOnlySpan<byte>) and read memoryOwner, an IMemoryOwner<byte>; the script declares both.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/NCode.Buffers/NCode.Buffers.csproj'
$source = Join-Path $root 'src/NCode.Buffers'
# Under the repository, so its global.json and nuget.config apply; artifacts/ is ignored by git and CSharpier.
$out = Join-Path $root 'artifacts/xml-examples'
New-Item -ItemType Directory -Force -Path $out | Out-Null

$framework = @(([xml](Get-Content -Raw -LiteralPath $project)).Project.PropertyGroup.TargetFramework | Where-Object { $_ })[0]
if (-not $framework) { throw "XML examples: $project names no TargetFramework." }

$code = [Text.StringBuilder]::new()
foreach ($line in 'using System.Buffers;', 'using System.Security.Cryptography;', 'using System.Text;', 'using NCode.Buffers;',
    'using Nerdbank.Streams;', '#pragma warning disable', 'internal static class Examples', '{',
    '    private static void ProcessData(ReadOnlySpan<byte> data) { }',
    '    private static IMemoryOwner<byte> memoryOwner = null!;') { [void]$code.AppendLine($line) }
$count = 0
foreach ($file in Get-ChildItem -LiteralPath $source -Filter *.cs -Recurse | Sort-Object FullName) {
    $lines = [IO.File]::ReadAllLines($file.FullName)
    $relative = [IO.Path]::GetRelativePath($root, $file.FullName) -replace '\\', '/'
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($lines[$i].Trim() -cne '/// <code>') { continue }
        $start = $i + 2
        $count++
        [void]$code.AppendLine("    private static void Example$count()")
        [void]$code.AppendLine('    {')
        # The compiler opens the file a #line directive names, so it takes the absolute path.
        [void]$code.AppendLine("#line $start `"$($file.FullName)`"")
        for ($i++; $i -lt $lines.Length -and $lines[$i].Trim() -cne '/// </code>'; $i++) {
            $text = $lines[$i] -replace '^\s*/// ?', ''
            [void]$code.AppendLine($text.Replace('&lt;', '<').Replace('&gt;', '>').Replace('&amp;', '&'))
        }
        if ($i -ge $lines.Length) { throw "XML examples: the <code> block at $relative line $($start - 1) has no </code>." }
        [void]$code.AppendLine('#line default')
        [void]$code.AppendLine('    }')
    }
}
[void]$code.AppendLine('}')
if (-not $count) { throw "XML examples: $source has no <code> block; the extraction is broken." }
# The loop above takes only a <code> tag alone on its /// line; fail on any other instead of dropping its example.
$tags = @(Get-ChildItem -LiteralPath $source -Filter *.cs -Recurse | Select-String -Pattern '^\s*///.*<code\b' -CaseSensitive)
if ($tags.Count -ne $count) {
    "XML examples: FAILED. $($tags.Count) <code> tags in $source, but $count examples extracted; put each <code> and </code> alone on its /// line:"
    $tags | Where-Object { $_.Line.Trim() -cne '/// <code>' } | ForEach-Object { "  $([IO.Path]::GetRelativePath($root, $_.Path) -replace '\\', '/'):$($_.LineNumber)" }
    exit 1
}

$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $out 'Examples.cs'), $code.ToString(), $utf8)
$reference = [IO.Path]::GetRelativePath($out, $project)
[IO.File]::WriteAllText((Join-Path $out 'Examples.csproj'), @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$framework</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$reference" />
  </ItemGroup>
</Project>
"@, $utf8)
# The scratch project takes none of the repository's build settings: it builds the examples as a consumer would.
foreach ($name in 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props') {
    [IO.File]::WriteAllText((Join-Path $out $name), "<Project />`n", $utf8)
}

dotnet build (Join-Path $out 'Examples.csproj') --nologo -v quiet
if ($LASTEXITCODE) { "XML examples: FAILED. An example in the XML documentation of $source does not compile; the errors above name its file and line."; exit 1 }
"XML examples: PASSED ($count examples)."
exit 0
