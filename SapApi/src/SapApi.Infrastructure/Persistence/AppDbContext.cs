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
    public DbSet<ApprovalLog> ApprovalLogs => Set<ApprovalLog>();
    public DbSet<UserGroup> UserGroups => Set<UserGroup>();
    public DbSet<UserGroupMember> UserGroupMembers => Set<UserGroupMember>();
    public DbSet<IssueForProductionRequests> IssueForProductionRequests => Set<IssueForProductionRequests>();
    public DbSet<ReceiptFromProductionRequests> ReceiptFromProductionRequests => Set<ReceiptFromProductionRequests>();
    public DbSet<CacheEntry> CacheEntries => Set<CacheEntry>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<PurchaseOrderPaymentTerm> PurchaseOrderPaymentTerms => Set<PurchaseOrderPaymentTerm>();
    public DbSet<PurchaseOrderSyncState> PurchaseOrderSyncStates => Set<PurchaseOrderSyncState>();

    public override int SaveChanges()
    {
        NormalizeDateTimesToUtc(ChangeTracker);
        ApplySoftDeletes(ChangeTracker);
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        NormalizeDateTimesToUtc(ChangeTracker);
        ApplySoftDeletes(ChangeTracker);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        NormalizeDateTimesToUtc(ChangeTracker);
        ApplySoftDeletes(ChangeTracker);
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        NormalizeDateTimesToUtc(ChangeTracker);
        ApplySoftDeletes(ChangeTracker);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private static void ApplySoftDeletes(ChangeTracker changeTracker)
    {
        foreach (var entry in changeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State != EntityState.Deleted)
                continue;

            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
        }
    }

    private static void NormalizeDateTimesToUtc(ChangeTracker changeTracker)
    {
        foreach (var entry in changeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
                continue;

            foreach (var property in entry.Properties)
            {
                // Pattern-match covers both DateTime and boxed DateTime from DateTime?
                // (ClrType checks alone can miss nullable properties depending on EF version).
                if (property.CurrentValue is DateTime dateTime)
                    property.CurrentValue = DateTimeUtcConverter.ToUtc(dateTime);
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.FullName).HasMaxLength(150);
            entity.ConfigureSoftDeleteProperty();
            entity.HasIndex(u => u.NormalizedUserName)
                .IsUnique()
                .HasDatabaseName("UserNameIndex")
                .HasFilter(SoftDeleteModelBuilderExtensions.ActiveRowFilter);
        });

        modelBuilder.Entity<ApplicationRole>(entity =>
        {
            entity.ConfigureSoftDeleteProperty();
            entity.HasIndex(r => r.NormalizedName)
                .IsUnique()
                .HasDatabaseName("RoleNameIndex")
                .HasFilter(SoftDeleteModelBuilderExtensions.ActiveRowFilter);
        });

        modelBuilder.Entity<StageWisePayment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ConfigureSoftDeleteProperty();
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CompanyDb).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => new { e.CompanyDb, e.DocNumber });
            entity.HasIndex(e => e.PurchaseOrderId);
            entity.Property(e => e.UtrNo).HasConversion(EncryptedStringConverter.Instance);
            entity.Property(e => e.Bank).HasConversion(EncryptedStringConverter.Instance);
            entity.HasOne(e => e.PurchaseOrder)
                .WithMany(p => p.StageWisePayments)
                .HasForeignKey(e => e.PurchaseOrderId)
                .OnDelete(DeleteBehavior.SetNull);
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
            entity.ConfigureSoftDeleteProperty();
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CompanyDb).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Account).HasConversion(EncryptedStringConverter.Instance);
            entity.HasIndex(e => new { e.CompanyDb, e.PoDocEntry });
            entity.HasIndex(e => e.PurchaseOrderId);
            entity.HasIndex(e => e.ApprovalRequestId);
            entity.HasIndex(e => e.DownPaymentStageWisePaymentId);
            entity.HasOne(e => e.PurchaseOrder)
                .WithMany(p => p.StageWisePaymentBatches)
                .HasForeignKey(e => e.PurchaseOrderId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(e => e.Lines).WithOne(l => l.Batch).HasForeignKey(l => l.BatchId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApprovalRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ConfigureSoftDeleteProperty();
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CompanyDb).HasMaxLength(64).IsRequired();
            entity.HasIndex(p => new { p.CompanyDb, p.OverallStatus });
            entity.HasIndex(e => e.PurchaseOrderId);
            entity.Property(e => e.RequestBody).HasConversion(EncryptedStringConverter.Instance);
            entity.Property(e => e.SupportingData).HasConversion(EncryptedStringConverter.Instance);
            entity.HasOne(e => e.PurchaseOrder)
                .WithMany(p => p.ApprovalRequests)
                .HasForeignKey(e => e.PurchaseOrderId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Policy)
                .WithMany()
                .HasForeignKey(e => e.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.RequesterUser)
                .WithMany(u => u.ApprovalRequest)
                .HasForeignKey(e => e.RequesterUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(r => r.UserApprovals).WithOne(u => u.ApprovalRequest).HasForeignKey(u => u.ApprovalRequestId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApprovalLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ConfigureSoftDeleteProperty();
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CompanyDb).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Action).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => new { e.CompanyDb, e.ApprovalRequestId });
            entity.HasIndex(e => e.ActionByUserId);
            entity.Property(e => e.OldValue).HasConversion(EncryptedStringConverter.Instance);
            entity.Property(e => e.NewValue).HasConversion(EncryptedStringConverter.Instance);
            entity.HasOne(e => e.ApprovalRequest).WithMany().HasForeignKey(e => e.ApprovalRequestId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ActionByUser).WithMany().HasForeignKey(e => e.ActionByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StageWisePaymentBatchLine>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ConfigureSoftDeleteProperty();
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Bank).HasConversion(EncryptedStringConverter.Instance);
            entity.HasMany(e => e.PaymentTerms).WithOne(t => t.Line).HasForeignKey(t => t.LineId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StageWisePaymentBatchLinePaymentTerm>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ConfigureSoftDeleteProperty();
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => new { e.LineId, e.PaymentTermsType }).IsUniqueAmongActiveRows();
        });

        modelBuilder.Entity<UserGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ConfigureSoftDeleteProperty();
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CompanyDb).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasIndex(e => new { e.CompanyDb, e.Name }).IsUniqueAmongActiveRows();
            entity.HasMany(e => e.Members).WithOne(m => m.Group).HasForeignKey(m => m.UserGroupId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserGroupMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ConfigureSoftDeleteProperty();
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => new { e.UserGroupId, e.UserId }).IsUniqueAmongActiveRows();
            // A user may belong to only one group at a time.
            entity.HasIndex(e => e.UserId).IsUniqueAmongActiveRows();
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApprovalPolicy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ConfigureSoftDeleteProperty();
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CompanyDb).HasMaxLength(64).IsRequired();
            entity.HasIndex(p => new { p.CompanyDb, p.DocumentType });
            entity.HasMany(x => x.Approvers).WithOne(a => a.Policy).HasForeignKey(a => a.ApprovalPolicyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Rules).WithOne(a => a.Policy).HasForeignKey(a => a.ApprovalPolicyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.RequesterUser).WithMany(u => u.Policy).HasForeignKey(x => x.RequesterUserId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
            entity.HasOne(x => x.RequesterGroup).WithMany().HasForeignKey(x => x.RequesterGroupId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        });

        modelBuilder.Entity<ApprovalPolicyApprover>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ConfigureSoftDeleteProperty();
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasOne(x => x.ApproverUser).WithMany(u => u.PolicyApprover).HasForeignKey(x => x.ApproverUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserApproval>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ConfigureSoftDeleteProperty();
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(p => p.UserId);
            entity.HasOne(u => u.User).WithMany().HasForeignKey(u => u.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApprovalPolicyRule>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedOnAdd();
            entity.ConfigureSoftDeleteProperty();
        });

        modelBuilder.Entity<IssueForProductionRequests>(entity =>
        {
            entity.ConfigureSoftDeleteProperty();
            entity.Property(x => x.Id).ValueGeneratedOnAdd();
            entity.Property(x => x.CompanyDb).HasMaxLength(64).IsRequired();
            entity.Property(x => x.CreatedByUserName).HasMaxLength(150);
            entity.Property(x => x.WorkerName).HasMaxLength(200);
            entity.HasIndex(x => x.CompanyDb);
        });

        modelBuilder.Entity<ReceiptFromProductionRequests>(entity =>
        {
            entity.ConfigureSoftDeleteProperty();
            entity.Property(x => x.Id).ValueGeneratedOnAdd();
            entity.Property(x => x.CompanyDb).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.CompanyDb);
        });

        modelBuilder.Entity<CacheEntry>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.ConfigureSoftDeleteProperty();
            entity.Property(e => e.Key).HasMaxLength(512);
            entity.Property(e => e.CompanyDb).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => new { e.CompanyDb, e.ExpiresAtUtc });
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ConfigureSoftDeleteProperty();
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CompanyDb).HasMaxLength(64).IsRequired();
            entity.Property(e => e.CardCode).HasMaxLength(50);
            entity.Property(e => e.CardName).HasMaxLength(200);
            entity.Property(e => e.Project).HasMaxLength(50);
            entity.Property(e => e.DocumentStatus).HasMaxLength(32);
            entity.Property(e => e.DocType).HasMaxLength(32);
            entity.HasIndex(e => new { e.CompanyDb, e.DocEntry }).IsUniqueAmongActiveRows();
            entity.HasIndex(e => new { e.CompanyDb, e.DocNum });
            entity.HasIndex(e => new { e.CompanyDb, e.DocDate });
            entity.HasIndex(e => new { e.CompanyDb, e.CardCode });
            entity.HasMany(e => e.Lines).WithOne(l => l.PurchaseOrder).HasForeignKey(l => l.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.PaymentTerms).WithOne(t => t.PurchaseOrder).HasForeignKey(t => t.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PurchaseOrderLine>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ConfigureSoftDeleteProperty();
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.ItemCode).HasMaxLength(50);
            entity.Property(e => e.WarehouseCode).HasMaxLength(20);
            entity.HasIndex(e => new { e.PurchaseOrderId, e.LineNum }).IsUniqueAmongActiveRows();
        });

        modelBuilder.Entity<PurchaseOrderPaymentTerm>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ConfigureSoftDeleteProperty();
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => new { e.PurchaseOrderId, e.Slot }).IsUniqueAmongActiveRows();
        });

        modelBuilder.Entity<PurchaseOrderSyncState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ConfigureSoftDeleteProperty();
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CompanyDb).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(32).IsRequired();
            entity.Property(e => e.HangfireJobId).HasMaxLength(64);
            entity.Property(e => e.LastSyncMessage).HasMaxLength(2000);
            entity.HasIndex(e => e.CompanyDb).IsUnique();
        });

        modelBuilder.ApplySoftDeleteQueryFilters();

        // Npgsql rejects DateTime Kind=Unspecified for timestamptz. Convert at the model layer
        // so every write (including SaveChanges inside transactions) is UTC regardless of caller.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(DateTimeUtcConverter.Required);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(DateTimeUtcConverter.Optional);
            }
        }
    }
}
