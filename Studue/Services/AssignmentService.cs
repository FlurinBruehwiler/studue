namespace Studue.Services;

public class AssignmentRuleException(string message) : Exception(message);

public class AssignmentService(StudueContext context)
{
    public Assignment Create(Student student, AssignmentModel model)
    {
        var assignment = new Assignment { CreatedBy = student, CreatedTime = Helper.Now() };

        SetValues(assignment, model, student);

        context.EditLog.Add(
            new EditLogEntry
            {
                Assignment = assignment,
                Student = student,
                Type = "add",
                DateTime = Helper.Now(),
            }
        );
        context.Assignements.Add(assignment);

        return assignment;
    }

    public void Update(Assignment assignment, Student student, AssignmentModel model)
    {
        RequireAttendance(assignment, student);

        SetValues(assignment, model, student);

        context.EditLog.Add(
            new EditLogEntry
            {
                Assignment = assignment,
                Student = student,
                Type = "change",
                DateTime = Helper.Now(),
            }
        );
    }

    public void Delete(Assignment assignment, Student student)
    {
        RequireAttendance(assignment, student);

        assignment.IsDeleted = true;

        context.EditLog.Add(
            new EditLogEntry
            {
                Assignment = assignment,
                Student = student,
                Type = "delete",
                DateTime = Helper.Now(),
            }
        );
    }

    public static bool Attends(Assignment assignment, Student student) =>
        student.ModuleInstances.Any(x => x.Id == assignment.ModuleInstance.Id);

    private static void RequireAttendance(Assignment assignment, Student student)
    {
        if (!Attends(assignment, student))
            throw new AssignmentRuleException(
                $"{student.StudentId} does not attend the module of assignment {assignment.Id}"
            );
    }

    private static void SetValues(Assignment assignment, AssignmentModel formData, Student student)
    {
        if (string.IsNullOrWhiteSpace(formData.Title))
            throw new AssignmentRuleException("An assignment needs a title");

        assignment.UpdatedBy = student;
        assignment.UpdatedTime = Helper.Now();
        assignment.Title = formData.Title;
        assignment.Description = formData.Details;
        assignment.DueDateTime = new DateTime(formData.DueDate, formData.DueTime ?? new TimeOnly());
        assignment.Mandatory = formData.Type == AssignmentType.Mandatory;
        assignment.ModuleInstance = student.ModuleInstances.FirstOrDefault(x =>
            x.Module.Code == formData.ModuleCode
        )!;

        if (assignment.ModuleInstance == null)
            throw new AssignmentRuleException(
                $"Cannot create assignment with module instance {formData.ModuleCode}, because {student.StudentId} is not part of that module"
            );
    }
}
