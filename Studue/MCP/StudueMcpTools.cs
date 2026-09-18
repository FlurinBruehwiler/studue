using System.ComponentModel;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Studue.Services;

namespace Studue.MCP;

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
    public async Task<List<ModuleDto>> ListMyModules() =>
        await Run(
            async () =>
            {
                var semester = Helper.GetCurrentSemester();

                return (await studentContext.GetStudentModules())
                    .Select(x => new ModuleDto(x.Code, x.Name, semester))
                    .ToList();
            },
            nameof(ListMyModules)
        );

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
    ) =>
        await Run(
            async () =>
            {
                var fromDate =
                    from == null
                        ? Helper.Now().Date
                        : ParseDate(from, nameof(from)).ToDateTime(TimeOnly.MinValue);
                var toDate =
                    to == null
                        ? (DateTime?)null
                        : ParseDate(to, nameof(to)).ToDateTime(TimeOnly.MaxValue);

                var query = BaseQuery().Where(x => x.DueDateTime >= fromDate);

                if (toDate != null)
                    query = query.Where(x => x.DueDateTime <= toDate);

                if (moduleCode != null)
                {
                    if (AssignmentService.CurrentInstanceOf(Student, moduleCode) == null)
                        throw new McpException(
                            $"'{moduleCode}' is not a module {Student.StudentId} attends"
                        );

                    query = query.Where(x => x.ModuleInstance.Module.Code == moduleCode);
                }

                var assignments = await query.OrderBy(x => x.DueDateTime).ToListAsync();

                return assignments.Select(ToDto).ToList();
            },
            nameof(ListAssignments)
        );

    [McpServerTool(Name = "get_assignment")]
    [Description("Returns a single assignment by its id.")]
    public async Task<AssignmentDto> GetAssignment(
        [Description("Id of the assignment, as returned by list_assignments.")] int id
    ) => await Run(async () => ToDto(await LoadAssignment(id)), nameof(GetAssignment));

    [McpServerTool(Name = "get_schedule")]
    [Description(
        "Returns the student's weekly lecture schedule for the current semester. Useful to turn a deadline like 'until the next lecture' into an actual date."
    )]
    public async Task<List<ScheduleEntryDto>> GetSchedule() =>
        await Run(
            async () =>
            {
                var entries = await studentContext.GetScheduleEntriesForStudent(Student.StudentId);
                return entries
                    .GroupBy(x => (x.Weekday, x.StartTime, x.Duration, x.Module.Code))
                    .OrderBy(x => x.Key.Weekday)
                    .ThenBy(x => x.Key.StartTime)
                    .Select(lesson => new ScheduleEntryDto(
                        Weekdays[Math.Clamp(lesson.Key.Weekday, 0, Weekdays.Length - 1)],
                        lesson.Key.StartTime.ToString("HH:mm"),
                        ScheduleSlots
                            .EndTimeOf(lesson.Key.StartTime, lesson.Key.Duration)
                            .ToString("HH:mm"),
                        lesson.Key.Code,
                        lesson.First().Module.Name,
                        JoinDistinct(lesson.Select(x => x.Room)),
                        JoinDistinct(lesson.Select(x => x.Teacher))
                    ))
                    .ToList();
            },
            nameof(GetSchedule)
        );

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
        [Description(
            "Optional due time as 'HH:mm'. Omit when only the day is known. 00:00 is not accepted, it is how 'no time' is stored."
        )]
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
    ) =>
        await Run(
            async () =>
            {
                if (assignments.Count == 0)
                    throw new McpException("No assignments were given");

                if (assignments.Count > MaxBatchSize)
                    throw new McpException(
                        $"At most {MaxBatchSize} assignments can be created in one call, {assignments.Count} were given"
                    );

                var student = Student;
                var models = new List<AssignmentModel>();
                var errors = new List<(int Index, string Message)>();

                for (var i = 0; i < assignments.Count; i++)
                {
                    var item = assignments[i];
                    try
                    {
                        var model = ToModel(item);
                        if (AssignmentService.CurrentInstanceOf(student, model.ModuleCode) == null)
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

                var created = models
                    .Select(model => assignmentService.Create(student, model))
                    .ToList();
                await studueContext.SaveChangesAsync();

                return created.Select(ToDto).ToList();
            },
            nameof(CreateAssignments)
        );

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
    ) =>
        await Run(
            async () =>
            {
                var assignment = await LoadAssignment(id);

                RejectControlCharacters(title, nameof(title));
                RejectControlCharacters(details, nameof(details));

                var model = new AssignmentModel
                {
                    ModuleCode = moduleCode ?? assignment.ModuleInstance.Module.Code,
                    Title = title ?? assignment.Title,
                    Details = details == null ? assignment.Description : NullIfEmpty(details),
                    DueDate =
                        dueDate == null
                            ? DateOnly.FromDateTime(assignment.DueDateTime)
                            : ParseDueDate(dueDate, nameof(dueDate)),
                    DueTime =
                        dueTime == null
                            ? CurrentDueTime(assignment)
                            : ParseTime(NullIfEmpty(dueTime), nameof(dueTime)),
                    Type =
                        (mandatory ?? assignment.Mandatory)
                            ? AssignmentType.Mandatory
                            : AssignmentType.Optional,
                };

                assignmentService.Update(assignment, Student, model);
                await studueContext.SaveChangesAsync();

                return ToDto(assignment);
            },
            nameof(UpdateAssignment)
        );

    [McpServerTool(Name = "delete_assignment")]
    [Description(
        "Deletes an assignment. Everyone attending the module is allowed to do this, so only delete what the student explicitly asked to delete."
    )]
    public async Task<string> DeleteAssignment(
        [Description("Id of the assignment, as returned by list_assignments.")] int id
    ) =>
        await Run(
            async () =>
            {
                var assignment = await LoadAssignment(id);

                assignmentService.Delete(assignment, Student);
                await studueContext.SaveChangesAsync();

                return $"Deleted assignment {id} '{assignment.Title}'";
            },
            nameof(DeleteAssignment)
        );

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

    private static TimeOnly? CurrentDueTime(Assignment assignment)
    {
        var time = TimeOnly.FromDateTime(assignment.DueDateTime);
        return time == TimeOnly.MinValue ? null : time;
    }

    private static AssignmentModel ToModel(NewAssignment item)
    {
        RejectControlCharacters(item.Title, "title");
        RejectControlCharacters(item.Details, "details");

        return new AssignmentModel
        {
            ModuleCode = item.ModuleCode,
            Title = item.Title,
            Details = NullIfEmpty(item.Details),
            DueDate = ParseDueDate(item.DueDate, "dueDate"),
            DueTime = ParseTime(NullIfEmpty(item.DueTime), "dueTime"),
            Type = item.Mandatory ? AssignmentType.Mandatory : AssignmentType.Optional,
        };
    }

    private static void RejectControlCharacters(string? value, string parameterName)
    {
        if (value == null)
            return;

        foreach (var c in value)
        {
            if (char.IsControl(c) && c is not ('\n' or '\r' or '\t'))
                throw new McpException(
                    $"{parameterName} contains the control character U+{(int)c:X4}, which cannot be stored"
                );
        }
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string JoinDistinct(IEnumerable<string> values) =>
        string.Join(", ", values.Distinct().OrderBy(x => x, StringComparer.Ordinal));

    private const int MaxBatchSize = 100;
    private const int MaxYearsInThePast = 1;
    private const int MaxYearsInTheFuture = 2;

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy-M-d"];
    private static readonly string[] TimeFormats = ["HH:mm", "H:mm", "HH:mm:ss"];

    private static DateOnly ParseDueDate(string value, string parameterName)
    {
        var date = ParseDate(value, parameterName);

        var today = DateOnly.FromDateTime(Helper.Now());
        var earliest = today.AddYears(-MaxYearsInThePast);
        var latest = today.AddYears(MaxYearsInTheFuture);

        if (date < earliest || date > latest)
            throw new McpException(
                $"{parameterName} '{value}' is outside the range {earliest:yyyy-MM-dd} to {latest:yyyy-MM-dd} that an assignment can be due in"
            );

        return date;
    }

    private static DateOnly ParseDate(string value, string parameterName) =>
        DateOnly.TryParseExact(
            value,
            DateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date
        )
            ? date
            : throw new McpException(
                $"{parameterName} '{value}' is not a date of the form yyyy-MM-dd"
            );

    private static TimeOnly? ParseTime(string? value, string parameterName)
    {
        if (value == null)
            return null;

        if (
            !TimeOnly.TryParseExact(
                value,
                TimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var time
            )
        )
            throw new McpException($"{parameterName} '{value}' is not a time of the form HH:mm");

        var minutes = new TimeOnly(time.Hour, time.Minute);
        if (minutes == TimeOnly.MinValue)
            throw new McpException(
                $"{parameterName} '{value}' cannot be stored, because 00:00 is how an assignment without a due time is represented. Use 23:59 on the day before, or omit {parameterName}."
            );

        return minutes;
    }

    private async Task<T> Run<T>(Func<Task<T>> body, string toolName)
    {
        try
        {
            return await body();
        }
        catch (AssignmentRuleException e)
        {
            throw new McpException(e.Message);
        }
        catch (Exception e) when (e is not McpException)
        {
            await studentContext.GenerateIncident($"MCP tool {toolName} failed", e);
            throw new McpException("An error occured, try again later");
        }
    }
}
