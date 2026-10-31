# LIP agent context

Working tree: `/home/consultantfil/projects/LIP`  
GitHub: https://github.com/Otic-Group/LIP  
Do not modify `/home/consultantfil/projects/CIP_Web` for LIP work, and do not point LIP settings at `rg-cip-web-prod`.

Look and cross-cutting chrome come from CIP. Flows come from the LIP BRD, FSD, and TSD. See `docs/architecture/LOOK_VERSUS_FLOW.md`.

Local API: `dotnet run --project apps/lip-api` then `npm run dev` in `apps/web`.  
Demonstration identities are on `/login`.

Production names and the isolation rule are in `docs/architecture/AZURE_ISOLATION.md`.

Live demonstration: https://app-lip-web.azurewebsites.net  
API: https://app-lip-web-api.azurewebsites.net
