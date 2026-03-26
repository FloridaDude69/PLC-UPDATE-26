# PLC-UPDATE-26
PLC Update Blazor

## Run Locally

From the repository root:

```powershell
dotnet run --project ".\PLC-UPDATE-26\PLC-UPDATE-26.csproj" --urls "http://localhost:5288"
```

If you get a Windows `Access is denied` error when launching the generated `.exe`, use this fallback:

```powershell
dotnet build ".\PLC-UPDATE-26.slnx" -v minimal
$env:ASPNETCORE_URLS = "http://localhost:5288"
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_STATICWEBASSETS = "c:\Users\634647\source\repos\PLC-UPDATE-26\PLC-UPDATE-26\bin\Debug\net10.0\PLC-UPDATE-26.staticwebassets.runtime.json"
dotnet "c:\Users\634647\source\repos\PLC-UPDATE-26\PLC-UPDATE-26\bin\Debug\net10.0\PLC-UPDATE-26.dll" --contentRoot "c:\Users\634647\source\repos\PLC-UPDATE-26\PLC-UPDATE-26"
```

Then open:

http://localhost:5288/file-updates
