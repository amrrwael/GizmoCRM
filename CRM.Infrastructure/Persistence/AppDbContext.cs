using CRM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace CRM.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<Activity> Activities => Set<Activity>();

    public AppDbContext() { }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure Decimal
        modelBuilder.Entity<Deal>(entity =>
        {
            entity.Property(d => d.Value).HasColumnType("decimal(18,2)");
        });

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer(
                "Server=LAPTOP-0EPH8ELO\\MSSQLSERVER01;Database=GizmoCRM;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true",
                b => b.MigrationsAssembly("CRM.Infrastructure"));
        }
    }
}