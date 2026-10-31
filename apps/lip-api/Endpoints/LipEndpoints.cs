using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lip.Api.Data;
using Lip.Api.Domain;
using Lip.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace Lip.Api.Endpoints;

public static class LipEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok", product = "LIP" }));

        var api = app.MapGroup("/api/v1");

        api.MapPost("/auth/demo", (DemoLogin body, LipDb db, IConfiguration cfg) =>
        {
            var user = db.Users.FirstOrDefault(u => u.Id == body.UserId && u.Active);
            if (user is null) return Results.Json(new { code = "AUTH_REQUIRED" }, statusCode: 401);
            var token = DemoTokens.Issue(user.Id, cfg["DemoAuth:Key"] ?? "lip-demo-signing-key");
            return Results.Ok(new { token, user = PublicUser(user) });
        });

        api.MapGet("/auth/me", (HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Current(req, db, cfg);
            if (actor is null) return Results.Json(new { code = "AUTH_REQUIRED" }, statusCode: 401);
            var user = db.Users.First(u => u.Id == actor.UserId);
            var tenant = db.Tenants.First(t => t.Id == actor.TenantId);
            return Results.Ok(new { user = PublicUser(user), tenant = new { tenant.Id, tenant.Name, tenant.Mode, tenant.Plan, tenant.Branding } });
        });

        api.MapPost("/enquiries", (EnquiryInput body, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            if (body.ConsentStatus == "refused")
                return Results.Json(new { code = "VALIDATION_FAILED", message = "Consent is required before an enquiry is created." }, statusCode: 400);
            var id = NewId("enq");
            var enquiry = new Enquiry
            {
                Id = id, TenantId = actor.TenantId, ClientId = body.ClientId ?? "", SourceChannel = body.Channel,
                LatestChannel = body.Channel, Category = body.Category, Jurisdiction = body.Jurisdiction ?? "Uganda",
                Summary = body.Summary, Urgency = body.Urgency ?? "normal", Stage = "new", OwnerId = "",
                ConsentVersion = body.ConsentVersion ?? "lip-privacy-1", ConsentStatus = "granted", ReceivedAt = DateTimeOffset.UtcNow
            };
            db.Enquiries.Add(enquiry);
            var conversation = new Conversation
            {
                Id = NewId("con"), TenantId = actor.TenantId, EnquiryId = id, Channel = body.Channel,
                Queue = "unassigned", SlaStartedAt = enquiry.ReceivedAt
            };
            db.Conversations.Add(conversation);
            db.Messages.Add(new Message
            {
                Id = NewId("msg"), TenantId = actor.TenantId, ConversationId = conversation.Id, Author = "requester",
                Body = body.Summary, Kind = "human", At = enquiry.ReceivedAt
            });
            Audit(db, actor, "enquiry.create", id, "ok");
            db.SaveChanges();
            return Results.Ok(new { enquiry.Id, enquiry.SourceChannel, enquiry.Stage, conversationId = conversation.Id });
        });

        api.MapGet("/enquiries", (HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var rows = db.Enquiries.Where(e => e.TenantId == actor.TenantId && !e.TestOrSpam).ToList()
                .Where(e => CanSeeEnquiry(db, actor, e)).OrderByDescending(e => e.ReceivedAt).ToList();
            return Results.Ok(rows);
        });

        api.MapGet("/inbox", (HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var visibleEnquiryIds = db.Enquiries.Where(e => e.TenantId == actor.TenantId).ToList().Where(e => CanSeeEnquiry(db, actor, e)).Select(e => e.Id).ToHashSet();
            var items = db.Conversations.Where(c => c.TenantId == actor.TenantId).ToList()
                .Where(c => visibleEnquiryIds.Contains(c.EnquiryId))
                .OrderByDescending(c => c.SlaStartedAt)
                .Select(c => new
                {
                    c.Id, c.EnquiryId, c.Channel, c.Queue, c.OwnerId, c.Status, c.SlaStartedAt,
                    enquiry = db.Enquiries.Where(e => e.Id == c.EnquiryId).Select(e => new { e.Summary, e.SourceChannel, e.Stage, e.Category }).FirstOrDefault(),
                    messages = db.Messages.Where(m => m.ConversationId == c.Id && m.TenantId == actor.TenantId).ToList().OrderBy(m => m.At).ToList()
                });
            return Results.Ok(items);
        });

        api.MapPost("/inbox/{id}/messages", (string id, InboxMessageInput body, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var conversation = VisibleConversation(db, actor, id);
            if (conversation is null) return Missing();
            if (string.IsNullOrWhiteSpace(body.Body))
                return Results.Json(new { code = "VALIDATION_FAILED", message = "A reply needs text." }, statusCode: 400);
            var message = new Message
            {
                Id = NewId("msg"), TenantId = actor.TenantId, ConversationId = id, Author = actor.Name,
                Body = body.Body.Trim(), Kind = "human", At = DateTimeOffset.UtcNow
            };
            db.Messages.Add(message);
            Audit(db, actor, "inbox.reply", id, "ok");
            db.SaveChanges();
            return Results.Ok(message);
        });

        api.MapPost("/inbox/{id}/handover", (string id, HandoverInput body, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var conversation = VisibleConversation(db, actor, id);
            if (conversation is null) return Missing();
            var owner = db.Users.FirstOrDefault(u => u.Id == body.OwnerId && u.TenantId == actor.TenantId && u.Active && u.Role != "Client");
            if (owner is null)
                return Results.Json(new { code = "VALIDATION_FAILED", message = "Choose an active colleague in this tenant." }, statusCode: 400);
            conversation.OwnerId = owner.Id;
            conversation.Queue = "assigned";
            conversation.Status = "escalated";
            var reason = string.IsNullOrWhiteSpace(body.Reason)
                ? "Handed to counsel. Automated advice is paused."
                : body.Reason.Trim();
            if (!reason.Contains("Automated advice is paused", StringComparison.OrdinalIgnoreCase))
                reason = $"{reason.TrimEnd('.', ' ')}. Automated advice is paused.";
            db.Messages.Add(new Message
            {
                Id = NewId("msg"), TenantId = actor.TenantId, ConversationId = id, Author = actor.Name,
                Body = reason, Kind = "handover", At = DateTimeOffset.UtcNow
            });
            Audit(db, actor, "handover", id, "ok");
            db.SaveChanges();
            return Results.Ok(conversation);
        });

        api.MapGet("/clients", (HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            return Results.Ok(db.Clients.Where(c => c.TenantId == actor.TenantId).ToList());
        });

        api.MapGet("/clients/{id}", (string id, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var client = db.Clients.FirstOrDefault(c => c.Id == id && c.TenantId == actor.TenantId);
            if (client is null) return Missing();
            var matters = Access.VisibleMatters(db, actor).Where(m => m.ClientId == id).Select(m => new { m.Id, m.Title, m.Status, m.Risk }).ToList();
            var enquiries = db.Enquiries.Where(e => e.TenantId == actor.TenantId && e.ClientId == id).ToList().Where(e => CanSeeEnquiry(db, actor, e)).Select(e => new { e.Id, e.SourceChannel, e.Stage, e.Summary }).ToList();
            var matterIds = matters.Select(m => m.Id).ToHashSet();
            var enquiryIds = enquiries.Select(e => e.Id).ToHashSet();
            var timeline = db.AuditEvents.Where(a => a.TenantId == actor.TenantId).ToList()
                .Where(a => a.Resource == client.Id || matterIds.Contains(a.Resource) || enquiryIds.Contains(a.Resource))
                .OrderByDescending(a => a.At)
                .Take(50)
                .Select(a => new { a.Id, a.Action, a.Resource, a.Outcome, a.At });
            return Results.Ok(new { client, matters, enquiries, consent = client.ConsentStatus, timeline });
        });

        api.MapPost("/conflicts/checks", (ConflictInput body, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var hit = db.Matters.Any(m => m.TenantId == actor.TenantId && m.Title.Contains(body.Counterparty))
                      || body.Counterparty.Contains("Kabanda", StringComparison.OrdinalIgnoreCase);
            var check = new ConflictCheck
            {
                Id = NewId("cof"), TenantId = actor.TenantId, EnquiryId = body.EnquiryId, Counterparty = body.Counterparty,
                Disposition = hit ? "potential" : "cleared",
                Notes = hit ? "Potential match. Human clearance is required. Restricted details are withheld from unauthorised screeners." : "No match on permitted parties.",
                At = DateTimeOffset.UtcNow
            };
            db.ConflictChecks.Add(check);
            Audit(db, actor, "conflict.check", check.Id, check.Disposition);
            db.SaveChanges();
            return Results.Ok(check);
        });

        api.MapPost("/matters", (MatterInput body, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var enquiry = db.Enquiries.FirstOrDefault(e => e.Id == body.EnquiryId && e.TenantId == actor.TenantId);
            if (enquiry is null || !CanSeeEnquiry(db, actor, enquiry)) return Missing();
            var ownerId = string.IsNullOrWhiteSpace(body.OwnerId) ? actor.UserId : body.OwnerId;
            var owner = db.Users.FirstOrDefault(u => u.Id == ownerId && u.TenantId == actor.TenantId && u.Active && u.Role != "Client");
            if (owner is null)
                return Results.Json(new { code = "VALIDATION_FAILED", message = "Choose an active colleague in this tenant." }, statusCode: 400);
            var openConflict = db.ConflictChecks.Any(c => c.EnquiryId == enquiry.Id && c.TenantId == actor.TenantId && c.Disposition == "potential");
            var matter = new Matter
            {
                Id = NewId("mat"), TenantId = actor.TenantId, EnquiryId = enquiry.Id, ClientId = enquiry.ClientId,
                Title = body.Title, OwnerId = owner.Id, PracticeArea = body.PracticeArea,
                Status = openConflict ? "conflict_review" : "proposed",
                ClearanceState = openConflict ? "pending" : "cleared",
                Jurisdiction = enquiry.Jurisdiction, OpenedAt = DateTimeOffset.UtcNow
            };
            db.Matters.Add(matter);
            db.MatterMembers.Add(new MatterMember { Id = NewId("mm"), TenantId = actor.TenantId, MatterId = matter.Id, UserId = owner.Id });
            if (owner.Id != actor.UserId)
                db.MatterMembers.Add(new MatterMember { Id = NewId("mm"), TenantId = actor.TenantId, MatterId = matter.Id, UserId = actor.UserId });
            Audit(db, actor, "matter.create", matter.Id, matter.Status);
            db.SaveChanges();
            return Results.Ok(matter);
        });

        api.MapGet("/matters", (HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            return Results.Ok(Access.VisibleMatters(db, actor).OrderByDescending(m => m.OpenedAt).ToList());
        });

        api.MapGet("/matters/{id}", (string id, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var matter = db.Matters.FirstOrDefault(m => m.Id == id);
            if (matter is null || !Access.CanReadMatter(db, actor, matter)) return Missing();
            var tasks = db.Tasks.Where(t => t.MatterId == id && t.TenantId == actor.TenantId).ToList();
            var docs = db.Documents.Where(d => d.MatterId == id && d.TenantId == actor.TenantId)
                .Select(d => new { d.Id, d.Title, d.ApprovalState, d.Classification, d.OriginalHash }).ToList();
            var hearings = db.LitigationEvents.Where(l => l.MatterId == id && l.TenantId == actor.TenantId).ToList();
            var docIds = docs.Select(d => d.Id).ToHashSet();
            var approvals = db.Approvals.Where(a => a.TenantId == actor.TenantId).AsEnumerable().Where(a => docIds.Contains(a.SubjectId)).ToList();
            var conflicts = db.ConflictChecks.Where(c => c.MatterId == id && c.TenantId == actor.TenantId).ToList();
            return Results.Ok(new { matter, tasks, documents = docs, hearings, approvals, conflicts, outcomeProbability = (decimal?)null });
        });

        api.MapPost("/matters/{id}/clearance", (string id, ClearanceInput body, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var matter = db.Matters.FirstOrDefault(m => m.Id == id && m.TenantId == actor.TenantId);
            if (matter is null || !Access.CanReadMatter(db, actor, matter)) return Missing();
            if (actor.Role is not ("ManagingPartner" or "GeneralCounsel" or "TenantAdministrator"))
                return Results.Json(new { code = "ACCESS_DENIED" }, statusCode: 403);
            if (actor.UserId == matter.OwnerId)
                return Results.Json(new { code = "ACCESS_DENIED", message = "The matter owner cannot clear their own conflict." }, statusCode: 403);
            if (body.Decision is not ("cleared" or "blocked"))
                return Results.Json(new { code = "VALIDATION_FAILED" }, statusCode: 400);
            matter.ClearanceState = body.Decision == "cleared" ? "cleared" : "blocked";
            if (body.Decision == "blocked") matter.Status = "conflict_review";
            var check = db.ConflictChecks.FirstOrDefault(c => c.MatterId == id && c.Disposition == "potential");
            if (check is not null) check.Disposition = body.Decision == "cleared" ? "cleared" : "potential";
            Audit(db, actor, "matter.clearance", id, matter.ClearanceState);
            db.SaveChanges();
            return Results.Ok(matter);
        });

        api.MapPost("/matters/{id}/activate", (string id, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var matter = db.Matters.FirstOrDefault(m => m.Id == id && m.TenantId == actor.TenantId);
            if (matter is null || !Access.CanReadMatter(db, actor, matter)) return Missing();
            if (matter.ClearanceState != "cleared")
                return Results.Json(new { code = "CONFLICT_PENDING", message = "Matter acceptance stays blocked until an authorised clearance." }, statusCode: 409);
            matter.Status = "active";
            matter.Version += 1;
            Audit(db, actor, "matter.activate", id, "ok");
            db.SaveChanges();
            return Results.Ok(matter);
        });

        api.MapPost("/documents", (DocumentInput body, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            if (actor.Role == "OticAdministrator") return Missing();
            var matter = db.Matters.FirstOrDefault(m => m.Id == body.MatterId);
            if (matter is null || !Access.CanReadMatter(db, actor, matter)) return Missing();
            if (!body.MalwareClear)
                return Results.Json(new { code = "VALIDATION_FAILED", message = "The file was quarantined before indexing." }, statusCode: 400);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body.Title + body.Body))).ToLowerInvariant();
            var doc = new MatterDocument
            {
                Id = NewId("doc"), TenantId = actor.TenantId, MatterId = matter.Id, Title = body.Title,
                Classification = "privileged", ApprovalState = "draft", UploaderId = actor.UserId,
                OriginalHash = hash, MalwareClear = true
            };
            var version = new DocumentVersion
            {
                Id = NewId("ver"), TenantId = actor.TenantId, DocumentId = doc.Id, Number = 1, Hash = hash,
                UploaderId = actor.UserId, ArtifactKind = "original", At = DateTimeOffset.UtcNow
            };
            doc.CurrentVersionId = version.Id;
            db.Documents.Add(doc);
            db.DocumentVersions.Add(version);
            Audit(db, actor, "document.upload", doc.Id, "ok");
            db.SaveChanges();
            return Results.Ok(new { doc.Id, doc.OriginalHash, doc.ApprovalState, linkExpires = true });
        });

        api.MapPost("/documents/{id}/issue", (string id, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var doc = db.Documents.FirstOrDefault(d => d.Id == id && d.TenantId == actor.TenantId);
            if (doc is null) return Missing();
            var matter = db.Matters.FirstOrDefault(m => m.Id == doc.MatterId);
            if (matter is null || !Access.CanReadMatter(db, actor, matter)) return Missing();
            var approved = db.Approvals.Any(a => a.SubjectId == id && a.TenantId == actor.TenantId && a.Decision == "approved" && a.ApproverId != a.RequestorId);
            if (!approved || doc.ApprovalState != "approved")
                return Results.Json(new { code = "APPROVAL_REQUIRED", message = "Only an approved version can be issued." }, statusCode: 409);
            doc.ApprovalState = "issued";
            Audit(db, actor, "document.issue", id, "ok");
            db.SaveChanges();
            return Results.Ok(doc);
        });

        api.MapGet("/research", (string? matterId, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            if (!string.IsNullOrEmpty(matterId))
            {
                var matter = db.Matters.FirstOrDefault(m => m.Id == matterId);
                if (matter is null || !Access.CanReadMatter(db, actor, matter)) return Missing();
            }
            var notes = db.ResearchNotes.Where(n => n.TenantId == actor.TenantId && n.AuthorId == actor.UserId).ToList()
                .Where(n => string.IsNullOrEmpty(matterId) ? string.IsNullOrEmpty(n.MatterId) : n.MatterId == matterId)
                .OrderByDescending(n => n.At)
                .Select(n => new { n.Id, n.Question, n.Body, n.ReviewState, n.Jurisdiction, n.MatterId, n.At });
            return Results.Ok(notes);
        });

        api.MapPost("/research", (ResearchInput body, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            if (!string.IsNullOrEmpty(body.MatterId))
            {
                var matter = db.Matters.FirstOrDefault(m => m.Id == body.MatterId);
                if (matter is null || !Access.CanReadMatter(db, actor, matter)) return Missing();
            }
            var source = db.Sources.FirstOrDefault(s => s.Jurisdiction == (body.Jurisdiction ?? "Uganda") && s.LicenceStatus == "demonstration"
                && (body.Question.Contains("employment", StringComparison.OrdinalIgnoreCase) || body.Question.Contains("notice", StringComparison.OrdinalIgnoreCase)));
            var note = new ResearchNote
            {
                Id = NewId("note"), TenantId = actor.TenantId, MatterId = body.MatterId ?? "", AuthorId = actor.UserId,
                Question = body.Question, Jurisdiction = body.Jurisdiction ?? "Uganda", At = DateTimeOffset.UtcNow,
                ReviewState = "pending", CorpusVersion = "ug-demo-1"
            };
            if (source is null)
            {
                note.Body = "No verified authority is available for this question. No citation has been created.";
                db.ResearchNotes.Add(note);
                Meter(db, actor, "research", note.Id);
                Audit(db, actor, "research", note.Id, "no-evidence");
                db.SaveChanges();
                return Results.Ok(new { note.Id, evidence = "none", citations = Array.Empty<object>(), reviewState = note.ReviewState, note.Body });
            }
            note.Body = source.Passage;
            db.ResearchNotes.Add(note);
            Meter(db, actor, "research", note.Id);
            Audit(db, actor, "research", note.Id, "cited");
            db.SaveChanges();
            return Results.Ok(new
            {
                note.Id,
                evidence = "cited",
                reviewState = note.ReviewState,
                note.Body,
                corpusVersion = note.CorpusVersion,
                citations = new[] { new { source.Id, source.Citation, source.Passage, source.VerificationStatus, source.EffectiveFrom, source.LicenceStatus } }
            });
        });

        api.MapGet("/contracts", (HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var visible = Access.VisibleMatters(db, actor).Select(m => m.Id).ToList();
            return Results.Ok(db.Contracts.Where(c => c.TenantId == actor.TenantId && visible.Contains(c.MatterId)).ToList());
        });

        api.MapPost("/contracts/{id}/dispositions", (string id, DispositionInput body, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var review = db.Contracts.FirstOrDefault(c => c.Id == id && c.TenantId == actor.TenantId);
            if (review is null) return Missing();
            var matter = db.Matters.First(m => m.Id == review.MatterId);
            if (!Access.CanReadMatter(db, actor, matter)) return Missing();
            if (body.Disposition is not ("accepted" or "mitigated" or "escalated"))
                return Results.Json(new { code = "VALIDATION_FAILED" }, statusCode: 400);
            review.Disposition = body.Disposition;
            review.DispositionReason = body.Reason;
            review.DatesValidated = body.DatesValidated;
            Audit(db, actor, "contract.disposition", id, body.Disposition);
            db.SaveChanges();
            return Results.Ok(review);
        });

        api.MapPost("/commands/runs", (CommandInput body, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var visible = Access.VisibleMatters(db, actor).Select(m => m.Id).ToHashSet();
            if (!string.IsNullOrEmpty(body.MatterId) && !visible.Contains(body.MatterId)) return Missing();
            if (body.Query.Contains("Kabanda", StringComparison.OrdinalIgnoreCase) && Access.IsWalled(db, actor, DemoSeed.WalledMatter))
                return Missing();

            var owned = db.CommandRuns.Where(r => r.TenantId == actor.TenantId && r.ActorId == actor.UserId).ToList();
            string? conversationId = null;
            if (!string.IsNullOrWhiteSpace(body.ConversationId))
            {
                var existing = owned.Where(r => r.ConversationId == body.ConversationId || r.Id == body.ConversationId).ToList();
                if (existing.Count == 0 || existing.Any(r => !string.IsNullOrEmpty(r.MatterId) && !visible.Contains(r.MatterId)))
                    return Missing();
                conversationId = ConversationKey(existing[0]);
            }

            var run = new CommandRun
            {
                Id = NewId("run"), TenantId = actor.TenantId, ActorId = actor.UserId, MatterId = body.MatterId ?? "",
                Mode = body.Mode, Query = body.Query, At = DateTimeOffset.UtcNow, Tokens = 400
            };
            run.ConversationId = conversationId ?? run.Id;
            string? approvalId = null;
            string? draftDocumentId = null;

            if (body.Mode == "prepare" && !string.IsNullOrEmpty(body.MatterId))
            {
                var hash = Hash(body.Query);
                var doc = new MatterDocument
                {
                    Id = NewId("doc"), TenantId = actor.TenantId, MatterId = body.MatterId, Title = "Prepared draft",
                    Classification = "privileged", ApprovalState = "draft", UploaderId = actor.UserId,
                    OriginalHash = hash, MalwareClear = true
                };
                var version = new DocumentVersion
                {
                    Id = NewId("ver"), TenantId = actor.TenantId, DocumentId = doc.Id, Number = 1, Hash = hash,
                    UploaderId = actor.UserId, ArtifactKind = "draft", At = DateTimeOffset.UtcNow
                };
                doc.CurrentVersionId = version.Id;
                db.Documents.Add(doc);
                db.DocumentVersions.Add(version);
                draftDocumentId = doc.Id;
            }

            if (body.Mode == "act")
            {
                var approval = new Approval
                {
                    Id = NewId("apr"), TenantId = actor.TenantId, SubjectType = "command", SubjectId = run.Id,
                    PayloadHash = Hash(body.Query), RequestorId = actor.UserId, Decision = "pending", At = DateTimeOffset.UtcNow
                };
                db.Approvals.Add(approval);
                approvalId = approval.Id;
                run.State = "AWAITING_APPROVAL";
                run.ReviewState = "approval_required";
                run.ResultJson = JsonSerializer.Serialize(new
                {
                    label = "act",
                    body = $"Waiting for approval {approval.Id}. Nothing was published.",
                    autoPublished = false
                });
            }
            else if (body.Query.Contains("employment", StringComparison.OrdinalIgnoreCase) || body.Query.Contains("notice", StringComparison.OrdinalIgnoreCase))
            {
                var source = db.Sources.First();
                run.State = "COMPLETED";
                run.ReviewState = body.Mode == "prepare" ? "draft" : "informational";
                run.ResultJson = JsonSerializer.Serialize(new
                {
                    label = body.Mode == "prepare" ? "draft" : "answer",
                    body = source.Passage,
                    citation = source.Citation,
                    verification = source.VerificationStatus,
                    draftDocumentId,
                    autoPublished = false
                });
            }
            else if (body.Query.Contains("conversion", StringComparison.OrdinalIgnoreCase) || body.Query.Contains("enquir", StringComparison.OrdinalIgnoreCase))
            {
                var count = db.Enquiries.Count(e => e.TenantId == actor.TenantId);
                var whatsapp = db.Enquiries.Count(e => e.TenantId == actor.TenantId && e.SourceChannel == "whatsapp");
                run.State = "COMPLETED";
                run.ReviewState = "informational";
                run.ResultJson = JsonSerializer.Serialize(new { whatsapp, enquiries = count, draftDocumentId, definition = "Distinct enquiry ids by immutable first-touch channel." });
            }
            else
            {
                run.State = "NEEDS_REVIEW";
                run.ReviewState = "no-evidence";
                run.ResultJson = JsonSerializer.Serialize(new { evidence = "none", draftDocumentId, message = "No verified authority is available. No citation has been created." });
            }

            db.CommandRuns.Add(run);
            Meter(db, actor, "command", run.Id);
            Audit(db, actor, "command.run", run.Id, run.State);
            db.SaveChanges();
            return Results.Ok(RunPayload(db, run));
        });

        api.MapGet("/commands/runs", (HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var visible = Access.VisibleMatters(db, actor).Select(m => m.Id).ToHashSet();
            var runs = db.CommandRuns.Where(r => r.TenantId == actor.TenantId && r.ActorId == actor.UserId).ToList();
            var hidden = runs.Where(r => !string.IsNullOrEmpty(r.MatterId) && !visible.Contains(r.MatterId))
                .Select(ConversationKey).ToHashSet();
            var conversations = runs
                .Where(r => !hidden.Contains(ConversationKey(r)))
                .GroupBy(ConversationKey)
                .Select(group =>
                {
                    var ordered = group.OrderBy(r => r.At).ToList();
                    var latest = ordered[^1];
                    return new { id = group.Key, ordered[0].Query, latest.Mode, latest.State, latest.ReviewState, at = latest.At, latest.MatterId };
                })
                .OrderByDescending(r => r.at)
                .Take(20);
            return Results.Ok(conversations);
        });

        api.MapGet("/commands/runs/{id}", (string id, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var visible = Access.VisibleMatters(db, actor).Select(m => m.Id).ToHashSet();
            var owned = db.CommandRuns.Where(r => r.TenantId == actor.TenantId && r.ActorId == actor.UserId).ToList();
            var turns = owned.Where(r => r.ConversationId == id || r.Id == id).OrderBy(r => r.At).ToList();
            if (turns.Count == 0 || turns.Any(r => !string.IsNullOrEmpty(r.MatterId) && !visible.Contains(r.MatterId)))
                return Missing();
            return Results.Ok(new
            {
                id = ConversationKey(turns[0]),
                matterId = turns[^1].MatterId,
                turns = turns.Select(r => RunPayload(db, r)).ToList()
            });
        });

        api.MapPost("/approvals/{id}/decisions", (string id, DecisionInput body, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var approval = db.Approvals.FirstOrDefault(a => a.Id == id && a.TenantId == actor.TenantId);
            if (approval is null) return Missing();
            if (approval.RequestorId == actor.UserId)
                return Results.Json(new { code = "ACCESS_DENIED", message = "The requestor cannot approve their own action." }, statusCode: 403);
            if (actor.Role is not ("ManagingPartner" or "GeneralCounsel" or "TenantAdministrator"))
                return Results.Json(new { code = "ACCESS_DENIED" }, statusCode: 403);
            approval.Decision = body.Decision;
            approval.ApproverId = actor.UserId;
            if (approval.SubjectType == "document" && body.Decision == "approved")
            {
                var doc = db.Documents.FirstOrDefault(d => d.Id == approval.SubjectId);
                if (doc is not null) doc.ApprovalState = "approved";
            }
            if (approval.SubjectType == "command" && body.Decision == "approved")
            {
                var run = db.CommandRuns.FirstOrDefault(r => r.Id == approval.SubjectId);
                if (run is not null)
                {
                    run.State = "COMPLETED";
                    run.ReviewState = "approved";
                    run.ResultJson = JsonSerializer.Serialize(new { label = "approved act", body = "The action is approved and remains unpublished until a document is issued.", autoPublished = false });
                }
            }
            Audit(db, actor, "approval.decision", id, body.Decision);
            db.SaveChanges();
            return Results.Ok(approval);
        });

        api.MapPost("/documents/{id}/request-approval", (string id, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var doc = db.Documents.FirstOrDefault(d => d.Id == id && d.TenantId == actor.TenantId);
            if (doc is null) return Missing();
            var matter = db.Matters.FirstOrDefault(m => m.Id == doc.MatterId);
            if (matter is null || !Access.CanReadMatter(db, actor, matter)) return Missing();
            var approval = new Approval
            {
                Id = NewId("apr"), TenantId = actor.TenantId, SubjectType = "document", SubjectId = id,
                PayloadHash = doc.OriginalHash, RequestorId = actor.UserId, Decision = "pending", At = DateTimeOffset.UtcNow
            };
            db.Approvals.Add(approval);
            doc.ApprovalState = "review_requested";
            db.SaveChanges();
            return Results.Ok(approval);
        });

        api.MapGet("/approvals", (HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var query = db.Approvals.Where(a => a.TenantId == actor.TenantId);
            if (actor.Role is not ("ManagingPartner" or "GeneralCounsel" or "TenantAdministrator"))
                query = query.Where(a => a.RequestorId == actor.UserId);
            return Results.Ok(query.ToList().OrderByDescending(a => a.At).Take(50));
        });

        api.MapPost("/tasks/{id}/verify", (string id, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var task = db.Tasks.FirstOrDefault(t => t.Id == id && t.TenantId == actor.TenantId);
            if (task is null) return Missing();
            var matter = db.Matters.FirstOrDefault(m => m.Id == task.MatterId);
            if (matter is null || !Access.CanReadMatter(db, actor, matter)) return Missing();
            task.DateVerified = true;
            task.Status = "verified";
            Audit(db, actor, "task.verify", id, "ok");
            db.SaveChanges();
            return Results.Ok(task);
        });

        api.MapPost("/hearings/{id}/verify", (string id, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var hearing = db.LitigationEvents.FirstOrDefault(l => l.Id == id && l.TenantId == actor.TenantId);
            if (hearing is null) return Missing();
            var matter = db.Matters.FirstOrDefault(m => m.Id == hearing.MatterId);
            if (matter is null || !Access.CanReadMatter(db, actor, matter)) return Missing();
            hearing.VerifiedByCounsel = true;
            Audit(db, actor, "hearing.verify", id, "ok");
            db.SaveChanges();
            return Results.Ok(hearing);
        });

        api.MapPost("/obligations/{id}/status", (string id, ObligationInput body, HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var row = db.Obligations.FirstOrDefault(o => o.Id == id && o.TenantId == actor.TenantId);
            if (row is null) return Missing();
            if (body.Status is not ("open" or "in_review" or "complete"))
                return Results.Json(new { code = "VALIDATION_FAILED" }, statusCode: 400);
            row.Status = body.Status;
            Audit(db, actor, "obligation.status", id, body.Status);
            db.SaveChanges();
            return Results.Ok(row);
        });

        api.MapGet("/directory", (HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            if (actor.Role == "OticAdministrator") return Missing();
            var people = db.Users.Where(u => u.TenantId == actor.TenantId).Select(u => new { u.Id, u.Name, u.Email, u.Role }).ToList();
            var walls = db.EthicalWalls.Where(w => w.TenantId == actor.TenantId).ToList()
                .Where(w => Access.VisibleMatters(db, actor).Any(m => m.Id == w.MatterId) || actor.Role is "ManagingPartner" or "TenantAdministrator" or "GeneralCounsel")
                .Select(w => new { w.Id, w.MatterId, w.UserId, w.Reason, matterTitle = db.Matters.Where(m => m.Id == w.MatterId).Select(m => m.Title).FirstOrDefault() });
            return Results.Ok(new { people, walls });
        });

        api.MapGet("/analytics/metrics", (HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var enquiries = db.Enquiries.Where(e => e.TenantId == actor.TenantId && !e.TestOrSpam).ToList().Where(e => CanSeeEnquiry(db, actor, e)).ToList();
            var visible = Access.VisibleMatters(db, actor).ToList();
            var usage = db.UsageEvents.Where(u => u.TenantId == actor.TenantId).ToList();
            var metered = usage.Where(u => u.Agent is not ("cash-receipt" or "invoice")).ToList();
            return Results.Ok(new
            {
                refreshedAt = DateTimeOffset.UtcNow,
                definitions = new
                {
                    uniqueEnquiries = "Distinct enquiry ids. Messages are a different count.",
                    collectedRevenue = "Reconciled cash receipts. Invoices are a separate figure."
                },
                uniqueEnquiries = enquiries.Count,
                whatsappFirstTouch = enquiries.Count(e => e.SourceChannel == "whatsapp"),
                webFirstTouch = enquiries.Count(e => e.SourceChannel == "web"),
                visibleMatters = visible.Count,
                highRiskMatters = visible.Count(m => m.Risk == "high"),
                aiUnits = metered.Sum(u => u.Units),
                aiCost = metered.Sum(u => u.CostBasis),
                collectedRevenue = usage.Where(u => u.Agent == "cash-receipt").Sum(u => u.CostBasis),
                invoicedAmount = usage.Where(u => u.Agent == "invoice").Sum(u => u.CostBasis),
                regulatoryObligations = db.Obligations.Count(o => o.TenantId == actor.TenantId),
                outcomeProbability = (decimal?)null,
                activity = VisibleActivity(db, actor, visible, enquiries)
            });
        });

        api.MapGet("/portal/matters", (HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            if (actor.Role is not ("Client" or "BusinessRequestor"))
                return Results.Json(new { code = "ACCESS_DENIED" }, statusCode: 403);
            var rows = Access.VisibleMatters(db, actor).Select(m => new
            {
                m.Id,
                title = m.Title.Contains("Kabanda", StringComparison.OrdinalIgnoreCase) ? "Commercial matter" : m.Title,
                milestone = m.Status == "active" ? "In review" : m.Status,
                m.PracticeArea
            }).ToList();
            return Results.Ok(rows);
        });

        api.MapPost("/channels/whatsapp/webhook", (WhatsAppInput body, LipDb db) =>
        {
            var existing = db.InboundEvents.FirstOrDefault(e => e.Provider == "whatsapp" && e.ProviderEventId == body.ProviderEventId);
            if (existing is not null)
                return Results.Ok(new { duplicate = true, enquiryId = existing.EnquiryId });
            var enquiry = new Enquiry
            {
                Id = NewId("enq"), TenantId = body.TenantId, ClientId = body.ClientId ?? "", SourceChannel = "whatsapp", LatestChannel = "whatsapp",
                Category = body.Category, Jurisdiction = "Uganda", Summary = body.Summary, Stage = "new",
                ConsentVersion = "lip-privacy-1", ConsentStatus = body.ConsentGranted ? "granted" : "refused",
                ReceivedAt = DateTimeOffset.UtcNow
            };
            if (!body.ConsentGranted)
                return Results.Json(new { code = "VALIDATION_FAILED", message = "Consent is required." }, statusCode: 400);
            db.Enquiries.Add(enquiry);
            var conversationId = NewId("con");
            db.Conversations.Add(new Conversation
            {
                Id = conversationId, TenantId = body.TenantId, EnquiryId = enquiry.Id, Channel = "whatsapp",
                Queue = "unassigned", SlaStartedAt = enquiry.ReceivedAt
            });
            db.Messages.Add(new Message
            {
                Id = NewId("msg"), TenantId = body.TenantId, ConversationId = conversationId, Author = "client",
                Body = body.Summary, Kind = "human", At = enquiry.ReceivedAt
            });
            db.InboundEvents.Add(new InboundEvent
            {
                Id = NewId("in"), TenantId = body.TenantId, Provider = "whatsapp", ProviderEventId = body.ProviderEventId,
                EnquiryId = enquiry.Id, ReceivedAt = enquiry.ReceivedAt
            });
            db.SaveChanges();
            return Results.Ok(new { duplicate = false, enquiryId = enquiry.Id });
        });

        api.MapGet("/admin/tenants", (HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null || actor.Role != "OticAdministrator") return Results.Json(new { code = "ACCESS_DENIED" }, statusCode: 403);
            var tenants = db.Tenants.Where(t => t.Mode != "Platform").Select(t => new { t.Id, t.Name, t.Mode, t.Plan, t.Status, t.Region }).ToList();
            return Results.Ok(new { tenants, documents = Array.Empty<object>() });
        });

        api.MapGet("/admin/usage", (HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null || actor.Role != "OticAdministrator") return Results.Json(new { code = "ACCESS_DENIED" }, statusCode: 403);
            var rows = db.UsageEvents.GroupBy(u => u.TenantId).Select(g => new { tenantId = g.Key, units = g.Sum(x => x.Units), cost = g.Sum(x => x.CostBasis) }).ToList();
            return Results.Ok(new { rows, documentBodies = Array.Empty<string>() });
        });

        api.MapGet("/calendar", (HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            var matters = Access.VisibleMatters(db, actor).Select(m => m.Id).ToList();
            var tasks = db.Tasks.Where(t => t.TenantId == actor.TenantId && matters.Contains(t.MatterId)).ToList();
            var hearings = db.LitigationEvents.Where(l => l.TenantId == actor.TenantId && matters.Contains(l.MatterId)).ToList();
            return Results.Ok(new { tasks, hearings });
        });

        api.MapGet("/compliance", (HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            return Results.Ok(db.Obligations.Where(o => o.TenantId == actor.TenantId).ToList());
        });

        api.MapGet("/connectors", (HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            return Results.Ok(db.Connectors.Where(c => c.TenantId == actor.TenantId).ToList());
        });

        api.MapGet("/audit", (HttpRequest req, LipDb db, IConfiguration cfg) =>
        {
            var actor = Need(req, db, cfg);
            if (actor is null) return Denied();
            if (actor.Role is not ("TenantAdministrator" or "ManagingPartner" or "OticAdministrator" or "GeneralCounsel"))
                return Results.Json(new { code = "ACCESS_DENIED" }, statusCode: 403);
            var query = actor.Role == "OticAdministrator"
                ? db.AuditEvents.Where(a => a.Action != "document.upload")
                : db.AuditEvents.Where(a => a.TenantId == actor.TenantId);
            return Results.Ok(query.ToList().OrderByDescending(a => a.At).Take(100));
        });
    }

    private static string ConversationKey(CommandRun run) => string.IsNullOrEmpty(run.ConversationId) ? run.Id : run.ConversationId;

    private static object RunPayload(LipDb db, CommandRun run)
    {
        JsonElement? result = string.IsNullOrWhiteSpace(run.ResultJson) ? null : JsonSerializer.Deserialize<JsonElement>(run.ResultJson);
        string? draftDocumentId = null;
        if (result is JsonElement element && element.ValueKind == JsonValueKind.Object && element.TryGetProperty("draftDocumentId", out var draft) && draft.ValueKind == JsonValueKind.String)
            draftDocumentId = draft.GetString();
        var approvalId = db.Approvals.Where(a => a.TenantId == run.TenantId && a.SubjectType == "command" && a.SubjectId == run.Id)
            .Select(a => a.Id).FirstOrDefault();
        return new
        {
            run.Id,
            conversationId = ConversationKey(run),
            run.Mode,
            run.Query,
            run.State,
            run.ReviewState,
            run.MatterId,
            run.At,
            approvalId,
            draftDocumentId,
            result
        };
    }

    private static object PublicUser(AppUser user) => new { user.Id, user.Name, user.Email, user.Role, user.TenantId };

    private static Actor? Current(HttpRequest req, LipDb db, IConfiguration cfg)
    {
        var key = cfg["DemoAuth:Key"] ?? "lip-demo-signing-key";
        if (!DemoTokens.TryRead(req.Headers.Authorization.ToString(), key, out var userId)) return null;
        var user = db.Users.FirstOrDefault(u => u.Id == userId && u.Active);
        return user is null ? null : new Actor(user.Id, user.TenantId, user.Role, user.Name, user.Active);
    }

    private static Actor? Need(HttpRequest req, LipDb db, IConfiguration cfg) => Current(req, db, cfg);
    private static IResult Denied() => Results.Json(new { code = "AUTH_REQUIRED" }, statusCode: 401);
    private static IResult Missing() => Results.Json(new { code = "ACCESS_DENIED" }, statusCode: 404);

    private static void Audit(LipDb db, Actor actor, string action, string resource, string outcome) =>
        db.AuditEvents.Add(new AuditEvent
        {
            Id = NewId("aud"), TenantId = actor.TenantId, ActorId = actor.UserId, Action = action,
            Resource = resource, Outcome = outcome, CorrelationId = Guid.NewGuid().ToString("N"), At = DateTimeOffset.UtcNow
        });

    private static void Meter(LipDb db, Actor actor, string agent, string dedup)
    {
        db.UsageEvents.Add(new UsageEvent
        {
            Id = NewId("use"), TenantId = actor.TenantId, UserId = actor.UserId, Agent = agent, Channel = "web",
            Units = 400, CostBasis = 0.01m, DedupKey = dedup, At = DateTimeOffset.UtcNow
        });
    }

    private static Conversation? VisibleConversation(LipDb db, Actor actor, string id)
    {
        var conversation = db.Conversations.FirstOrDefault(c => c.Id == id && c.TenantId == actor.TenantId);
        if (conversation is null) return null;
        var enquiry = db.Enquiries.FirstOrDefault(e => e.Id == conversation.EnquiryId && e.TenantId == actor.TenantId);
        return enquiry is not null && CanSeeEnquiry(db, actor, enquiry) ? conversation : null;
    }

    private sealed record ActivityRow(string Action, string Resource, string Outcome, DateTimeOffset At);

    private static List<ActivityRow> VisibleActivity(LipDb db, Actor actor, List<Matter> visible, List<Enquiry> enquiries)
    {
        var matterIds = db.Matters.Where(m => m.TenantId == actor.TenantId).Select(m => m.Id).ToHashSet();
        var visibleMatterIds = visible.Select(m => m.Id).ToHashSet();
        var enquiryIds = db.Enquiries.Where(e => e.TenantId == actor.TenantId).Select(e => e.Id).ToHashSet();
        var visibleEnquiryIds = enquiries.Select(e => e.Id).ToHashSet();
        return db.AuditEvents.Where(a => a.TenantId == actor.TenantId).AsEnumerable()
            .Where(a => (!matterIds.Contains(a.Resource) || visibleMatterIds.Contains(a.Resource))
                && (!enquiryIds.Contains(a.Resource) || visibleEnquiryIds.Contains(a.Resource)))
            .OrderByDescending(a => a.At)
            .Take(6)
            .Select(a => new ActivityRow(a.Action, a.Resource, a.Outcome, a.At))
            .ToList();
    }

    private static bool CanSeeEnquiry(LipDb db, Actor actor, Enquiry enquiry)
    {
        if (enquiry.TenantId != actor.TenantId) return false;
        var linked = db.Matters.Where(m => m.EnquiryId == enquiry.Id && m.TenantId == actor.TenantId).ToList();
        if (linked.Count == 0) return true;
        return linked.Any(m => Access.CanReadMatter(db, actor, m));
    }

    private static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed record DemoLogin(string UserId);
public sealed record EnquiryInput(string Channel, string Category, string Summary, string? Jurisdiction, string? Urgency, string? ClientId, string? ConsentVersion, string? ConsentStatus);
public sealed record InboxMessageInput(string? Body);
public sealed record HandoverInput(string OwnerId, string? Reason);
public sealed record ConflictInput(string EnquiryId, string Counterparty);
public sealed record MatterInput(string EnquiryId, string Title, string? OwnerId, string PracticeArea, string? Clearance);
public sealed record DocumentInput(string MatterId, string Title, string Body, bool MalwareClear);
public sealed record ResearchInput(string Question, string? Jurisdiction, string? MatterId);
public sealed record DispositionInput(string Disposition, string Reason, bool DatesValidated);
public sealed record CommandInput(string Mode, string Query, string? MatterId, string? ConversationId);
public sealed record DecisionInput(string Decision);
public sealed record ClearanceInput(string Decision);
public sealed record ObligationInput(string Status);
public sealed record WhatsAppInput(string TenantId, string ProviderEventId, string Category, string Summary, bool ConsentGranted, string? ClientId);
