using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SapApi.Domain.Entities;
using SapApi.Infrastructure.Security;

namespace SapApi.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser, ApplicationRole, int>(options)
{
    public DbSet<StageWisePayment> StageWisePayments => Set<StageWisePayment>();
    public DbSet<StageWisePaymentBatch> StageWisePaymentBatches => Set<StageWisePaymentBatch>();
    public DbSet<StageWisePaymentBatchLine> StageWisePaymentBatchLines => Set<StageWisePaymentBatchLine>();
    public DbSet<StageWisePaymentBatchLinePaymentTerm> StageWisePaymentBatchLinePaymentTerms => Set<StageWisePaymentBatchLinePaymentTerm>();
    public DbSet<ApprovalPolicy> ApprovalPolicies => Set<ApprovalPolicy>();
    public DbSet<ApprovalPolicyApprover> ApprovalPolicyApprovers => Set<ApprovalPolicyApprover>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<UserApproval> UserApprovals => Set<UserApproval>();
    public DbSet<ApprovalPolicyRule> ApprovalPolicyRules => Set<ApprovalPolicyRule>();
    public DbSet<IssueForProductionRequests> IssueForProductionRequests => Set<IssueForProductionRequests>();
    public DbSet<ReceiptFromProductionRequests> ReceiptFromProductionRequests => Set<ReceiptFromProductionRequests>();
    public DbSet<CacheEntry> CacheEntries => Set<CacheEntry>();

    public override int SaveChanges()
    {
        NormalizeDateTimesToUtc(ChangeTracker);
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        NormalizeDateTimesToUtc(ChangeTracker);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        NormalizeDateTimesToUtc(ChangeTracker);
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        NormalizeDateTimesToUtc(ChangeTracker);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private static void NormalizeDateTimesToUtc(ChangeTracker changeTracker)
    {
        foreach (var entry in changeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
                continue;

            foreach (var property in entry.Properties)
            {
                if (property.CurrentValue is null)
                    continue;

                if (property.Metadata.ClrType == typeof(DateTime))
                {
                    property.CurrentValue = DateTimeUtcConverter.ToUtc((DateTime)property.CurrentValue);
                }
                else if (property.Metadata.ClrType == typeof(DateTime?))
                {
                    property.CurrentValue = DateTimeUtcConverter.ToUtc((DateTime?)property.CurrentValue);
                }
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>()
            .Property(x => x.FullName)
            .HasMaxLength(150);

        modelBuilder.Entity<ApprovalRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CompanyDb).HasMaxLength(64).IsRequired();
            entity.HasIndex(p => new { p.CompanyDb, p.OverallStatus });
            entity.Property(e => e.RequestBody).HasConversion(EncryptedStringConverter.Instance);
            entity.Property(e => e.SupportingData).HasConversion(EncryptedStringConverter.Instance);
            entity.HasMany(r => r.UserApprovals).WithOne(u => u.ApprovalRequest).HasForeignKey(u => u.ApprovalRequestId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StageWisePayment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CompanyDb).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => new { e.CompanyDb, e.DocNumber });
            entity.Property(e => e.UtrNo).HasConversion(EncryptedStringConverter.Instance);
            entity.Property(e => e.Bank).HasConversion(EncryptedStringConverter.Instance);
            entity.HasOne<StageWisePaymentBatch>()
                .WithOne(b => b.StageWisePayment)
                .HasForeignKey<StageWisePaymentBatch>(b => b.StageWisePaymentId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<StageWisePaymentBatch>()
                .WithOne(b => b.DownPaymentStageWisePayment)
                .HasForeignKey<StageWisePaymentBatch>(b => b.DownPaymentStageWisePaymentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<StageWisePaymentBatch>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CompanyDb).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Account).HasConversion(EncryptedStringConverter.Instance);
            entity.HasIndex(e => new { e.CompanyDb, e.PoDocEntry });
            entity.HasIndex(e => e.ApprovalRequestId);
            entity.HasIndex(e => e.DownPaymentStageWisePaymentId);
            entity.HasMany(e => e.Lines).WithOne(l => l.Batch).HasForeignKey(l => l.BatchId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StageWisePaymentBatchLine>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Bank).HasConversion(EncryptedStringConverter.Instance);
            entity.HasMany(e => e.PaymentTerms).WithOne(t => t.Line).HasForeignKey(t => t.LineId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StageWisePaymentBatchLinePaymentTerm>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => new { e.LineId, e.PaymentTermsType }).IsUnique();
        });

        modelBuilder.Entity<ApprovalPolicy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CompanyDb).HasMaxLength(64).IsRequired();
            entity.HasIndex(p => new { p.CompanyDb, p.DocumentType });
            entity.HasMany(x => x.Approvers).WithOne(a => a.Policy).HasForeignKey(a => a.ApprovalPolicyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Rules).WithOne(a => a.Policy).HasForeignKey(a => a.ApprovalPolicyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.RequesterUser).WithMany().HasForeignKey(x => x.RequesterUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApprovalPolicyApprover>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasOne(x => x.ApproverUser).WithMany().HasForeignKey(x => x.ApproverUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserApproval>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(p => p.UserId);
            entity.HasOne(u => u.User).WithMany().HasForeignKey(u => u.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApprovalPolicyRule>(entity => entity.Property(x => x.Id).ValueGeneratedOnAdd());

        modelBuilder.Entity<IssueForProductionRequests>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedOnAdd();
            entity.Property(x => x.CompanyDb).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.CompanyDb);
        });

        modelBuilder.Entity<ReceiptFromProductionRequests>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedOnAdd();
            entity.Property(x => x.CompanyDb).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.CompanyDb);
        });

        modelBuilder.Entity<CacheEntry>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(512);
            entity.Property(e => e.CompanyDb).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => new { e.CompanyDb, e.ExpiresAtUtc });
        });
    }
}
