# ESCRS IOL Calculator Mockup

## Run Locally

```bash
dotnet restore
dotnet run
```

Open the URL shown by `dotnet run`.

## Formula Rules

Formula support, required fields, conditional requirements, and numeric ranges are authored in:

```text
wwwroot/data/formula-rules.json
```

See [Formula Rules Authoring Guide](docs/formula-rules-authoring.md) before editing the rule file.

## Deploy to GitHub Pages

```bash
dotnet publish -c Release
```

Deploy the contents of:

```text
bin/Release/net10.0/publish/wwwroot
```

to GitHub Pages. The app is configured with `<base href="./">` and `wwwroot/.nojekyll` for project-page hosting.
