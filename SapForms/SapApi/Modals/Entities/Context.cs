using Microsoft.EntityFrameworkCore;

namespace SapApi.Modals.Entities
{
    public class Context(DbContextOptions options) : DbContext(options)
    {
        public DbSet<StageWisePayment> StageWisePayments => Set<StageWisePayment>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<StageWisePayment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
            });
            modelBuilder.Entity<ApprovalLogs>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
            });
            modelBuilder.Entity<ApprovalRequests>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
            });

        }
    }
}