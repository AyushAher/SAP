using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Shared.Entities
{
    public class Context(DbContextOptions options) : IdentityDbContext<
    ApplicationUser,
    ApplicationRole,
    int>(options)
    {
        public DbSet<StageWisePayment> StageWisePayments => Set<StageWisePayment>();
        public DbSet<ApprovalPolicy> ApprovalPolicies => Set<ApprovalPolicy>();
        public DbSet<ApprovalPolicyApprover> ApprovalPolicyApprovers => Set<ApprovalPolicyApprover>();
        public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
        public DbSet<UserApproval> UserApprovals => Set<UserApproval>();
        public DbSet<ApprovalPolicyRule> ApprovalPolicyRules => Set<ApprovalPolicyRule>();
        public DbSet<IssueForProductionRequests> IssueForProductionRequests => Set<IssueForProductionRequests>();
        public DbSet<ReceiptFromProductionRequests> ReceiptFromProductionRequests => Set<ReceiptFromProductionRequests>();


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>()
                .Property(x => x.FullName)
                .HasMaxLength(150);

            modelBuilder.Entity<StageWisePayment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
            });
            modelBuilder.Entity<ApprovalPolicy>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.HasIndex(p => p.DocumentType);

                entity.HasMany(x => x.Approvers)
                      .WithOne(a => a.Policy)
                      .HasForeignKey(a => a.ApprovalPolicyId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(x => x.Rules)
                      .WithOne(a => a.Policy)
                      .HasForeignKey(a => a.ApprovalPolicyId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.RequesterUser)
                      .WithMany()
                      .HasForeignKey(x => x.RequesterUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<ApprovalPolicyApprover>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.HasOne(x => x.ApproverUser)
                      .WithMany()
                      .HasForeignKey(x => x.ApproverUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ApprovalRequest>(entity =>
            {
                entity.HasMany(r => r.UserApprovals)
                .WithOne(u => u.ApprovalRequest)
                .HasForeignKey(u => u.ApprovalRequestId)
                .OnDelete(DeleteBehavior.Cascade);
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
            });


            modelBuilder.Entity<UserApproval>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.HasIndex(p => p.UserId);

                entity.HasOne(u => u.User)
                      .WithMany()
                      .HasForeignKey(u => u.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ApprovalRequest>(entity =>
            {
                entity.HasIndex(p => p.OverallStatus);
                entity.Property(x => x.Id).ValueGeneratedOnAdd();
            });
            modelBuilder.Entity<ApprovalPolicyRule>(entity =>
            {
                entity.Property(x => x.Id).ValueGeneratedOnAdd();
            });
            modelBuilder.Entity<IssueForProductionRequests>(entity =>
            {
                entity.Property(x => x.Id).ValueGeneratedOnAdd();
            });
            modelBuilder.Entity<ReceiptFromProductionRequests>(entity =>
            {
                entity.Property(x => x.Id).ValueGeneratedOnAdd();
            });
        }
    }
}