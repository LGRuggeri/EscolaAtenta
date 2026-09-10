param([Parameter(Mandatory = $true)][string]$InstallationRoot)
$ErrorActionPreference = 'Stop'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Execute com elevação administrativa antes de alterar permissões.'
}
$root = [IO.Path]::GetFullPath($InstallationRoot).TrimEnd('\')
if ($root -eq [IO.Path]::GetPathRoot($root).TrimEnd('\')) { throw 'A raiz de um volume não é um destino permitido.' }
# Não seguir junctions/symlinks: nenhuma alteração pode alcançar outro diretório.
function Get-SafeTree([string]$Path) {
    $item = Get-Item -LiteralPath $Path -Force
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Link não permitido: $Path" }
    $item
    if ($item.PSIsContainer) {
        foreach ($child in Get-ChildItem -LiteralPath $Path -Force) { Get-SafeTree $child.FullName }
    }
}
New-Item -ItemType Directory -Path $root -Force | Out-Null
if ((Get-Item -LiteralPath $root -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Raiz da instalação não pode ser link.' }
foreach ($name in @('API', 'Logs', 'TrayMonitor')) {
    New-Item -ItemType Directory -Path (Join-Path $root $name) -Force | Out-Null
}
$items = @(Get-SafeTree $root)
$tray = Join-Path $root 'TrayMonitor'
foreach ($item in $items) {
    if ($item.PSIsContainer) { $acl = New-Object Security.AccessControl.DirectorySecurity }
    else { $acl = New-Object Security.AccessControl.FileSecurity }
    $acl.SetAccessRuleProtection($true, $false)
    $inherit = [Security.AccessControl.InheritanceFlags]::None
    if ($item.PSIsContainer) { $inherit = [Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit' }
    foreach ($sid in @('S-1-5-18', 'S-1-5-32-544')) {
        $identity = New-Object Security.Principal.SecurityIdentifier($sid)
        $rule = New-Object Security.AccessControl.FileSystemAccessRule($identity, 'FullControl', $inherit, 'None', 'Allow')
        $acl.AddAccessRule($rule)
    }
    # Usuários comuns podem atravessar a raiz e executar apenas o monitor.
    if ($item.FullName -eq $root -or $item.FullName -eq $tray -or $item.FullName.StartsWith($tray + '\', [StringComparison]::OrdinalIgnoreCase)) {
        $readInheritance = $inherit
        if ($item.FullName -eq $root) { $readInheritance = [Security.AccessControl.InheritanceFlags]::None }
        $identity = New-Object Security.Principal.SecurityIdentifier('S-1-5-32-545')
        $rule = New-Object Security.AccessControl.FileSystemAccessRule($identity, 'ReadAndExecute', $readInheritance, 'None', 'Allow')
        $acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $item.FullName -AclObject $acl
}
