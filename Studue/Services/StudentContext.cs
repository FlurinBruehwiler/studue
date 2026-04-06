using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Studue.Services;

public class StudentContext(IHttpClientFactory clientFactory, StudueContext context, ILogger<StudentContext> logger, IOptions<Settings> settings, IHostEnvironment environment, IDbContextFactory<StudueContext> studueContextFactory)
{
    public Student Student { get; private set; }
    public bool HasWriteAccess { get; set; }

    public async Task<Student?> GetOrCreateStudent(string studentId)
    {
        studentId = studentId.ToLower().Trim();

        try
        {
            //check existing student
            var student = await context.Students.Where(x => x.StudentId == studentId)
                .Include(x => x.ModuleInstances)
                .FirstOrDefaultAsync();

            //if not already exists, initialize
            student ??= await InitializeStudentInternal(studentId);

            if (student != null)
            {
                Student = student;
                await UpdateLastAccess(studentId);
            }

            return student;
        }
        catch (Exception e)
        {
            await GenerateIncident($"Could not initialize student with id {studentId}", e);
            return null;
        }
    }

    private async Task UpdateLastAccess(string studentId)
    {
        await using var dbContext = await studueContextFactory.CreateDbContextAsync();
        var stu = await dbContext.Students.FirstAsync(x => x.StudentId == studentId);
        if (stu.LastAccess != DateTime.Now)
        {
            stu.LastAccess = DateTime.Now;
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task GenerateIncident(string description, Exception? exception = null, bool sendMail = true)
    {
        await using var db = await studueContextFactory.CreateDbContextAsync();

        logger.LogError(exception, "Incident occured: {0}", description);

        var incident = new Incident
        {
            StackTrace = exception?.ToString(),
            Description = description,
            DateTime = DateTime.Now,
            UserId = Student?.StudentId
        };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();

        if (sendMail)
        {
            await SendMail("bruhwiler.flurin@gmail.com", "Incident", JsonSerializer.Serialize(incident), null, []); //avoid recursion
        }
    }

    public async Task<bool> SendMail(string recipient, string subject, string text, string? html, (HttpContent content, string name, string filename)[] additionalContents)
    {
        using var client = clientFactory.CreateClient();

        if (environment.IsDevelopment() && string.IsNullOrEmpty(settings.Value.MailgunApiKey))
            return true;

        var content = new MultipartFormDataContent
        {
            { new StringContent("Studue <verify@studue.ch>"), "from" },
            { new StringContent(recipient), "to" },
            { new StringContent(subject), "subject" },
            { new StringContent(text), "text" },
        };

        if (html != null)
        {
            content.Add(new StringContent(html), "html");
        }

        foreach (var additionalContent in additionalContents)
        {
            content.Add(additionalContent.content, additionalContent.name, additionalContent.filename);
        }

        var request = new HttpRequestMessage
        {
            RequestUri = new Uri("https://api.eu.mailgun.net/v3/studue.ch/messages"),
            Method = HttpMethod.Post,
            Content = content,
            Headers =
            {
                Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"api:{settings.Value.MailgunApiKey}"))),
            }
        };

        try
        {
            var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            await GenerateIncident($"Unable to send mail, {response}", sendMail: false); //don't send mail if mail fails....
        }
        catch (Exception e)
        {
            await GenerateIncident("Unable to send mail", e, sendMail: false); //don't send mail if mail fails....
        }
        return false;
    }

    public static string GenerateWriteToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    }

    private async Task<Student?> InitializeStudentInternal(string studentId)
    {
        if (studentId.Length != 8)
            return null;

        using var client = clientFactory.CreateClient();

        var semester = Helper.GetCurrentSemester();

        var response = await client.SendAsync(new HttpRequestMessage
        {
            Content = new FormUrlEncodedContent([
                new KeyValuePair<string, string>("ctl00$SelectionContent$txtSearch", studentId),
                new KeyValuePair<string, string>("ctl00$SelectionContent$selDepartment", "T"),
                new KeyValuePair<string, string>("ctl00$SelectionContent$selPeriodVersion", semester)]),
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://stundenplan.zhaw.ch/"),
        });

        var stream = await response.Content.ReadAsStringAsync();

        var parser = new HtmlParser();
        var document = parser.ParseDocument(stream);

        var searchHighlight = document.QuerySelector(".searchHighlight");

        if (searchHighlight == null) //this case we check, because when the studentId does not exist, we hit it
            return null;

        var className = searchHighlight.NextSibling!.TextContent.Trim(',', ' ');

        var allLessons = new List<Lesson>();
        foreach (var lessonElement in document.QuerySelectorAll(".left"))
        {
            var lesson = new Lesson();

            lesson.ModuleCode = NormalizeModuleCode(lessonElement.TextContent);
            lesson.ModuleName = lessonElement.GetAttribute("title")!;
            lesson.Semester = semester;

            var title = lessonElement.ParentElement!.GetAttribute("title")!;
            lesson.LessonId = int.Parse(title.Substring(title.IndexOf("id: ", StringComparison.Ordinal) + 4));

            var teacherElement = lessonElement.NextElementSibling!;
            lesson.TeacherName = RemoveShorthandFromTeacherName(teacherElement.GetAttribute("title")!);

            var roomElement = teacherElement.NextElementSibling!;
            lesson.RoomCode = roomElement.TextContent;

            var tableDefinition = lessonElement.ParentElement!.ParentElement!.ParentElement!;
            lesson.WeekdayNumber = tableDefinition.ParentElement!.IndexOf(tableDefinition);

            lesson.FirstLessonTime = tableDefinition.ParentElement!.FirstElementChild!.TextContent;

            allLessons.Add(lesson);
        }

        var newStudent = new Student();
        newStudent.WriteToken = GenerateWriteToken();
        newStudent.StudentId = studentId;
        newStudent.Class = className;
        if (studentId == "bruehflu")
            newStudent.IsAdmin = true;

        foreach (var x in allLessons.GroupBy(x => x.ModuleCode))
        {
            var moduleInstance = await GetOrCreateModuleInstance(x.Key, x.ToArray());
            newStudent.ModuleInstances.Add(moduleInstance);
        }

        context.Students.Add(newStudent);
        await context.SaveChangesAsync();

        return newStudent;

        async Task<ModuleInstance> GetOrCreateModuleInstance(string moduleCode, Lesson[] moduleLessons)
        {
            var module = await context.Modules.FirstOrDefaultAsync(x => x.Code == moduleCode);
            if (module == null)
            {
                module = new Module
                {
                    Code = moduleCode,
                    Name = moduleLessons.First().ModuleName ?? string.Empty
                };
                context.Modules.Add(module);
            }

            var lessonsId = string.Join(',', moduleLessons.Select(x => $"({x.Semester},{x.LessonId},{x.TeacherName},{x.RoomCode},{x.WeekdayNumber},{x.FirstLessonTime})"));
            var moduleInstance = await context.ModuleInstances.FirstOrDefaultAsync(x => x.LessionsId == lessonsId && x.Module == module);
            if (moduleInstance == null)
            {
                moduleInstance = new ModuleInstance
                {
                    LessionsId = lessonsId,
                    Module = module,
                    ProfessorNames = string.Join(", ", moduleLessons.Select(x => x.TeacherName).Distinct()),
                };
                context.ModuleInstances.Add(moduleInstance);
            }

            return moduleInstance;
        }
    }

    private static string NormalizeModuleCode(string moduleCode)
    {
        //XXM1.AN2.V => XXM1.AN2
        //XXM1.AN2-BL.V => XXM1.AN2

        var split = moduleCode.Split(".");

        if (split.Length == 1)
            return split[0];

        return split[0] + "." + split[1].Split("-")[0];
    }

    private static string RemoveShorthandFromTeacherName(string teacherName)
    {
        var idx = teacherName.IndexOf("(", StringComparison.Ordinal);
        if (idx == -1)
            return teacherName;

        return teacherName.Substring(0, idx).Trim();
    }

    class Lesson
    {
        public string ModuleCode = null!;
        public string? ModuleName;
        public string Semester = null!;
        public int LessonId;
        public string TeacherName = null!;
        public string RoomCode = null!;
        public int WeekdayNumber;
        public string FirstLessonTime = null!;
    }
}