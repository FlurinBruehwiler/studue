using Microsoft.EntityFrameworkCore;

namespace Studue.Services;

public class MigrationService(StudueContext studueContext, StudentContext studentContext)
{
    public async Task Migrate()
    {
        var students = await studueContext.Students.ToListAsync();
        foreach (var student in students)
        {
            await studentContext.InitializeOrUpdateStudentInternal(student, student.StudentId);
        }
    }
}