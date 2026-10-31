using Lip.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Lip.Api.Data;

public static class DemoSeed
{
    public const string Firm = "ten-firm";
    public const string Corp = "ten-corp";
    public const string Platform = "ten-otic";
    public const string Ada = "usr-ada";
    public const string Ben = "usr-ben";
    public const string Paul = "usr-paul";
    public const string Grace = "usr-grace";
    public const string Chris = "usr-chris";
    public const string Tina = "usr-tina";
    public const string Owen = "usr-owen";
    public const string WalledMatter = "mat-kabanda";
    public const string OpenMatter = "mat-employment";

    public static void Apply(LipDb db)
    {
        var blank = db.CommandRuns.Where(r => r.ConversationId == "").ToList();
        foreach (var run in blank) run.ConversationId = run.Id;
        if (blank.Count > 0) db.SaveChanges();
        if (!db.Tenants.Any()) SeedBase(db);
        Upgrade(db);
        EnsureConversationMessages(db);
        EnsureClientTimeline(db);
        EnsureEmploymentHearing(db);
    }

    private static void EnsureEmploymentHearing(LipDb db)
    {
        if (db.LitigationEvents.Any(l => l.Id == "lit-emp")) return;
        var now = DateTimeOffset.Parse("2026-10-01T09:00:00Z");
        db.LitigationEvents.Add(new LitigationEvent
        {
            Id = "lit-emp", TenantId = Firm, MatterId = OpenMatter, Forum = "Industrial Court (demonstration)",
            CaseReference = "Lab. 8/2026 (demonstration)", Kind = "hearing", When = now.AddDays(12),
            VerifiedByCounsel = false, SourceNote = "Extracted date. Counsel has not verified it."
        });
        db.SaveChanges();
    }

    private static void EnsureClientTimeline(LipDb db)
    {
        var now = DateTimeOffset.Parse("2026-10-01T09:00:00Z");
        var added = false;
        if (!db.AuditEvents.Any(a => a.Id == "aud-chris"))
        {
            db.AuditEvents.Add(new AuditEvent
            {
                Id = "aud-chris", TenantId = Firm, ActorId = Ada, Action = "client.profile", Resource = "cli-chris",
                Outcome = "Consent granted. Channels whatsapp, web.", CorrelationId = "aud-chris", At = now.AddDays(1)
            });
            added = true;
        }
        if (!db.AuditEvents.Any(a => a.Id == "aud-kabanda"))
        {
            db.AuditEvents.Add(new AuditEvent
            {
                Id = "aud-kabanda", TenantId = Firm, ActorId = Paul, Action = "matter.opened", Resource = WalledMatter,
                Outcome = "Kabanda commercial dispute opened.", CorrelationId = "aud-kabanda", At = now.AddDays(1)
            });
            added = true;
        }
        if (added) db.SaveChanges();
    }

    private static void EnsureConversationMessages(LipDb db)
    {
        var added = false;
        foreach (var conversation in db.Conversations.ToList())
        {
            if (db.Messages.Any(m => m.ConversationId == conversation.Id)) continue;
            var summary = db.Enquiries.Where(e => e.Id == conversation.EnquiryId).Select(e => e.Summary).FirstOrDefault();
            db.Messages.Add(new Message
            {
                Id = "msg-seed-" + conversation.Id,
                TenantId = conversation.TenantId,
                ConversationId = conversation.Id,
                Author = "client",
                Body = string.IsNullOrWhiteSpace(summary) ? "Enquiry received." : summary,
                Kind = "human",
                At = conversation.SlaStartedAt
            });
            added = true;
        }
        if (added) db.SaveChanges();
    }

    public static void EnsureCommandThreadColumn(LipDb db)
    {
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened) connection.Open();
        try
        {
            var sqlite = db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
            using var check = connection.CreateCommand();
            check.CommandText = sqlite
                ? "SELECT COUNT(*) FROM pragma_table_info('CommandRuns') WHERE name = 'ConversationId'"
                : "SELECT COL_LENGTH('CommandRuns', 'ConversationId')";
            var found = check.ExecuteScalar();
            var missing = sqlite ? Convert.ToInt32(found) == 0 : found is null or DBNull;
            if (!missing) return;
            using var alter = connection.CreateCommand();
            alter.CommandText = sqlite
                ? "ALTER TABLE CommandRuns ADD COLUMN ConversationId TEXT NOT NULL DEFAULT ''"
                : "ALTER TABLE CommandRuns ADD ConversationId nvarchar(450) NOT NULL CONSTRAINT DF_CommandRuns_ConversationId DEFAULT ''";
            alter.ExecuteNonQuery();
        }
        finally
        {
            if (opened) connection.Close();
        }
    }

    private static void SeedBase(LipDb db)
    {
        var now = DateTimeOffset.Parse("2026-10-01T09:00:00Z");

        db.Tenants.AddRange(
            new Tenant { Id = Firm, Name = "Kampala Advocates Demo", Mode = "LawFirm", Plan = "Practice Professional", Branding = "Kampala Advocates" },
            new Tenant { Id = Corp, Name = "Nile Manufacturing Legal", Mode = "Corporate", Plan = "Legal Intelligence Professional", Branding = "Nile Manufacturing" },
            new Tenant { Id = Platform, Name = "Otic platform", Mode = "Platform", Plan = "operator", Branding = "Otic" });

        db.Users.AddRange(
            new AppUser { Id = Ada, TenantId = Firm, Name = "Ada Namukasa", Email = "ada@kampala-advocates.demo", Role = "Advocate" },
            new AppUser { Id = Ben, TenantId = Firm, Name = "Ben Okello", Email = "ben@kampala-advocates.demo", Role = "Advocate" },
            new AppUser { Id = Paul, TenantId = Firm, Name = "Paul Ssemakula", Email = "paul@kampala-advocates.demo", Role = "ManagingPartner" },
            new AppUser { Id = Tina, TenantId = Firm, Name = "Tina Sekitoleko", Email = "tina@kampala-advocates.demo", Role = "TenantAdministrator" },
            new AppUser { Id = Grace, TenantId = Corp, Name = "Grace Atim", Email = "grace@nile.demo", Role = "GeneralCounsel" },
            new AppUser { Id = Chris, TenantId = Firm, Name = "Chris Mugisha", Email = "chris@client.demo", Role = "Client" },
            new AppUser { Id = Owen, TenantId = Platform, Name = "Owen Operator", Email = "owen@otic.demo", Role = "OticAdministrator" });

        db.Clients.AddRange(
            new ClientRecord { Id = "cli-chris", TenantId = Firm, Name = "Chris Mugisha", VerifiedChannels = "whatsapp,web", ConsentStatus = "granted" },
            new ClientRecord { Id = "cli-nile", TenantId = Corp, Name = "Nile Manufacturing Ltd", Kind = "organisation", VerifiedChannels = "web", ConsentStatus = "granted" });

        db.Enquiries.AddRange(
            new Enquiry
            {
                Id = "enq-wa-1", TenantId = Firm, ClientId = "cli-chris", SourceChannel = "whatsapp", LatestChannel = "web",
                Category = "Employment", Jurisdiction = "Uganda", Summary = "Review of a fixed-term employment contract.",
                Stage = "converted", OwnerId = Ada, ConsentVersion = "lip-privacy-1", ConsentStatus = "granted", ReceivedAt = now
            },
            new Enquiry
            {
                Id = "enq-web-2", TenantId = Firm, ClientId = "cli-chris", SourceChannel = "web", LatestChannel = "web",
                Category = "Dispute", Summary = "Status request on a walled commercial dispute.",
                Stage = "qualified", OwnerId = Paul, ConsentVersion = "lip-privacy-1", ConsentStatus = "granted", ReceivedAt = now.AddHours(2)
            });

        db.Conversations.AddRange(
            new Conversation { Id = "con-1", TenantId = Firm, EnquiryId = "enq-wa-1", Channel = "whatsapp", Queue = "assigned", OwnerId = Ada, SlaStartedAt = now },
            new Conversation { Id = "con-2", TenantId = Firm, EnquiryId = "enq-web-2", Channel = "web", Queue = "escalated", OwnerId = Paul, SlaStartedAt = now.AddHours(2) });
        db.Messages.Add(new Message { Id = "msg-1", TenantId = Firm, ConversationId = "con-1", Author = "client", Body = "Please review the attached employment contract.", Kind = "human", At = now });

        db.Matters.AddRange(
            new Matter
            {
                Id = OpenMatter, TenantId = Firm, EnquiryId = "enq-wa-1", ClientId = "cli-chris", Title = "Mugisha employment contract review",
                OwnerId = Ada, PracticeArea = "Employment", Status = "active", ClearanceState = "cleared", OpenedAt = now.AddDays(1),
                RecordedExposure = 0, ExposureNote = "Advisory matter. No claim value recorded."
            },
            new Matter
            {
                Id = WalledMatter, TenantId = Firm, EnquiryId = "enq-web-2", ClientId = "cli-chris", Title = "Kabanda commercial dispute",
                OwnerId = Paul, PracticeArea = "Litigation", Status = "active", ClearanceState = "cleared", Confidentiality = "privileged",
                OpenedAt = now.AddDays(1), RecordedExposure = 180000000, ExposureNote = "Counsel-recorded exposure, 1 Oct 2026, instruction letter.", Risk = "high"
            });

        db.MatterMembers.AddRange(
            new MatterMember { Id = "mm-1", TenantId = Firm, MatterId = OpenMatter, UserId = Ada },
            new MatterMember { Id = "mm-2", TenantId = Firm, MatterId = WalledMatter, UserId = Paul });
        db.EthicalWalls.Add(new EthicalWall { Id = "wall-1", TenantId = Firm, MatterId = WalledMatter, UserId = Ben, Reason = "Prior instruction on the opposing party." });

        db.ConflictChecks.Add(new ConflictCheck
        {
            Id = "cof-1", TenantId = Firm, EnquiryId = "enq-wa-1", MatterId = OpenMatter, Counterparty = "Nile Manufacturing Ltd",
            Disposition = "cleared", Notes = "No match on permitted parties.", At = now
        });

        db.Tasks.Add(new WorkTask
        {
            Id = "task-1", TenantId = Firm, MatterId = OpenMatter, AssigneeId = Ada, Title = "Confirm notice-period deviation with client",
            DueAt = now.AddDays(5), DateVerified = false, Status = "open"
        });

        db.Documents.Add(new MatterDocument
        {
            Id = "doc-1", TenantId = Firm, MatterId = OpenMatter, Title = "Employment contract draft",
            Classification = "privileged", ApprovalState = "pending", UploaderId = Ada, OriginalHash = "sha256-demo-contract",
            CurrentVersionId = "ver-1", MalwareClear = true
        });
        db.DocumentVersions.Add(new DocumentVersion
        {
            Id = "ver-1", TenantId = Firm, DocumentId = "doc-1", Number = 1, Hash = "sha256-demo-contract",
            UploaderId = Ada, ArtifactKind = "original", At = now
        });

        db.Sources.Add(new LegalSource
        {
            Id = "src-ug-emp", Jurisdiction = "Uganda", Title = "Employment Act (demonstration extract)",
            Citation = "Employment Act, Cap. 226 (demonstration passage)",
            Passage = "Demonstration passage only. A fixed-term contract should state its duration and the notice required to end it. This text is not a licensed corpus.",
            EffectiveFrom = DateTimeOffset.Parse("2006-06-08T00:00:00Z"),
            LicenceStatus = "demonstration", VerificationStatus = "demonstration-not-a-licensed-corpus"
        });

        db.ResearchNotes.Add(new ResearchNote
        {
            Id = "note-1", TenantId = Firm, MatterId = OpenMatter, AuthorId = Ada,
            Question = "What should a Ugandan fixed-term employment contract state about duration and notice?",
            Body = "The demonstration extract of the Employment Act states that a fixed-term contract should state its duration and the notice required to end it.",
            ReviewState = "pending", At = now.AddDays(1)
        });

        db.Contracts.Add(new ContractReview
        {
            Id = "conr-1", TenantId = Firm, MatterId = OpenMatter, Title = "Mugisha fixed-term contract",
            Parties = "Chris Mugisha; Nile Manufacturing Ltd", GoverningLaw = "Uganda",
            ExtractedDates = "Start 1 Nov 2026; expiry 31 Oct 2027 (unvalidated)",
            DeviationRule = "Playbook EMP-04: notice period must be at least 30 days",
            DeviationLocation = "Clause 8.2", Severity = "high", Disposition = ""
        });

        db.LitigationEvents.Add(new LitigationEvent
        {
            Id = "lit-1", TenantId = Firm, MatterId = WalledMatter, Forum = "High Court of Uganda, Commercial Division",
            CaseReference = "HCCS 14/2026 (demonstration)", Kind = "hearing",
            When = now.AddDays(21), VerifiedByCounsel = true, SourceNote = "Entered by counsel. No outcome probability is stored."
        });

        db.UsageEvents.Add(new UsageEvent
        {
            Id = "use-1", TenantId = Firm, UserId = Ada, Agent = "research", Channel = "web",
            Units = 1200, CostBasis = 0.04m, DedupKey = "seed-use-1", At = now.AddDays(1)
        });

        db.Obligations.Add(new Obligation
        {
            Id = "obl-1", TenantId = Corp, Title = "Annual data-protection filing (demonstration)",
            SourceId = "src-ug-emp", OwnerId = Grace, Status = "open", ReviewDue = now.AddDays(30)
        });

        db.Connectors.AddRange(
            new ConnectorState { Id = "cx-wa", TenantId = Firm, Name = "WhatsApp Business", Release = "MVP", Status = "demo-adapter", Detail = "Signed webhook contract. Live provider is not configured." },
            new ConnectorState { Id = "cx-m365", TenantId = Firm, Name = "Microsoft 365", Release = "Phase 2", Status = "demo-adapter", Detail = "Permission boundary preserved. No CIP tenant is connected." },
            new ConnectorState { Id = "cx-teams", TenantId = Firm, Name = "Microsoft Teams", Release = "Phase 2", Status = "demo-adapter", Detail = "Alerts respect LIP permissions." },
            new ConnectorState { Id = "cx-court", TenantId = Firm, Name = "Court e-filing", Release = "Phase 3", Status = "unavailable", Detail = "No official API has been approved." },
            new ConnectorState { Id = "cx-finance", TenantId = Firm, Name = "Finance", Release = "Phase 2", Status = "demo-adapter", Detail = "Quoted, invoiced, and collected remain separate." });

        db.AuditEvents.Add(new AuditEvent
        {
            Id = "aud-seed", TenantId = Firm, ActorId = "system", Action = "seed", Resource = "tenant",
            Outcome = "ok", CorrelationId = "seed", At = now
        });

        db.SaveChanges();
    }

    private static void Upgrade(LipDb db)
    {
        if (db.AuditEvents.Any(a => a.Id == "seed-v2")) return;
        var now = DateTimeOffset.Parse("2026-10-01T09:00:00Z");
        var tina = db.Users.FirstOrDefault(u => u.Id == Tina);
        if (tina is not null) tina.Name = "Tina Sekitoleko";

        void AddClient(ClientRecord row) { if (!db.Clients.Any(c => c.Id == row.Id)) db.Clients.Add(row); }
        AddClient(new ClientRecord { Id = "cli-kabanda", TenantId = Firm, Name = "A. Kabanda", Kind = "person", VerifiedChannels = "web", ConsentStatus = "granted" });
        AddClient(new ClientRecord { Id = "cli-acme", TenantId = Firm, Name = "Acme Supplies Ltd", Kind = "organisation", VerifiedChannels = "web", ConsentStatus = "granted" });

        var kabanda = db.Matters.FirstOrDefault(m => m.Id == WalledMatter);
        if (kabanda is not null) kabanda.ClientId = "cli-kabanda";

        if (!db.Messages.Any(m => m.Id == "msg-2"))
            db.Messages.Add(new Message { Id = "msg-2", TenantId = Firm, ConversationId = "con-2", Author = "client", Body = "Please share the status of the commercial dispute.", Kind = "human", At = now.AddHours(2) });

        if (!db.Enquiries.Any(e => e.Id == "enq-web-3"))
            db.Enquiries.Add(new Enquiry
            {
                Id = "enq-web-3", TenantId = Firm, ClientId = "cli-acme", SourceChannel = "web", LatestChannel = "web",
                Category = "Commercial", Summary = "Lease review for a warehouse in Namanve.", Stage = "qualified",
                OwnerId = Ada, ConsentVersion = "lip-privacy-1", ConsentStatus = "granted", ReceivedAt = now.AddDays(2)
            });
        if (!db.Conversations.Any(c => c.Id == "con-3"))
            db.Conversations.Add(new Conversation { Id = "con-3", TenantId = Firm, EnquiryId = "enq-web-3", Channel = "web", Queue = "assigned", OwnerId = Ada, SlaStartedAt = now.AddDays(2) });
        if (!db.Messages.Any(m => m.Id == "msg-3"))
            db.Messages.Add(new Message { Id = "msg-3", TenantId = Firm, ConversationId = "con-3", Author = "client", Body = "We need the warehouse lease reviewed before 1 November.", Kind = "human", At = now.AddDays(2) });

        if (!db.Matters.Any(m => m.Id == "mat-lease"))
            db.Matters.Add(new Matter
            {
                Id = "mat-lease", TenantId = Firm, EnquiryId = "enq-web-3", ClientId = "cli-acme", Title = "Acme warehouse lease",
                OwnerId = Ada, PracticeArea = "Commercial", Status = "active", ClearanceState = "cleared", Risk = "high",
                OpenedAt = now.AddDays(3), RecordedExposure = 0, ExposureNote = "Advisory lease. No claim value recorded."
            });
        if (!db.MatterMembers.Any(m => m.Id == "mm-3"))
            db.MatterMembers.Add(new MatterMember { Id = "mm-3", TenantId = Firm, MatterId = "mat-lease", UserId = Ada });

        if (!db.Enquiries.Any(e => e.Id == "enq-conflict"))
            db.Enquiries.Add(new Enquiry
            {
                Id = "enq-conflict", TenantId = Firm, ClientId = "cli-acme", SourceChannel = "web", LatestChannel = "web",
                Category = "Commercial", Summary = "Instruction that names a known counterparty.", Stage = "qualified",
                OwnerId = Ada, ConsentVersion = "lip-privacy-1", ConsentStatus = "granted", ReceivedAt = now.AddDays(4)
            });
        if (!db.Matters.Any(m => m.Id == "mat-conflict"))
            db.Matters.Add(new Matter
            {
                Id = "mat-conflict", TenantId = Firm, EnquiryId = "enq-conflict", ClientId = "cli-acme", Title = "Acme supply dispute",
                OwnerId = Ada, PracticeArea = "Commercial", Status = "conflict_review", ClearanceState = "pending", Risk = "medium",
                OpenedAt = now.AddDays(4), ExposureNote = "Held in conflict review until a different person clears it."
            });
        if (!db.MatterMembers.Any(m => m.Id == "mm-4"))
            db.MatterMembers.Add(new MatterMember { Id = "mm-4", TenantId = Firm, MatterId = "mat-conflict", UserId = Ada });
        if (!db.ConflictChecks.Any(c => c.Id == "cof-2"))
            db.ConflictChecks.Add(new ConflictCheck
            {
                Id = "cof-2", TenantId = Firm, EnquiryId = "enq-conflict", MatterId = "mat-conflict", Counterparty = "Kabanda",
                Disposition = "potential", Notes = "Potential match. Human clearance is required.", At = now.AddDays(4)
            });

        if (!db.LitigationEvents.Any(l => l.Id == "lit-emp"))
            db.LitigationEvents.Add(new LitigationEvent
            {
                Id = "lit-emp", TenantId = Firm, MatterId = OpenMatter, Forum = "Industrial Court (demonstration)",
                CaseReference = "Lab. 8/2026 (demonstration)", Kind = "hearing", When = now.AddDays(12),
                VerifiedByCounsel = false, SourceNote = "Extracted date. Counsel has not verified it."
            });
        if (!db.Tasks.Any(t => t.Id == "task-2"))
            db.Tasks.Add(new WorkTask
            {
                Id = "task-2", TenantId = Firm, MatterId = "mat-lease", AssigneeId = Ada, Title = "Confirm rent commencement date",
                DueAt = now.AddDays(8), DateVerified = false, Status = "open"
            });
        if (!db.Approvals.Any(a => a.Id == "apr-seed"))
            db.Approvals.Add(new Approval
            {
                Id = "apr-seed", TenantId = Firm, SubjectType = "document", SubjectId = "doc-1",
                PayloadHash = "sha256-demo-contract", RequestorId = Ada, Decision = "pending", At = now.AddDays(2)
            });
        if (!db.CommandRuns.Any(r => r.Id == "run-seed"))
            db.CommandRuns.Add(new CommandRun
            {
                Id = "run-seed", ConversationId = "run-seed", TenantId = Firm, ActorId = Ada, MatterId = OpenMatter, Mode = "ask",
                Query = "What notice should a fixed-term employment contract state?",
                State = "COMPLETED", ReviewState = "informational", Tokens = 400, At = now.AddDays(2),
                ResultJson = "{\"label\":\"answer\",\"body\":\"Demonstration passage only. A fixed-term contract should state its duration and the notice required to end it. This text is not a licensed corpus.\",\"citation\":\"Employment Act, Cap. 226 (demonstration passage)\",\"autoPublished\":false}"
            });

        if (!db.Enquiries.Any(e => e.Id == "enq-nile"))
            db.Enquiries.Add(new Enquiry
            {
                Id = "enq-nile", TenantId = Corp, ClientId = "cli-nile", SourceChannel = "web", LatestChannel = "web",
                Category = "Compliance", Summary = "Internal request to track the annual data-protection filing.",
                Stage = "qualified", OwnerId = Grace, ConsentVersion = "lip-privacy-1", ConsentStatus = "granted", ReceivedAt = now.AddDays(1)
            });
        if (!db.Conversations.Any(c => c.Id == "con-nile"))
            db.Conversations.Add(new Conversation { Id = "con-nile", TenantId = Corp, EnquiryId = "enq-nile", Channel = "web", Queue = "assigned", OwnerId = Grace, SlaStartedAt = now.AddDays(1) });
        if (!db.Messages.Any(m => m.Id == "msg-nile"))
            db.Messages.Add(new Message { Id = "msg-nile", TenantId = Corp, ConversationId = "con-nile", Author = "business", Body = "Please keep the filing on the compliance register.", Kind = "human", At = now.AddDays(1) });
        if (!db.Matters.Any(m => m.Id == "mat-nile"))
            db.Matters.Add(new Matter
            {
                Id = "mat-nile", TenantId = Corp, EnquiryId = "enq-nile", ClientId = "cli-nile", Title = "Data-protection filing 2026",
                OwnerId = Grace, PracticeArea = "Compliance", Status = "active", ClearanceState = "cleared", Risk = "medium",
                OpenedAt = now.AddDays(2), ExposureNote = "Internal compliance matter. No claim value recorded."
            });
        if (!db.MatterMembers.Any(m => m.Id == "mm-nile"))
            db.MatterMembers.Add(new MatterMember { Id = "mm-nile", TenantId = Corp, MatterId = "mat-nile", UserId = Grace });
        if (!db.Contracts.Any(c => c.Id == "conr-nile"))
            db.Contracts.Add(new ContractReview
            {
                Id = "conr-nile", TenantId = Corp, MatterId = "mat-nile", Title = "Processor terms",
                Parties = "Nile Manufacturing Ltd; Kampala Hosting", GoverningLaw = "Uganda",
                ExtractedDates = "Renewal 1 Jan 2027 (unvalidated)", DeviationRule = "Playbook DP-02: processor must name a contact",
                DeviationLocation = "Clause 4", Severity = "medium", Disposition = ""
            });
        if (!db.Connectors.Any(c => c.Id == "cx-wa-corp"))
            db.Connectors.AddRange(
                new ConnectorState { Id = "cx-wa-corp", TenantId = Corp, Name = "WhatsApp Business", Release = "MVP", Status = "demo-adapter", Detail = "Corporate intake adapter. Live provider is not configured." },
                new ConnectorState { Id = "cx-m365-corp", TenantId = Corp, Name = "Microsoft 365", Release = "Phase 2", Status = "demo-adapter", Detail = "Permission boundary preserved." });

        if (!db.UsageEvents.Any(u => u.Id == "use-cash"))
            db.UsageEvents.Add(new UsageEvent { Id = "use-cash", TenantId = Firm, UserId = Tina, Agent = "cash-receipt", Channel = "finance", Units = 0, CostBasis = 2500000m, DedupKey = "seed-cash", At = now.AddDays(6) });
        if (!db.UsageEvents.Any(u => u.Id == "use-invoice"))
            db.UsageEvents.Add(new UsageEvent { Id = "use-invoice", TenantId = Firm, UserId = Tina, Agent = "invoice", Channel = "finance", Units = 0, CostBasis = 4100000m, DedupKey = "seed-invoice", At = now.AddDays(3) });

        db.AuditEvents.Add(new AuditEvent { Id = "seed-v2", TenantId = Firm, ActorId = "system", Action = "seed-version", Resource = "demo", Outcome = "2", CorrelationId = "seed-v2", At = now });
        db.SaveChanges();
    }
}
