# Global architecture

LIP is a modular monolith, which the technical specification allows until measured scale requires separate services.

```text
Browser
  -> app-lip-web (Next.js, CIP visual system, LIP routes)
  -> app-lip-web-api (.NET 9)
       -> Azure SQL db-lip-web   tenant_id on every institution row
       -> Key Vault              signing and connection material
       -> Storage                reserved for matter files, public access off
       -> Application Insights
```

Trust boundaries:

- The browser holds a demo token or, later, an Entra token for the LIP app registration.
- The API resolves the actor, then filters by tenant, role, matter membership, and ethical wall.
- Otic administration is a separate page and a separate role. Matter reads return no record.
- WhatsApp webhooks deduplicate on provider event id. A duplicate does not create a second enquiry.
- Command Centre modes are ask, prepare, and act. Act does not publish.

Scale path: raise the App Service instance count, move SQL to a higher SKU, and extract the channel adapter or the model gateway when those become bottlenecks. Institution data stays in this resource group.
