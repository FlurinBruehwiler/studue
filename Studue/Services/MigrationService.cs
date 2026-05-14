using Microsoft.EntityFrameworkCore;

namespace Studue.Services;

public class MigrationService(StudueContext studueContext, StudentContext studentContext)
{
    public async Task Migrate()
    {
        var version = await studueContext.Configs.FirstOrDefaultAsync(x => x.Id == "DbVersion");

        if (version == null)
        {
            studueContext.Configs.Add(new Config
            {
                Id = "DbVersion",
                Data = "2"
            });
            await studueContext.SaveChangesAsync();

            var students = await studueContext.Students.ToListAsync();
            foreach (var student in students)
            {
                await studentContext.InitializeOrUpdateStudentInternal(student, student.StudentId);
            }
        }
    }
}