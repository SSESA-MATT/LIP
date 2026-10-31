using Lip.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Lip.Api.Data;

public sealed class LipDb(DbContextOptions<LipDb> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<ClientRecord> Clients => Set<ClientRecord>();
    public DbSet<Enquiry> Enquiries => Set<Enquiry>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Matter> Matters => Set<Matter>();
    public DbSet<MatterMember> MatterMembers => Set<MatterMember>();
    public DbSet<EthicalWall> EthicalWalls => Set<EthicalWall>();
    public DbSet<ConflictCheck> ConflictChecks => Set<ConflictCheck>();
    public DbSet<WorkTask> Tasks => Set<WorkTask>();
    public DbSet<MatterDocument> Documents => Set<MatterDocument>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<LegalSource> Sources => Set<LegalSource>();
    public DbSet<ResearchNote> ResearchNotes => Set<ResearchNote>();
    public DbSet<ContractReview> Contracts => Set<ContractReview>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<LitigationEvent> LitigationEvents => Set<LitigationEvent>();
    public DbSet<UsageEvent> UsageEvents => Set<UsageEvent>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<InboundEvent> InboundEvents => Set<InboundEvent>();
    public DbSet<CommandRun> CommandRuns => Set<CommandRun>();
    public DbSet<Obligation> Obligations => Set<Obligation>();
    public DbSet<ConnectorState> Connectors => Set<ConnectorState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboundEvent>().HasIndex(e => new { e.Provider, e.ProviderEventId }).IsUnique();
        modelBuilder.Entity<UsageEvent>().HasIndex(e => e.DedupKey).IsUnique();
        modelBuilder.Entity<Enquiry>().HasIndex(e => new { e.TenantId, e.SourceChannel, e.ReceivedAt });
        modelBuilder.Entity<Matter>().HasIndex(e => new { e.TenantId, e.Status });
    }
}
