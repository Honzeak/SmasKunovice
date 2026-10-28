#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#ifndef DeployBroker
  #define DeployBroker "true"
#endif

[Setup]
AppName=SMASKunovice
AppVersion={#AppVersion}
DefaultDirName={autopf}\SmasKunovice
DefaultGroupName=SMASKunovice
OutputDir=.\output
OutputBaseFilename=SMASKunovice_v{#AppVersion}
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin

[Files]
; Copy Desktop Client App files
Source: "publish\app\*"; DestDir: "{app}\ClientApp"; Flags: ignoreversion recursesubdirs createallsubdirs


#if DeployBroker == "true"
; Copy Broker Service binaries (appsettings.json will be dynamically created by code below)
Source: "publish\broker\*"; DestDir: "{app}\MqttBroker"; Flags: ignoreversion recursesubdirs createallsubdirs; AfterInstall: CreateBrokerConfig
#endif

[Icons]
Name: "{autodesktop}\SMAS Kunovice"; Filename: "{app}\ClientApp\SmasKunovice.Avalonia.exe"; WorkingDir: "{app}\ClientApp"

[Run]
#if DeployBroker == "true"
; 1. Register the MQTT broker executable as a Windows Service
Filename: "sc.exe"; Parameters: "create LocalMqttBrokerService binPath= ""{app}\MqttBroker\SmasKunovice.BrokerService.exe"" start= auto"; Flags: runhidden

; 2. Add description and start the service
Filename: "sc.exe"; Parameters: "description LocalMqttBrokerService ""Embedded local MQTT broker service for application telemetry."""; Flags: runhidden
Filename: "net.exe"; Parameters: "start LocalMqttBrokerService"; Flags: runhidden

; 3. Open Windows Firewall Port 1883
Filename: "netsh.exe"; Parameters: "advfirewall firewall add rule name=""Local MQTT Broker (Port 1883)"" dir=in action=allow protocol=TCP localport=1883 remoteip=LocalSubnet"; Flags: runhidden
#endif

[UninstallRun]
#if DeployBroker == "true"
Filename: "net.exe"; Parameters: "stop LocalMqttBrokerService"; Flags: runhidden; RunOnceId: "StopBrokerService"
Filename: "sc.exe"; Parameters: "delete LocalMqttBrokerService"; Flags: runhidden; RunOnceId: "DeleteBrokerService"
Filename: "netsh.exe"; Parameters: "advfirewall firewall delete rule name=""Local MQTT Broker (Port 1883)"""; Flags: runhidden; RunOnceId: "RemoveFirewallRule"
#endif

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
var
  CredentialsPage: TInputQueryWizardPage;

procedure InitializeWizard();
begin
  // Create a custom wizard page asking for MQTT Credentials right after directory selection
  CredentialsPage := CreateInputQueryPage(wpSelectDir,
    'MQTT Broker Authentication', 'Configure security credentials',
    'Please enter the username and password that your vendor devices and desktop app will use to communicate with the local MQTT broker.');

  CredentialsPage.Add('Username:', False);
  CredentialsPage.Add('Password:', True);
  CredentialsPage.Values[0] := 'vendor_client';
  CredentialsPage.Values[1] := '';
end;

procedure CreateBrokerConfig;
var
  ConfigFile: string;
  ConfigContent: string;
begin
    ConfigFile := ExpandConstant('{app}\MqttBroker\appsettings.json');
    ConfigContent := '{' + #13#10 +
                     '  "Logging": {' + #13#10 +
                     '    "LogLevel": {' + #13#10 +
                     '      "Default": "Information",' + #13#10 +
                     '      "Microsoft.Hosting.Lifetime": "Information"' + #13#10 +
                     '    }' + #13#10 +
                     '  },' + #13#10 +
                     '  "MqttBroker": {' + #13#10 +
                     '    "Username": "' + CredentialsPage.Values[0] + '",' + #13#10 +
                     '    "Password": "' + CredentialsPage.Values[1] + '"' + #13#10 +
                     '  }' + #13#10 +
                     '}';

    SaveStringToFile(ConfigFile, ConfigContent, False);
    
    ConfigFile := ExpandConstant('{app}\ClientApp\appsettings.User.json');
    ConfigContent := '{' + #13#10 +
                     '  "ClientAdapterOptions": {' + #13#10 +
                     '    "Username": "' + CredentialsPage.Values[0] + '",' + #13#10 +
                     '    "Password": "' + CredentialsPage.Values[1] + '"' + #13#10 +
                     '  }' + #13#10 +
                     '}';

    SaveStringToFile(ConfigFile, ConfigContent, False);
end;