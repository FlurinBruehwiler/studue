using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Studue.Services;

public class StudentContext(IHttpClientFactory clientFactory, StudueContext context, ILogger<StudentContext> logger, IOptions<Settings> settings, IHostEnvironment environment, IDbContextFactory<StudueContext> studueContextFactory)
{
    public Student Student { get; private set; } = null!;
    public bool HasWriteAccess { get; set; }

    public async Task<(Student?, string)> GetOrCreateStudent(string studentId)
    {
        studentId = studentId.ToLower().Trim();

        try
        {
            //check existing student
            var student = await context.Students.Where(x => x.StudentId == studentId)
                .Include(x => x.ModuleInstances)
                .FirstOrDefaultAsync();

            //if not already exists, initialize
            student ??= await InitializeOrUpdateStudentInternal(null, studentId);

            if (student != null)
            {
                Student = student;
                await UpdateLastAccess(studentId);
            }

            return (student, $"We couldn't find a student with the student ID '{studentId}'");
        }
        catch (Exception e)
        {
            await GenerateIncident($"Could not initialize student with id {studentId}", e);
            return (null, "An error occured, try again later");
        }
    }

    private async Task UpdateLastAccess(string studentId)
    {
        await using var dbContext = await studueContextFactory.CreateDbContextAsync();
        var stu = await dbContext.Students.FirstAsync(x => x.StudentId == studentId);
        if (stu.LastAccess != Helper.Now())
        {
            stu.LastAccess = Helper.Now();
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
            DateTime = Helper.Now(),
            // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
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

    private async Task<IHtmlDocument?> GetDocumentForDepartement(string studentId, string departement, string semester)
    {
        using var client = clientFactory.CreateClient();

        var response = await client.SendAsync(new HttpRequestMessage
        {
            Content = new FormUrlEncodedContent([
                new KeyValuePair<string, string>("ctl00$SelectionContent$txtSearch", studentId),
                new KeyValuePair<string, string>("ctl00$SelectionContent$selDepartment", departement),
                new KeyValuePair<string, string>("ctl00$SelectionContent$selPeriodVersion", semester),
                new KeyValuePair<string, string>("ctl00$SelectionContent$selWeek", 8.ToString())]), //todo don't hardcode the week!
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://stundenplan.zhaw.ch/"),
        });

        var stream = await response.Content.ReadAsStringAsync();

        var parser = new HtmlParser();
        var document = parser.ParseDocument(stream);

        var searchHighlight = document.QuerySelector(".searchHighlight");

        if (searchHighlight == null) //this case we check, because when the studentId does not exist, we hit it
            return null;

        return document;
    }

    public async Task<Student?> InitializeOrUpdateStudentInternal(Student? existingStudent, string studentId)
    {
        if (studentId.Length != 8)
            return null;

        var semester = Helper.GetCurrentSemester();

        logger.LogInformation("Initializing student {studentId}", studentId);

        var document = await GetDocumentForDepartement(studentId, "T", semester);
        document ??= await GetDocumentForDepartement(studentId, "A", semester);

        if (document == null)
            return null;

        var oldModuleInstances = await context.ModuleInstances.Where(x => x.Semester == "OLD" || x.Semester == null)
            .Include(moduleInstance => moduleInstance.Module)
            .Include(moduleInstance => moduleInstance.Students)
            .Include(moduleInstance => moduleInstance.Assignements)
            .ToListAsync();

        var searchHighlight = document.QuerySelector(".searchHighlight")!;

        var className = searchHighlight.NextSibling!.TextContent.Trim(',', ' ');

        var cellToColumnMapping = GetCellToColumnMapping(document.QuerySelector("table")!.FirstElementChild!);

        var allLessons = new List<ScheduleEntry>();
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
            lesson.WeekdayNumber = cellToColumnMapping[tableDefinition];
            lesson.Duration = int.Parse(tableDefinition.GetAttribute("rowspan")!);

            lesson.FirstLessonTime = tableDefinition.ParentElement!.FirstElementChild!.TextContent;

            var startTime = TimeOnly.Parse(lesson.FirstLessonTime.Split("-").First().Trim());
            var scheduleEntry = await context.ScheduleEntries.FirstOrDefaultAsync(x => x.Module.Code == lesson.ModuleCode
                                                             && x.Semester == lesson.Semester
                                                             && x.ZhawID == lesson.LessonId
                                                             && x.Teacher == lesson.TeacherName
                                                             && x.Room == lesson.RoomCode
                                                             && x.Weekday == lesson.WeekdayNumber
                                                             && x.StartTime == startTime
                                                             && x.Duration == lesson.Duration);

            if (scheduleEntry == null)
            {
                scheduleEntry = new ScheduleEntry
                {
                    Room = lesson.RoomCode,
                    Semester = lesson.Semester,
                    Weekday = lesson.WeekdayNumber,
                    Teacher = lesson.TeacherName,
                    ZhawID = lesson.LessonId,
                    Module = await GetOrCreateModule(lesson.ModuleCode, lesson.ModuleName ?? ""),
                    StartTime = startTime,
                    Duration = lesson.Duration,
                };
                context.ScheduleEntries.Add(scheduleEntry);
            }

            allLessons.Add(scheduleEntry);
        }

        var newStudent = existingStudent ?? new Student
        {
            WriteToken = GenerateWriteToken(),
            StudentId = studentId,
            Class = className
        };
        if (studentId == "bruehflu")
            newStudent.IsAdmin = true;

        foreach (var x in allLessons.GroupBy(x => x.Module))
        {
            var moduleInstance = await GetOrCreateModuleInstance(x.Key, x.ToArray());
            newStudent.ModuleInstances.Add(moduleInstance);
        }

        if (existingStudent == null)
            context.Students.Add(newStudent);

        await context.SaveChangesAsync();

        await SendMail("bruhwiler.flurin@gmail.com", $"Studue signup: {studentId}", $"Initialized student {studentId}", null, []);

        return newStudent;

        async Task<ModuleInstance> GetOrCreateModuleInstance(Module module, ScheduleEntry[] scheduleEntries)
        {
            var moduleInstances = await context.ModuleInstances.Where(x => x.Module == module)
                .Include(moduleInstance => moduleInstance.ScheduleEntries).ToListAsync();
            var moduleInstance =
                moduleInstances.FirstOrDefault(x => x.ScheduleEntries.Any(y => scheduleEntries.Any(z => z.Id == y.Id)));
            if (moduleInstance == null)
            {
                moduleInstance = new ModuleInstance
                {
                    Module = module,
                    Semester = Helper.GetCurrentSemester(),
                    ScheduleEntries = scheduleEntries.ToList()
                };

                context.ModuleInstances.Add(moduleInstance);

                if (existingStudent != null)
                {
                    var oldModuleInstance = oldModuleInstances.FirstOrDefault(x => x.Module == module && x.Students.FirstOrDefault(y => y.Id == existingStudent.Id) != null);
                    if (oldModuleInstance != null)
                    {
                        Console.WriteLine("Found old module instance");

                        foreach (var assignement in oldModuleInstance.Assignements.ToList())
                        {
                            assignement.ModuleInstance = moduleInstance;
                            moduleInstance.Assignements.Add(assignement);
                        }

                        oldModuleInstance.Students.Clear();

                        context.ModuleInstances.Remove(oldModuleInstance);
                    }
                    else
                    {
                        Console.WriteLine("Did not find old module instance");
                    }
                }
            }

            return moduleInstance;
        }

        async Task<Module> GetOrCreateModule(string moduleCode, string moduleName)
        {
            var module = await context.Modules.FirstOrDefaultAsync(x => x.Code == moduleCode);
            if (module == null)
            {
                module = new Module
                {
                    Code = moduleCode,
                    Name = moduleName
                };
                context.Modules.Add(module);
            }

            return module;
        }
    }

    public async Task<List<ScheduleEntry>> GetScheduleEntriesForStudent(string studentId)
    {
        var currentSemester = Helper.GetCurrentSemester();

        var student = await context.Students.Where(x => x.StudentId == studentId)
            .Include(x => x.ModuleInstances)
            .ThenInclude(x => x.ScheduleEntries)
            .ThenInclude(x => x.Module)
            .FirstAsync();

        return student.ModuleInstances.Where(x => x.Semester == currentSemester).SelectMany(x => x.ScheduleEntries).ToList();
    }

    private static Dictionary<IElement, int> GetCellToColumnMapping(IElement htmlTable)
    {
        var result = new Dictionary<IElement, int>();

        int[] columnSpans = new int[6];

        foreach (var row in htmlTable.Children.Skip(1)) // skip header
        {
            var column = 0;

            foreach (var cell in row.Children.Skip(1)) // skip time
            {
                while (columnSpans[column] > 0)
                {
                    columnSpans[column]--;
                    column++;
                }

                result.Add(cell, column);

                var rowspan = int.Parse(cell.GetAttribute("rowspan")!);
                columnSpans[column] = rowspan - 1;

                column++;
            }

            for (var i = column; i < columnSpans.Length; i++) {
                if (columnSpans[i] > 0) {
                    columnSpans[i]--;
                }
            }
        }

        return result;
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
        public int Duration;
    }
}
