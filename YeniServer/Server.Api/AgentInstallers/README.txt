AGENT INSTALLER SETUP
=====================

Bu klasöre agent executable'ı koymanız gerekiyor.

ADIMLAR:
========

1. Agent projesini Release modda build edin:
   cd "C:\Users\ÖMERÖLMEZ\OneDrive - SiteTelekom\Masaüstü\Yeni klasör\YeniAgent\AgentHost"
   dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true

2. Oluşan AgentHost.exe dosyasını buraya kopyalayın:
   C:\Users\ÖMERÖLMEZ\OneDrive - SiteTelekom\Masaüstü\Yeni klasör\YeniServer\Server.Api\AgentInstallers\AgentHost.exe

3. Agent installer download endpoint kullanıma hazır olacak:
   GET /api/agentinstaller/download/windows?deviceName=PC01&groupName=Marketing

NOTLAR:
=======
- AgentHost.exe dosyası burada olmalı
- Her build'de güncel agent'ı buraya kopyalayın
- Download endpoint otomatik olarak config dosyası oluşturacak
- ZIP içinde AgentHost.exe + agentconfig.json + install.bat olacak
