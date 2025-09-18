using Microsoft.EntityFrameworkCore;
using ProductionPlanning.Models;

namespace DataAccess.Data
{
    public class ApplicationDBContext : DbContext
    {
        public ApplicationDBContext(DbContextOptions<ApplicationDBContext> options) : base(options)
        {
        }

        // DbSets
        public DbSet<ModelData> ModelDatas { get; set; }
        public DbSet<ModelReference> ModelReferences { get; set; }
        public DbSet<LineConfiguration> LineConfigurations { get; set; }
        public DbSet<ProductionAssignment> ProductionAssignments { get; set; }
        public DbSet<OptimizedLineCapacity> OptimizedLineCapacities { get; set; }
        public DbSet<ShiftWorkConfiguration> ShiftWorkConfigurations { get; set; }
        public DbSet<OvertimeProductionAssignment> OvertimeProductionAssignments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // === ModelData -> ModelReference Relationship ===
            modelBuilder.Entity<ModelData>()
                .HasOne(md => md.ModelReference)
                .WithMany(mr => mr.ModelData)
                .HasForeignKey(md => md.ModelReferenceId)
                .OnDelete(DeleteBehavior.Restrict);

            // === OptimizedLineCapacity -> LineConfiguration Relationship ===
            modelBuilder.Entity<OptimizedLineCapacity>()
                .HasOne(olc => olc.LineConfiguration)
                .WithMany(lc => lc.OptimizedCapacities)
                .HasForeignKey(olc => olc.LineConfigurationId)
                .OnDelete(DeleteBehavior.Restrict);

            // === ProductionAssignment -> ModelData Relationship ===
            modelBuilder.Entity<ProductionAssignment>()
                .HasOne(pa => pa.ModelData)
                .WithMany(md => md.ProductionAssignments)
                .HasForeignKey(pa => pa.ModelDataId)
                .OnDelete(DeleteBehavior.Restrict);

            // === ProductionAssignment -> LineConfiguration Relationship ===
            modelBuilder.Entity<ProductionAssignment>()
                .HasOne(pa => pa.LineConfiguration)
                .WithMany(lc => lc.ProductionAssignments)
                .HasForeignKey(pa => pa.LineId)
                .OnDelete(DeleteBehavior.Restrict);

            // === OvertimeProductionAssignment -> ModelData Relationship ===
            modelBuilder.Entity<OvertimeProductionAssignment>()
                .HasOne(opa => opa.ModelData)
                .WithMany(md => md.OvertimeProductionAssignments)
                .HasForeignKey(opa => opa.ModelDataId)
                .OnDelete(DeleteBehavior.Restrict);

            // === OvertimeProductionAssignment -> LineConfiguration Relationship ===
            modelBuilder.Entity<OvertimeProductionAssignment>()
                .HasOne(opa => opa.LineConfiguration)
                .WithMany(lc => lc.OvertimeProductionAssignments)
                .HasForeignKey(opa => opa.LineId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}