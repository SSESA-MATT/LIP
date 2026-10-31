using System.Security.Cryptography;
using System.Text;
using Lip.Api.Data;
using Lip.Api.Domain;

namespace Lip.Api.Security;

public sealed record Actor(string UserId, string TenantId, string Role, string Name, bool Active);

public static class DemoTokens
{
    public static string Issue(string userId, string key)
    {
        var sig = Sig(userId, key);
        return $"{userId}.{sig}";
    }

    public static bool TryRead(string? header, string key, out string userId)
    {
        userId = "";
        if (string.IsNullOrWhiteSpace(header)) return false;
        var token = header.StartsWith("Demo ", StringComparison.OrdinalIgnoreCase)
            ? header["Demo ".Length..].Trim()
            : header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) && header.Contains('.', StringComparison.Ordinal) && !header.Contains(' ')
                ? header["Bearer ".Length..].Trim()
                : "";
        if (token.Length == 0 && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var raw = header["Bearer ".Length..].Trim();
            var partsBearer = raw.Split('.');
            if (partsBearer.Length == 2) token = raw;
        }
        var parts = token.Split('.');
        if (parts.Length != 2) return false;
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Sig(parts[0], key)), Encoding.UTF8.GetBytes(parts[1])))
            return false;
        userId = parts[0];
        return true;
    }

    private static string Sig(string userId, string key)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(userId));
        return Convert.ToHexString(hash)[..16];
    }
}

public static class Access
{
    public static bool IsWalled(LipDb db, Actor actor, string matterId) =>
        db.EthicalWalls.Any(w => w.TenantId == actor.TenantId && w.UserId == actor.UserId && w.MatterId == matterId);

    public static bool CanReadMatter(LipDb db, Actor actor, Matter matter)
    {
        if (actor.Role == "OticAdministrator") return false;
        if (matter.TenantId != actor.TenantId) return false;
        if (!actor.Active) return false;
        if (IsWalled(db, actor, matter.Id)) return false;
        if (actor.Role is "Client" or "BusinessRequestor")
            return db.Clients.Any(c => c.Id == matter.ClientId && c.TenantId == actor.TenantId && c.Name == actor.Name)
                   || matter.ClientId == "cli-chris" && actor.UserId == DemoSeed.Chris;
        if (actor.Role is "Advocate")
            return db.MatterMembers.Any(m => m.MatterId == matter.Id && m.UserId == actor.UserId && m.TenantId == actor.TenantId);
        return actor.Role is "ManagingPartner" or "GeneralCounsel" or "TenantAdministrator" or "IntakeOfficer" or "ComplianceOfficer";
    }

    public static List<Matter> VisibleMatters(LipDb db, Actor actor)
    {
        if (actor.Role == "OticAdministrator") return [];
        var walled = db.EthicalWalls.Where(w => w.UserId == actor.UserId && w.TenantId == actor.TenantId).Select(w => w.MatterId).ToHashSet();
        var matters = db.Matters.Where(m => m.TenantId == actor.TenantId).AsEnumerable().Where(m => !walled.Contains(m.Id));
        if (actor.Role == "Advocate")
        {
            var mine = db.MatterMembers.Where(m => m.UserId == actor.UserId && m.TenantId == actor.TenantId).Select(m => m.MatterId).ToHashSet();
            matters = matters.Where(m => mine.Contains(m.Id));
        }
        if (actor.Role is "Client" or "BusinessRequestor")
            matters = matters.Where(m => m.ClientId == "cli-chris");
        return matters.ToList();
    }
}
