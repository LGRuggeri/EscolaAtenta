; ============================================================================
; EscolaAtenta — Instalador Local (Inno Setup)
;
; Pré-requisito para gerar os binários:
;   dotnet publish src\EscolaAtenta.API\EscolaAtenta.API.csproj -p:PublishProfile=win-x64-selfcontained
;   dotnet publish src\EscolaAtenta.TrayMonitor\EscolaAtenta.TrayMonitor.csproj -p:PublishProfile=win-x64-selfcontained
;
; Compile este script com o Inno Setup Compiler (iscc.exe).
; ============================================================================

[Setup]
AppName=EscolaAtenta
AppVersion=1.4.2
AppPublisher=EscolaAtenta
DefaultDirName=C:\EscolaAtenta
DefaultGroupName=EscolaAtenta
OutputBaseFilename=EscolaAtenta-Setup-1.4.2
OutputDir=installer-output
Compression=lzma2
SolidCompression=yes
; Requer privilégio de admin para registrar o serviço do Windows
PrivilegesRequired=admin
; Arquitetura x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Não permite alterar o diretório (padroniza C:\EscolaAtenta)
DisableDirPage=yes
; Versão mínima: Windows 10
MinVersion=10.0

[Files]
Source: "scripts\Protect-Installation.ps1"; Flags: dontcopy
; Binários da API (Self-Contained)
Source: "src\EscolaAtenta.API\bin\Publish\win-x64\*"; DestDir: "{app}\API"; Flags: ignoreversion recursesubdirs createallsubdirs

; Binários do Tray Monitor (Self-Contained)
Source: "src\EscolaAtenta.TrayMonitor\bin\Publish\win-x64\*"; DestDir: "{app}\TrayMonitor"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Atalho do Tray Monitor na pasta de Inicialização do Windows (inicia com o PC)
Name: "{commonstartup}\EscolaAtenta Monitor"; Filename: "{app}\TrayMonitor\EscolaAtenta.TrayMonitor.exe"

; Atalho no Menu Iniciar
Name: "{group}\EscolaAtenta Monitor"; Filename: "{app}\TrayMonitor\EscolaAtenta.TrayMonitor.exe"
Name: "{group}\Desinstalar EscolaAtenta"; Filename: "{uninstallexe}"

[Run]
; Pós-instalação: Registrar a API como Serviço do Windows (inicialização automática)
Filename: "sc.exe"; Parameters: "create EscolaAtenta binPath= ""{app}\API\EscolaAtenta.API.exe"" start= auto"; Flags: runhidden waituntilterminated

; Iniciar o serviço imediatamente após o registro
Filename: "sc.exe"; Parameters: "start EscolaAtenta"; Flags: runhidden waituntilterminated

; Iniciar o Tray Monitor após a instalação
Filename: "{app}\TrayMonitor\EscolaAtenta.TrayMonitor.exe"; Description: "Iniciar o Monitor EscolaAtenta"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Antes de desinstalar: Parar o serviço
Filename: "sc.exe"; Parameters: "stop EscolaAtenta"; Flags: runhidden waituntilterminated; RunOnceId: "StopService"

; Remover o registro do serviço do Windows
Filename: "sc.exe"; Parameters: "delete EscolaAtenta"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteService"

; Encerrar o Tray Monitor se estiver rodando
Filename: "taskkill.exe"; Parameters: "/F /IM EscolaAtenta.TrayMonitor.exe"; Flags: runhidden; RunOnceId: "KillTrayMonitor"

[Dirs]
; ACLs restritas são aplicadas pelo script antes e depois da cópia.
Name: "{app}"
; Criar pasta de Logs com permissão para o serviço escrever
Name: "{app}\Logs"

[Code]
procedure ProtegerInstalacao;
var
  ResultCode: Integer;
begin
  ExtractTemporaryFile('Protect-Installation.ps1');
  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
    ExpandConstant('{tmp}\Protect-Installation.ps1') + '" -InstallationRoot "' +
    ExpandConstant('{app}') + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    RaiseException('Não foi possível aplicar as permissões de segurança.');
  if ResultCode <> 0 then
    RaiseException('Falha ao proteger dados locais. A instalação não pode continuar.');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  { Protege também dados de instalações antigas antes de copiar novos arquivos.
    A segunda passagem remove ACLs explícitas herdadas de versões anteriores.
    ssPostInstall ocorre antes das entradas [Run], portanto antes do serviço. }
  if (CurStep = ssInstall) or (CurStep = ssPostInstall) then
    ProtegerInstalacao;
end;
