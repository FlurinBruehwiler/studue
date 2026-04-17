using Microsoft.EntityFrameworkCore;
// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength

namespace Studue;

public class StudueContext(DbContextOptions<StudueContext> options) : DbContext(options)
{
    public DbSet<Student> Students { get; set; }
    public DbSet<Module> Modules { get; set; }
    public DbSet<ModuleInstance> ModuleInstances { get; set; }
    public DbSet<Assignment> Assignements { get; set; }
    public DbSet<EditLogEntry> EditLog { get; set; }
    public DbSet<Incident> Incidents { get; set; }
    public DbSet<ScheduleEntry> ScheduleEntries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Assignment>()
            .HasOne(x => x.CreatedBy)
            .WithMany(x => x.CreatedAssignments);

        modelBuilder.Entity<Assignment>()
            .HasOne(x => x.UpdatedBy)
            .WithMany();
    }
}

public class Incident
{
    public int Id { get; set; }
    public required string Description { get; set; }
    public string? StackTrace { get; set; }
    public DateTime DateTime { get; set; }
    public string? UserId { get; set; }
}

public class EditLogEntry
{
    public int Id { get; set; }
    public required string Type { get; set; } //Add, Change, Delete
    public required Assignment Assignment { get; set; }
    public required Student Student { get; set; }
    public DateTime DateTime { get; set; }
    public string? ChangeInfo { get; set; } //
}

public class Student
{
    public int Id { get; set; }
    public required string StudentId { get; set; }
    public required string Class { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsBanned { get; set; }
    public required string WriteToken { get; set; }
    public DateTime LastAccess { get; set; }

    public List<ModuleInstance> ModuleInstances { get; set; } = new();
    public List<Assignment> CreatedAssignments { get; set; } = new();
}

public class Module
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public List<ModuleInstance> ModuleInstances { get; set; } = new();
}

public class ModuleInstance
{
    public int Id { get; set; }
    public required Module Module { get; set; }
    public string Semester { get; set; }

    public List<ScheduleEntry> ScheduleEntries { get; set; }
    public List<Student> Students { get; set; } = new();
    public List<Assignment> Assignements { get; set; } = new();
}

public class ScheduleEntry
{
    public string Semester { get; set; } = null!;
    public int ZhawID { get; set; }
    public Module Module { get; set; }
    public string Teacher { get; set; }
    public string Room { get; set; }
    public int Weekday { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public List<Student> Students { get; set; }
}

public class Assignment
{
    public int Id { get; set; }
    public ModuleInstance ModuleInstance { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsDeleted { get; set; }


    public DateTime DueDateTime { get; set; }
    public bool Mandatory { get; set; }
    public Student CreatedBy { get; set; } = null!;
    public DateTime CreatedTime { get; set; }

    public Student UpdatedBy { get; set; } = null!;
    public DateTime UpdatedTime { get; set; }
}
