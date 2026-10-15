# Windows Memory Cleaner

Fork de [IgorMundstein/WinMemoryCleaner](https://github.com/IgorMundstein/WinMemoryCleaner), migrado para **.NET 10** com foco em baixo consumo de CPU e memória em idle.

Otimiza as áreas de memória do Windows usando a API nativa (working set, standby list, modified page list, file cache, etc.).

## Diferenças em relação ao original

- Alvo `net10.0-windows` (o original usa .NET Framework 4.0). Requer **Windows 10 1607+** e o runtime [.NET 10 Desktop](https://dotnet.microsoft.com/download/dotnet/10.0).
- Removidos dois loops de espera ativa nos monitores (`MonitorApp`/`MonitorComputer`) que ocupavam um núcleo de CPU durante a otimização.
- GC concorrente desligado para reduzir threads e memória em idle.
- Serviço do Windows instalado via `sc.exe` (o `System.Configuration.Install` não existe no .NET moderno).
- Testes migrados para NUnit 3 e executados com `dotnet test`.
- Auto-update aponta para as releases deste repositório.

## Build

```cmd
dotnet build src -c Release
dotnet test src -c Release
dotnet publish src/WinMemoryCleaner.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:PublishReadyToRun=true -o publish
```

O aplicativo exige execução como administrador (manifest `requireAdministrator`).

## Release

O workflow `release.yml` roda a cada push em `main` (alterando `src/**`) e usa a versão de `AssemblyVersion` em `src/Properties/AssemblyInfo.cs` (Major.Minor.Patch) como tag. Para lançar uma nova versão, atualize esse valor; se a tag já existir, o release é reaproveitado e nenhum novo é criado.

## Licença

[GPL-3.0](LICENSE). Código original de Igor Mundstein; modificações deste fork sob a mesma licença.
