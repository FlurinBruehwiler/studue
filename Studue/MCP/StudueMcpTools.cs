using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Studue.Services;

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
    string Room,
    string Teacher
);

public record NewAssignment(
    [property: Description(
        "Module code the assignment belongs to, exactly as returned by list_my_modules, e.g. 'XXM1.AN2'."
    )]
        string ModuleCode,
    [property: Description("Short title of the assignment, e.g. 'Exercise sheet 4'.")] string Title,
    [property: Description("Due date as 'yyyy-MM-dd'.")] string DueDate,
    [property: Description("Optional due time as 'HH:mm'. Omit when only the day is known.")]
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

/// <summary>
/// The MCP tool surface of Studue. Every tool acts as the student authenticated by
/// <see cref="McpAuthenticationHandler"/> and is limited to the modules that student attends.
/// </summary>
[McpServerToolType]
public class StudueMcpTools(
    StudentContext studentContext,
    StudueContext studueContext,
    AssignmentService assignmentService
)
{
    private Student Student =>
        studentContext.Student
        ?? throw new McpException("No student is authenticated for this request");

    [McpServerTool(Name = "list_my_modules")]
    [Description(
        "Lists the modules the student attends this semester. Call this first: the module codes it returns are the only values accepted by create_assignment and update_assignment."
    )]
    public async Task<List<ModuleDto>> ListMyModules()
    {
        var semester = Helper.GetCurrentSemester();

        return
        [
            .. (await studentContext.GetStudentModules()).Select(x => new ModuleDto(
                x.Code,
                x.Name,
                semester
            )),
        ];
    }

    [McpServerTool(Name = "list_assignments")]
    [Description(
        "Lists assignments of the student's modules for the current semester, earliest due date first. Assignments are shared: they are visible to and editable by everyone attending the module."
    )]
    public async Task<List<AssignmentDto>> ListAssignments(
        [Description("Optional module code to restrict the result to one module.")]
            string? moduleCode = null,
        [Description(
            "Optional earliest due date as 'yyyy-MM-dd'. Defaults to today; pass an earlier date to include overdue assignments."
        )]
            string? from = null,
        [Description("Optional latest due date as 'yyyy-MM-dd'.")] string? to = null
    )
    {
        var fromDate =
            from == null
                ? Helper.Now().Date
                : ParseDate(from, nameof(from)).ToDateTime(TimeOnly.MinValue);
        var toDate =
            to == null ? (DateTime?)null : ParseDate(to, nameof(to)).ToDateTime(TimeOnly.MaxValue);

        var query = BaseQuery().Where(x => x.DueDateTime >= fromDate);

        if (toDate != null)
            query = query.Where(x => x.DueDateTime <= toDate);

        if (moduleCode != null)
            query = query.Where(x => x.ModuleInstance.Module.Code == moduleCode);

        var assignments = await query.OrderBy(x => x.DueDateTime).ToListAsync();

        return assignments.Select(ToDto).ToList();
    }

    [McpServerTool(Name = "get_assignment")]
    [Description("Returns a single assignment by its id.")]
    public async Task<AssignmentDto> GetAssignment(
        [Description("Id of the assignment, as returned by list_assignments.")] int id
    )
    {
        return ToDto(await LoadAssignment(id));
    }

    [McpServerTool(Name = "get_schedule")]
    [Description(
        "Returns the student's weekly lecture schedule for the current semester. Useful to turn a deadline like 'until the next lecture' into an actual date."
    )]
    public async Task<List<ScheduleEntryDto>> GetSchedule()
    {
        var entries = await studentContext.GetScheduleEntriesForStudent(Student.StudentId);

        return entries
            .OrderBy(x => x.Weekday)
            .ThenBy(x => x.StartTime)
            .Select(x => new ScheduleEntryDto(
                Weekdays[Math.Clamp(x.Weekday, 0, Weekdays.Length - 1)],
                x.StartTime.ToString("HH:mm"),
                x.StartTime.AddMinutes(45 * x.Duration).ToString("HH:mm"),
                x.Module.Code,
                x.Module.Name,
                x.Room,
                x.Teacher
            ))
            .ToList();
    }

    [McpServerTool(Name = "create_assignment")]
    [Description(
        "Creates a new assignment for one of the student's modules. The assignment becomes visible to everyone attending that module."
    )]
    public async Task<AssignmentDto> CreateAssignment(
        [Description(
            "Module code the assignment belongs to, exactly as returned by list_my_modules, e.g. 'XXM1.AN2'."
        )]
            string moduleCode,
        [Description("Short title of the assignment, e.g. 'Exercise sheet 4'.")] string title,
        [Description("Due date as 'yyyy-MM-dd'.")] string dueDate,
        [Description("Optional due time as 'HH:mm'. Omit when only the day is known.")]
            string? dueTime = null,
        [Description(
            "Optional longer description: what to do, where to hand it in, which chapters."
        )]
            string? details = null,
        [Description("True for mandatory work, false for optional/voluntary work.")]
            bool mandatory = true
    )
    {
        var created = await CreateAssignments(
            [new NewAssignment(moduleCode, title, dueDate, dueTime, details, mandatory)]
        );

        return created.Single();
    }

    [McpServerTool(Name = "create_assignments")]
    [Description(
        "Creates several assignments at once, for example a whole semester plan taken from a syllabus. Either all of them are created or none: if any entry is invalid, nothing is written and the error names the offending entries by their position in the list."
    )]
    public async Task<List<AssignmentDto>> CreateAssignments(
        [Description("The assignments to create.")] IReadOnlyList<NewAssignment> assignments
    )
    {
        if (assignments.Count == 0)
            throw new McpException("No assignments were given");

        var student = Student;
        var models = new List<AssignmentModel>();
        var errors = new List<(int Index, string Message)>();

        for (var i = 0; i < assignments.Count; i++)
        {
            var item = assignments[i];
            try
            {
                var model = ToModel(item);

                if (student.ModuleInstances.All(x => x.Module.Code != model.ModuleCode))
                    throw new AssignmentRuleException(
                        $"'{model.ModuleCode}' is not a module {student.StudentId} attends"
                    );

                if (string.IsNullOrWhiteSpace(model.Title))
                    throw new AssignmentRuleException("An assignment needs a title");

                models.Add(model);
            }
            catch (Exception e) when (e is AssignmentRuleException or McpException)
            {
                errors.Add((i, e.Message));
            }
        }

        if (errors.Count == 1 && assignments.Count == 1)
            throw new McpException(errors[0].Message);

        if (errors.Count > 0)
            throw new McpException(
                $"Nothing was created. {errors.Count} of {assignments.Count} entries are invalid: "
                    + string.Join("; ", errors.Select(x => $"[{x.Index}] {x.Message}"))
            );

        var created = models.Select(model => assignmentService.Create(student, model)).ToList();
        await studueContext.SaveChangesAsync();

        return created.Select(ToDto).ToList();
    }

    [McpServerTool(Name = "update_assignment")]
    [Description(
        "Changes an existing assignment. Only the fields that are given are changed, everything else keeps its current value."
    )]
    public async Task<AssignmentDto> UpdateAssignment(
        [Description("Id of the assignment, as returned by list_assignments.")] int id,
        [Description("New title.")] string? title = null,
        [Description("New due date as 'yyyy-MM-dd'.")] string? dueDate = null,
        [Description(
            "New due time as 'HH:mm'. Pass an empty string to remove the time and leave only the date."
        )]
            string? dueTime = null,
        [Description("New description. Pass an empty string to remove it.")] string? details = null,
        [Description("New mandatory flag.")] bool? mandatory = null,
        [Description("Move the assignment to another module the student attends.")]
            string? moduleCode = null
    )
    {
        var assignment = await LoadAssignment(id);

        var model = new AssignmentModel
        {
            ModuleCode = moduleCode ?? assignment.ModuleInstance.Module.Code,
            Title = title ?? assignment.Title,
            Details = details == null ? assignment.Description : NullIfEmpty(details),
            DueDate =
                dueDate == null
                    ? DateOnly.FromDateTime(assignment.DueDateTime)
                    : ParseDate(dueDate, nameof(dueDate)),
            DueTime =
                dueTime == null
                    ? CurrentDueTime(assignment)
                    : ParseTime(NullIfEmpty(dueTime), nameof(dueTime)),
            Type =
                (mandatory ?? assignment.Mandatory)
                    ? AssignmentType.Mandatory
                    : AssignmentType.Optional,
        };

        Guard(() => assignmentService.Update(assignment, Student, model));
        await studueContext.SaveChangesAsync();

        return ToDto(assignment);
    }

    [McpServerTool(Name = "delete_assignment")]
    [Description(
        "Deletes an assignment. Everyone attending the module is allowed to do this, so only delete what the student explicitly asked to delete."
    )]
    public async Task<string> DeleteAssignment(
        [Description("Id of the assignment, as returned by list_assignments.")] int id
    )
    {
        var assignment = await LoadAssignment(id);

        Guard(() => assignmentService.Delete(assignment, Student));
        await studueContext.SaveChangesAsync();

        return $"Deleted assignment {id} '{assignment.Title}'";
    }

    private static readonly string[] Weekdays =
    [
        "Monday",
        "Tuesday",
        "Wednesday",
        "Thursday",
        "Friday",
        "Saturday",
        "Sunday",
    ];

    private IQueryable<Assignment> BaseQuery()
    {
        var student = Student;
        var semester = Helper.GetCurrentSemester();

        return studueContext
            .Assignements.Where(x =>
                x.ModuleInstance.Students.Contains(student)
                && x.ModuleInstance.Semester == semester
                && !x.IsDeleted
            )
            .Include(x => x.CreatedBy)
            .Include(x => x.UpdatedBy)
            .Include(x => x.CompletedByStudents)
            .Include(x => x.ModuleInstance)
            .ThenInclude(x => x.Module);
    }

    private async Task<Assignment> LoadAssignment(int id)
    {
        return await BaseQuery().FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new McpException(
                $"No assignment with id {id} in the modules {Student.StudentId} attends"
            );
    }

    private AssignmentDto ToDto(Assignment assignment)
    {
        var dueTime = CurrentDueTime(assignment);

        return new AssignmentDto(
            assignment.Id,
            assignment.ModuleInstance.Module.Code,
            assignment.ModuleInstance.Module.Name,
            assignment.Title,
            assignment.Description,
            DateOnly.FromDateTime(assignment.DueDateTime).ToString("yyyy-MM-dd"),
            dueTime?.ToString("HH:mm"),
            assignment.Mandatory,
            assignment.CompletedByStudents.Any(x => x.Id == Student.Id),
            assignment.CreatedBy.StudentId,
            assignment.UpdatedBy.StudentId
        );
    }

    //the app stores "no time given" as midnight, see Home.razor and Detail.razor
    private static TimeOnly? CurrentDueTime(Assignment assignment)
    {
        var time = TimeOnly.FromDateTime(assignment.DueDateTime);
        return time == TimeOnly.MinValue ? null : time;
    }

    private static AssignmentModel ToModel(NewAssignment item) =>
        new()
        {
            ModuleCode = item.ModuleCode,
            Title = item.Title,
            Details = NullIfEmpty(item.Details),
            DueDate = ParseDate(item.DueDate, nameof(item.DueDate)),
            DueTime = ParseTime(NullIfEmpty(item.DueTime), nameof(item.DueTime)),
            Type = item.Mandatory ? AssignmentType.Mandatory : AssignmentType.Optional,
        };

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static DateOnly ParseDate(string value, string parameterName) =>
        DateOnly.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var date)
            ? date
            : throw new McpException(
                $"{parameterName} '{value}' is not a date of the form yyyy-MM-dd"
            );

    private static TimeOnly? ParseTime(string? value, string parameterName)
    {
        if (value == null)
            return null;

        return TimeOnly.TryParse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            out var time
        )
            ? time
            : throw new McpException($"{parameterName} '{value}' is not a time of the form HH:mm");
    }

    //only McpException messages reach the client; anything else is replaced by a generic string
    private static void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (AssignmentRuleException e)
        {
            throw new McpException(e.Message);
        }
    }
}
