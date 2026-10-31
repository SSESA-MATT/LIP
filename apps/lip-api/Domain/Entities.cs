namespace Lip.Api.Domain;

public sealed class Tenant
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Mode { get; set; } = "LawFirm";
    public string Plan { get; set; } = "Practice Essentials";
    public string Status { get; set; } = "active";
    public string Region { get; set; } = "westus2";
    public string Branding { get; set; } = "LIP";
}

public sealed class AppUser
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public bool Active { get; set; } = true;
}

public sealed class ClientRecord
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "person";
    public string VerifiedChannels { get; set; } = "";
    public string ConsentStatus { get; set; } = "granted";
}

public sealed class Enquiry
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string SourceChannel { get; set; } = "";
    public string LatestChannel { get; set; } = "";
    public string Category { get; set; } = "";
    public string Jurisdiction { get; set; } = "Uganda";
    public string Summary { get; set; } = "";
    public string Urgency { get; set; } = "normal";
    public string Stage { get; set; } = "new";
    public string OwnerId { get; set; } = "";
    public string ConsentVersion { get; set; } = "";
    public string ConsentStatus { get; set; } = "granted";
    public DateTimeOffset ReceivedAt { get; set; }
    public bool TestOrSpam { get; set; }
}

public sealed class Conversation
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string EnquiryId { get; set; } = "";
    public string Channel { get; set; } = "";
    public string Queue { get; set; } = "unassigned";
    public string OwnerId { get; set; } = "";
    public string Status { get; set; } = "open";
    public string Sensitivity { get; set; } = "confidential";
    public DateTimeOffset SlaStartedAt { get; set; }
}

public sealed class Message
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string ConversationId { get; set; } = "";
    public string Author { get; set; } = "";
    public string Body { get; set; } = "";
    public string Kind { get; set; } = "human";
    public DateTimeOffset At { get; set; }
}

public sealed class Matter
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string EnquiryId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string Title { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public string PracticeArea { get; set; } = "";
    public string Status { get; set; } = "proposed";
    public string ClearanceState { get; set; } = "pending";
    public string Confidentiality { get; set; } = "privileged";
    public string Jurisdiction { get; set; } = "Uganda";
    public string Risk { get; set; } = "medium";
    public int Version { get; set; } = 1;
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public decimal? RecordedExposure { get; set; }
    public string ExposureNote { get; set; } = "";
}

public sealed class MatterMember
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string MatterId { get; set; } = "";
    public string UserId { get; set; } = "";
}

public sealed class EthicalWall
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string MatterId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Reason { get; set; } = "";
}

public sealed class ConflictCheck
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string EnquiryId { get; set; } = "";
    public string MatterId { get; set; } = "";
    public string Counterparty { get; set; } = "";
    public string Disposition { get; set; } = "pending";
    public string Notes { get; set; } = "";
    public DateTimeOffset At { get; set; }
}

public sealed class WorkTask
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string MatterId { get; set; } = "";
    public string AssigneeId { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTimeOffset DueAt { get; set; }
    public string TimeZone { get; set; } = "Africa/Kampala";
    public bool DateVerified { get; set; }
    public string Status { get; set; } = "open";
}

public sealed class MatterDocument
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string MatterId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Classification { get; set; } = "privileged";
    public string ApprovalState { get; set; } = "draft";
    public string UploaderId { get; set; } = "";
    public string OriginalHash { get; set; } = "";
    public string CurrentVersionId { get; set; } = "";
    public bool MalwareClear { get; set; } = true;
}

public sealed class DocumentVersion
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string DocumentId { get; set; } = "";
    public int Number { get; set; }
    public string Hash { get; set; } = "";
    public string UploaderId { get; set; } = "";
    public string ArtifactKind { get; set; } = "original";
    public DateTimeOffset At { get; set; }
}

public sealed class LegalSource
{
    public string Id { get; set; } = "";
    public string Jurisdiction { get; set; } = "Uganda";
    public string Title { get; set; } = "";
    public string Citation { get; set; } = "";
    public string Passage { get; set; } = "";
    public DateTimeOffset EffectiveFrom { get; set; }
    public string LicenceStatus { get; set; } = "demonstration";
    public string VerificationStatus { get; set; } = "unverified-demonstration";
}

public sealed class ResearchNote
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string MatterId { get; set; } = "";
    public string AuthorId { get; set; } = "";
    public string Question { get; set; } = "";
    public string Jurisdiction { get; set; } = "Uganda";
    public string Body { get; set; } = "";
    public string ReviewState { get; set; } = "pending";
    public string CorpusVersion { get; set; } = "ug-demo-1";
    public DateTimeOffset At { get; set; }
}

public sealed class ContractReview
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string MatterId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Parties { get; set; } = "";
    public string GoverningLaw { get; set; } = "Uganda";
    public string ExtractedDates { get; set; } = "";
    public string DeviationRule { get; set; } = "";
    public string DeviationLocation { get; set; } = "";
    public string Severity { get; set; } = "medium";
    public string Disposition { get; set; } = "";
    public string DispositionReason { get; set; } = "";
    public bool DatesValidated { get; set; }
}

public sealed class Approval
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string SubjectType { get; set; } = "";
    public string SubjectId { get; set; } = "";
    public string PolicyVersion { get; set; } = "lip-approval-1";
    public string PayloadHash { get; set; } = "";
    public string RequestorId { get; set; } = "";
    public string ApproverId { get; set; } = "";
    public string Decision { get; set; } = "pending";
    public DateTimeOffset At { get; set; }
}

public sealed class LitigationEvent
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string MatterId { get; set; } = "";
    public string Forum { get; set; } = "";
    public string CaseReference { get; set; } = "";
    public string Kind { get; set; } = "hearing";
    public DateTimeOffset When { get; set; }
    public bool VerifiedByCounsel { get; set; }
    public string SourceNote { get; set; } = "";
}

public sealed class UsageEvent
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Agent { get; set; } = "";
    public string Channel { get; set; } = "web";
    public int Units { get; set; }
    public decimal CostBasis { get; set; }
    public string DedupKey { get; set; } = "";
    public DateTimeOffset At { get; set; }
}

public sealed class AuditEvent
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string Action { get; set; } = "";
    public string Resource { get; set; } = "";
    public string Outcome { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public DateTimeOffset At { get; set; }
}

public sealed class InboundEvent
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string Provider { get; set; } = "";
    public string ProviderEventId { get; set; } = "";
    public string EnquiryId { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
}

public sealed class CommandRun
{
    public string Id { get; set; } = "";
    public string ConversationId { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string MatterId { get; set; } = "";
    public string Mode { get; set; } = "ask";
    public string Query { get; set; } = "";
    public string State { get; set; } = "completed";
    public string ResultJson { get; set; } = "";
    public string ReviewState { get; set; } = "pending";
    public int Tokens { get; set; }
    public DateTimeOffset At { get; set; }
}

public sealed class Obligation
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string Title { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public string Status { get; set; } = "open";
    public DateTimeOffset ReviewDue { get; set; }
}

public sealed class ConnectorState
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Release { get; set; } = "Phase 2";
    public string Status { get; set; } = "demo-adapter";
    public string Detail { get; set; } = "";
}
