using System.ComponentModel;

namespace Studue.MCP;

public record ModuleDto(string Code, string Name, string Semester);

public record AssignmentDto(
    int Id,
    string ModuleCode,
    string ModuleName,
    string Title,
    string? Details,
    string DueDate,
    string? DueTime,
    bool Mandatory,
    bool CompletedByMe,
    string CreatedBy,
    string UpdatedBy
);

public record ScheduleEntryDto(
    string Weekday,
    string StartTime,
    string EndTime,
    string ModuleCode,
    string ModuleName,
    string Rooms,
    string Teachers
);

public record NewAssignment(
    [property: Description(
        "Module code the assignment belongs to, exactly as returned by list_my_modules, e.g. 'XXM1.AN2'."
    )]
        string ModuleCode,
    [property: Description("Short title of the assignment, e.g. 'Exercise sheet 4'.")] string Title,
    [property: Description("Due date as 'yyyy-MM-dd'.")] string DueDate,
    [property: Description(
        "Optional due time as 'HH:mm'. Omit when only the day is known. 00:00 is not accepted, it is how 'no time' is stored."
    )]
        string? DueTime = null,
    [property: Description(
        "Optional longer description: what to do, where to hand it in, which chapters, links etc."
    )]
        string? Details = null,
    [property: Description(
        "True for mandatory work, false for optional/voluntary work. Defaults to true."
    )]
        bool Mandatory = true
);
