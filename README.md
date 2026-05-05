# ESCRS IOL Calculator Mockup

## Run Locally

```bash
dotnet restore
dotnet run
```

Open the URL shown by `dotnet run`.

## Deploy to GitHub Pages

```bash
dotnet publish -c Release
```

Deploy the contents of:

```text
bin/Release/net10.0/publish/wwwroot
```

to GitHub Pages. The app is configured with `<base href="./">` and `wwwroot/.nojekyll` for project-page hosting.
