
## Yayim Talimatlari
- Boyutu minimum tutmak icin framework-bagimli yayinlayin:
  ```bash
  dotnet publish AgentHost -c Release -r win-x64 --self-contained false
  ```
- Bu komut tek dosya (PublishSingleFile) ve trimming ile yaklasik 12 MB boyutlu `AgentHost.exe` uretir.
- .NET 8 runtime yuklu degilse, bir defalik `dotnet-runtime-8` kurulmasi gerekir.

