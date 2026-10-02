using Microsoft.EntityFrameworkCore;

// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength

namespace Studue;

public class DatabaseContext(DbContextOptions<DatabaseContext> options) : DbContext(options)
{
    public DbSet<Student> Students { get; set; }
    public DbSet<Module> Modules { get; set; }
    public DbSet<ModuleInstance> ModuleInstances { get; set; }
    public DbSet<Assignment> Assignements { get; set; }
    public DbSet<EditLogEntry> EditLog { get; set; }
    public DbSet<Incident> Incidents { get; set; }
    public DbSet<ScheduleEntry> ScheduleEntries { get; set; }
    public DbSet<PushSubscriptionRow> PushSubscriptions { get; set; }
    public DbSet<Config> Configs { get; set; }
    public DbSet<Banner> Banners { get; set; }
    public DbSet<Event> Events { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Entity<Assignment>()
            .HasOne(x => x.CreatedBy)
            .WithMany(x => x.CreatedAssignments);

        modelBuilder.Entity<Assignment>().HasOne(x => x.UpdatedBy).WithMany();

        modelBuilder
            .Entity<Assignment>()
            .HasMany(x => x.CompletedByStudents)
            .WithMany(x => x.CompletedAssignments);

        modelBuilder
            .Entity<Banner>()
            .HasMany(x => x.DismissedByStudents)
            .WithMany(x => x.DismissedBanners);

        modelBuilder.Entity<ScheduleEntry>().OwnsMany(x => x.Occurrences, x => x.ToJson());

        modelBuilder
            .Entity<Student>()
            .HasMany(x => x.ScheduleEntries)
            .WithMany(x => x.Students);
    }
}

public class Event
{
    public int Id { get; set; }
    public required string EventType { get; set; }
    public required string Message { get; set; }
    public required DateTime DateTime { get; set; }
}

public class Banner
{
    public int Id { get; set; }
    public required string Message { get; set; }
    public string? LinkUrl { get; set; }
    public string? LinkText { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<Student> DismissedByStudents { get; set; } = new();
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

[Index(nameof(StudentId), IsUnique = true)]
public class Student
{
    public int Id { get; set; }
    public required string StudentId { get; set; }
    public required string Class { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsBanned { get; set; }
    public required string WriteToken { get; set; }
    public DateTime LastAccess { get; set; }
    public string LastFetchedSemester { get; set; }
    public DateTime LastAuthenticationMailSend { get; set; }

    public List<ModuleInstance> ModuleInstances { get; set; } = new();

    // The student's own lessons, as the last schedule fetch found them. A module instance
    // is shared by every class that has a lesson in common and only holds the lessons of
    // whoever created it, so it says which assignments a student shares, not when and
    // where the student has class.
    public List<ScheduleEntry> ScheduleEntries { get; set; } = new();
    public List<Assignment> CreatedAssignments { get; set; } = new();
    public List<Assignment> CompletedAssignments { get; set; } = new();
    public List<PushSubscriptionRow> PushSubscriptions { get; set; } = new();
    public List<Banner> DismissedBanners { get; set; } = new();
}

[Index(nameof(Code), IsUnique = true)]
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

[Index(nameof(Endpoint), IsUnique = true)]
public class PushSubscriptionRow
{
    public int Id { get; set; }
    public string Endpoint { get; set; }
    public string P256DH { get; set; }
    public string Auth { get; set; }

    public Student Student { get; set; }
}

public class Config
{
    public string Id { get; set; }
    public string Data { get; set; }
}

public class ScheduleEntry
{
    public int Id { get; set; }
    public string Semester { get; set; } = null!;
    public int ZhawID { get; set; }
    public Module Module { get; set; }
    public string Teacher { get; set; } = null!;
    public string? TeacherId { get; set; }
    public string Room { get; set; } = null!;
    public int Weekday { get; set; }
    public TimeOnly StartTime { get; set; }
    public int Duration { get; set; }
    public List<Student> Students { get; set; } = new();

    // The dates this lesson actually takes place on, taken from the iCal export of
    // stundenplan.zhaw.ch. The weekly grid above knows nothing about holidays or moved
    // lessons; this does. Empty when the export could not be fetched.
    public List<LessonOccurrence> Occurrences { get; set; } = new();
}

public class LessonOccurrence
{
    public DateOnly Date { get; set; }
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
}

public class Assignment
{
    public int Id { get; set; }
    public ModuleInstance ModuleInstance { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public List<Student> CompletedByStudents { get; set; } = new();
    public bool IsDeleted { get; set; }

    public DateTime DueDateTime { get; set; }
    public bool Mandatory { get; set; }
    public Student CreatedBy { get; set; } = null!;
    public DateTime CreatedTime { get; set; }

    public Student UpdatedBy { get; set; } = null!;
    public DateTime UpdatedTime { get; set; }
}
