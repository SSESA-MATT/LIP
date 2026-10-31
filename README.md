# Legal Intelligence Platform

LIP is the Otic legal operations product. The interface uses the CIP web visual system. The workflows follow the LIP business, functional, and technical specifications.

## Applications

- `apps/web` — Next.js workspace, client portal, intake, and Otic console
- `apps/lip-api` — .NET 9 API, SQLite locally and Azure SQL in production
- `infrastructure/lip-web` — Bicep for a resource group that does not share CIP resources

## Demo identities

Open `/login` and continue as Ada (advocate), Ben (ethical wall), Chris (client portal), or Owen (Otic administration).

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test apps/lip-api/tests/Lip.Api.Tests.csproj
dotnet run --project apps/lip-api
cd apps/web && npm install && npm run dev
```

The seeded Employment Act passage is demonstration material. It is not a licensed legal corpus.

Deployed demonstration: https://app-lip-web.azurewebsites.net
