using System.Net;
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

public class StudentContext(
    IHttpClientFactory clientFactory,
    DatabaseContext context,
    ILogger<StudentContext> logger,
    IOptions<Settings> settings,
    IHostEnvironment environment,
    IDbContextFactory<DatabaseContext> studueContextFactory
)
{
    public Student Student { get; private set; } = null!;
    public bool HasWriteAccess { get; set; }

    //A colour belongs to the student's current module set, not to the module itself: the set is
    //small enough for distinct colours, it needs no storage, and both views derive the same map.
    private static readonly string[] ModuleAccents =
    [
        "#0061a2",
        "#0f766e",
        "#7c3aed",
        "#b45309",
        "#be123c",
        "#4338ca",
        "#0369a1",
        "#15803d",
        "#c2410c",
        "#9333ea",
    ];

    private IReadOnlyDictionary<string, string>? _moduleAccents;

    public async Task<IReadOnlyDictionary<string, string>> GetModuleAccents()
    {
        if (_moduleAccents != null)
            return _moduleAccents;

        var semester = Helper.GetCurrentSemester();

        var codes = await context
            .ModuleInstances.Where(x => x.Students.Contains(Student) && x.Semester == semester)
            .Select(x => x.Module.Code)
            .Distinct()
            .ToListAsync();

        _moduleAccents = codes
            .OrderBy(x => x, StringComparer.Ordinal)
            .Select((code, index) => (code, accent: ModuleAccents[index % ModuleAccents.Length]))
            .ToDictionary(x => x.code, x => x.accent);

        return _moduleAccents;
    }

    public async Task<List<Module>> GetStudentModules()
    {
        var currentSemster = Helper.GetCurrentSemester();
        return (
            await context
                .ModuleInstances.Where(x =>
                    x.Students.Contains(Student) && x.Semester == currentSemster
                )
                .Include(x => x.Module)
                .ToListAsync()
        )
            .Select(x => x.Module)
            .ToList();
    }

    public async Task<Student?> FindStudent(string studentId)
    {
        return await context
            .Students.Where(x => x.StudentId == studentId.ToLower().Trim())
            .Include(x => x.ModuleInstances)
            .ThenInclude(x => x.Module)
            .FirstOrDefaultAsync();
    }

    public async Task ActivateStudent(Student student)
    {
        var currentSemester = Helper.GetCurrentSemester();
        if (student.LastFetchedSemester != currentSemester)
        {
            var success = await FetchModulesForStudent(student);
            if (success)
            {
                await context.SaveChangesAsync();
            }
        }

        Student = student;
        await UpdateLastAccess(student.StudentId);
    }

    public async Task<(Student?, string)> GetOrCreateStudent(string studentId)
    {
        studentId = studentId.ToLower().Trim();

        try
        {
            //check existing student
            var student = await FindStudent(studentId);

            //if not already exists, initialize
            student ??= await InitializeStudentInternal(studentId);

            if (student != null)
            {
                await ActivateStudent(student);
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

    public async Task GenerateIncident(
        string description,
        Exception? exception = null,
        bool sendMail = true
    )
    {
        await using var db = await studueContextFactory.CreateDbContextAsync();

        logger.LogError(exception, "Incident occured: {0}", description);

        var incident = new Incident
        {
            StackTrace = exception?.ToString(),
            Description = description,
            DateTime = Helper.Now(),
            // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
            UserId = Student?.StudentId,
        };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();

        if (sendMail)
        {
            await SendMail(
                "bruhwiler.flurin@gmail.com",
                "Incident",
                JsonSerializer.Serialize(incident),
                null,
                []
            ); //avoid recursion
        }
    }

    public async Task<bool> SendMail(
        string recipient,
        string subject,
        string text,
        string? html,
        (HttpContent content, string name, string filename)[] additionalContents
    )
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
            content.Add(
                additionalContent.content,
                additionalContent.name,
                additionalContent.filename
            );
        }

        var request = new HttpRequestMessage
        {
            RequestUri = new Uri("https://api.eu.mailgun.net/v3/studue.ch/messages"),
            Method = HttpMethod.Post,
            Content = content,
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(
                        Encoding.UTF8.GetBytes($"api:{settings.Value.MailgunApiKey}")
                    )
                ),
            },
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

    private async Task<IHtmlDocument?> GetDocumentForDepartement(
        HttpClient client,
        string studentId,
        string departement,
        string semester
    )
    {
        var response = await client.SendAsync(
            new HttpRequestMessage
            {
                Content = new FormUrlEncodedContent(
                    [
                        new KeyValuePair<string, string>(
                            "ctl00$SelectionContent$txtSearch",
                            studentId
                        ),
                        new KeyValuePair<string, string>(
                            "ctl00$SelectionContent$selDepartment",
                            departement
                        ),
                        new KeyValuePair<string, string>(
                            "ctl00$SelectionContent$selPeriodVersion",
                            semester
                        ),
                        new KeyValuePair<string, string>(
                            "ctl00$SelectionContent$selWeek",
                            8.ToString()
                        ),
                    ]
                ), //todo don't hardcode the week!
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://stundenplan.zhaw.ch/"),
            }
        );

        var stream = await response.Content.ReadAsStringAsync();

        var parser = new HtmlParser();
        var document = parser.ParseDocument(stream);

        var searchHighlight = document.QuerySelector(".searchHighlight");

        if (searchHighlight == null) //this case we check, because when the studentId does not exist, we hit it
            return null;

        return document;
    }

    // The weekly grid only says when a lesson usually is; the export of the same search says
    // on which dates it really takes place. Without the export the entries keep the dates
    // they had, and the iCal feed falls back to every semester week for entries with none.
    private async Task AddOccurrencesFromExport(
        HttpClient client,
        Student student,
        List<ScheduleEntry> entries
    )
    {
        try
        {
            using var response = await client.GetAsync(
                "https://stundenplan.zhaw.ch/Default.aspx?ExpCal=1"
            );

            // without a search in the session, the export url just returns the start page
            if (
                !response.IsSuccessStatusCode
                || response.Content.Headers.ContentType?.MediaType != "text/calendar"
            )
            {
                logger.LogWarning(
                    "iCal export for {studentId} returned {status} {contentType}, keeping the old lesson dates",
                    student.StudentId,
                    response.StatusCode,
                    response.Content.Headers.ContentType?.MediaType
                );
                return;
            }

            var lessons = ZhawCalendarExport.Parse(await response.Content.ReadAsStringAsync());
            var unmatched = ZhawCalendarExport.AssignOccurrences(entries.Distinct().ToList(), lessons);

            if (unmatched.Count > 0)
            {
                // a module that has no lesson in the grid week we read
                logger.LogWarning(
                    "{count} exported lessons for {studentId} belong to no module in the grid: {modules}",
                    unmatched.Count,
                    student.StudentId,
                    string.Join(", ", unmatched.Select(x => x.ModuleCode).Distinct())
                );
            }
        }
        catch (Exception e)
            when (e is HttpRequestException or TaskCanceledException or FormatException)
        {
            logger.LogWarning(
                e,
                "Failed to fetch the iCal export for {studentId}, keeping the old lesson dates",
                student.StudentId
            );
        }
    }

    public async Task<bool> FetchModulesForStudent(Student student)
    {
        var semester = Helper.GetCurrentSemester();

        logger.LogInformation(
            "Fetching modules for {studentId} and {semester}",
            student.StudentId,
            semester
        );

        // stundenplan.zhaw.ch keeps the last search in the ASP.NET session, and its iCal
        // export is the export of exactly that search, so this fetch needs its own cookies
        using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
        using var client = new HttpClient(handler);

        var document = await GetDocumentForDepartement(client, student.StudentId, "T", semester);
        document ??= await GetDocumentForDepartement(client, student.StudentId, "A", semester);

        if (document == null)
        {
            logger.LogWarning(
                "Failed to fetch schedule for current semester for {studentId}",
                student.StudentId
            );
            return false;
        }

        student.LastFetchedSemester = semester;
        var searchHighlight = document.QuerySelector(".searchHighlight")!;

        student.Class = searchHighlight.NextSibling!.TextContent.Trim(',', ' ');

        var cellToColumnMapping = GetCellToColumnMapping(
            document.QuerySelector("table")!.FirstElementChild!
        );

        var allLessons = new List<ScheduleEntry>();
        foreach (var lessonElement in document.QuerySelectorAll(".left"))
        {
            // holidays ("Karfreitag", "Auffahrt ab 15:00h") use the same markup as lessons,
            // but have no teacher or room after them
            if (lessonElement.Closest(".schedHoliday") != null)
                continue;

            var lesson = new Lesson();

            lesson.ModuleCode = NormalizeModuleCode(lessonElement.TextContent);
            lesson.ModuleName = lessonElement.GetAttribute("title")!;
            lesson.Semester = semester;

            var title = lessonElement.ParentElement!.GetAttribute("title")!;
            lesson.LessonId = int.Parse(
                title.Substring(title.IndexOf("id: ", StringComparison.Ordinal) + 4)
            );

            var teacherElement = lessonElement.NextElementSibling!;
            lesson.TeacherName = RemoveShorthandFromTeacherName(
                teacherElement.GetAttribute("title")!
            );
            lesson.TeacherId = teacherElement.TextContent;

            var roomElement = teacherElement.NextElementSibling!;
            lesson.RoomCode = roomElement.TextContent;

            var tableDefinition = lessonElement.ParentElement!.ParentElement!.ParentElement!;
            lesson.WeekdayNumber = cellToColumnMapping[tableDefinition];
            lesson.Duration = int.Parse(tableDefinition.GetAttribute("rowspan")!);

            lesson.FirstLessonTime = tableDefinition.ParentElement!.FirstElementChild!.TextContent;

            var startTime = TimeOnly.Parse(lesson.FirstLessonTime.Split("-").First().Trim());
            var scheduleEntry = await context
                .ScheduleEntries.Include(x => x.Module)
                .FirstOrDefaultAsync(x =>
                    x.Module.Code == lesson.ModuleCode
                    && x.Semester == lesson.Semester
                    && x.ZhawID == lesson.LessonId
                    && x.Teacher == lesson.TeacherName
                    && x.Room == lesson.RoomCode
                    && x.Weekday == lesson.WeekdayNumber
                    && x.StartTime == startTime
                    && x.Duration == lesson.Duration
                );

            if (scheduleEntry == null)
            {
                scheduleEntry = new ScheduleEntry
                {
                    Room = lesson.RoomCode,
                    Semester = lesson.Semester,
                    Weekday = lesson.WeekdayNumber,
                    Teacher = lesson.TeacherName,
                    TeacherId = lesson.TeacherId,
                    ZhawID = lesson.LessonId,
                    Module = await GetOrCreateModule(lesson.ModuleCode, lesson.ModuleName ?? ""),
                    StartTime = startTime,
                    Duration = lesson.Duration,
                };
                context.ScheduleEntries.Add(scheduleEntry);
            }

            allLessons.Add(scheduleEntry);
        }

        await AddOccurrencesFromExport(client, student, allLessons);

        // a caller may have loaded the student without its modules; RemoveAll would then
        // clear nothing and every module would be linked a second time
        var modules = context.Entry(student).Collection(x => x.ModuleInstances);
        if (!modules.IsLoaded)
            await modules.LoadAsync();

        // Loaded before the module instances below are removed and re-added: Entry() detects
        // changes, and a detected removal is not undone by adding the instance back.
        var scheduleEntries = context.Entry(student).Collection(x => x.ScheduleEntries);
        if (!scheduleEntries.IsLoaded)
            await scheduleEntries.LoadAsync();

        // only the difference, for the same reason
        var ownLessons = allLessons.Distinct().ToList();
        student.ScheduleEntries.RemoveAll(x => x.Semester == semester && !ownLessons.Contains(x));
        student.ScheduleEntries.AddRange(
            ownLessons.Where(x => !student.ScheduleEntries.Contains(x)).ToList()
        );

        student.ModuleInstances.RemoveAll(x => x.Semester == semester);

        foreach (var x in allLessons.GroupBy(x => x.Module))
        {
            var moduleInstance = await GetOrCreateModuleInstance(x.Key, x.ToArray());
            if (!student.ModuleInstances.Contains(moduleInstance))
                student.ModuleInstances.Add(moduleInstance);
        }

        return true;

        async Task<ModuleInstance> GetOrCreateModuleInstance(
            Module module,
            ScheduleEntry[] scheduleEntries
        )
        {
            var moduleInstances = await context
                .ModuleInstances.Where(x => x.Module == module)
                .Include(moduleInstance => moduleInstance.ScheduleEntries)
                .ToListAsync();
            var moduleInstance = moduleInstances.FirstOrDefault(x =>
                x.ScheduleEntries.Any(y => scheduleEntries.Any(z => z.Id == y.Id))
            );
            if (moduleInstance == null)
            {
                moduleInstance = new ModuleInstance
                {
                    Module = module,
                    Semester = Helper.GetCurrentSemester(),
                    ScheduleEntries = scheduleEntries.ToList(),
                };

                context.ModuleInstances.Add(moduleInstance);
            }

            return moduleInstance;
        }

        async Task<Module> GetOrCreateModule(string moduleCode, string moduleName)
        {
            var module = context.Modules.Local.FirstOrDefault(x => x.Code == moduleCode);

            module ??= await context.Modules.FirstOrDefaultAsync(x => x.Code == moduleCode);

            if (module == null)
            {
                module = new Module { Code = moduleCode, Name = moduleName };
                context.Modules.Add(module);
            }

            return module;
        }
    }

    private async Task<Student?> InitializeStudentInternal(string studentId)
    {
        if (studentId.Length != 8)
            return null;

        logger.LogInformation("Initializing student {studentId}", studentId);

        var newStudent = new Student
        {
            WriteToken = GenerateWriteToken(),
            StudentId = studentId,
            Class = "unknown",
        };
        if (studentId == "bruehflu")
            newStudent.IsAdmin = true;

        context.Students.Add(newStudent);

        var success = await FetchModulesForStudent(newStudent);
        if (!success)
            return null;

        context.Events.Add(new Event
        {
            EventType = "StudentSignup",
            Message = $"{newStudent.StudentId} of class {newStudent.Class} signed up",
            DateTime = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        await SendMail(
            "bruhwiler.flurin@gmail.com",
            $"Studue signup: {studentId}",
            $"Initialized student {studentId}",
            null,
            []
        );

        return newStudent;
    }

    public async Task<List<ScheduleEntry>> GetScheduleEntriesForStudent(string studentId)
    {
        var currentSemester = Helper.GetCurrentSemester();

        var student = await context
            .Students.Where(x => x.StudentId == studentId)
            .Include(x => x.ScheduleEntries)
            .ThenInclude(x => x.Module)
            .Include(x => x.ModuleInstances)
            .ThenInclude(x => x.ScheduleEntries)
            .ThenInclude(x => x.Module)
            .FirstAsync();

        return ScheduleEntriesOf(student, currentSemester);
    }

    // Needs Student.ScheduleEntries and ModuleInstances.ScheduleEntries loaded, both with
    // their Module. A student whose schedule has not been fetched since the student's own
    // lessons are stored falls back to the lessons of their module instances, which may be
    // another class's; the nightly refresh replaces that with the student's own.
    public static List<ScheduleEntry> ScheduleEntriesOf(Student student, string semester)
    {
        var own = student.ScheduleEntries.Where(x => x.Semester == semester).ToList();
        if (own.Count > 0)
            return own;

        return student
            .ModuleInstances.Where(x => x.Semester == semester)
            .SelectMany(x => x.ScheduleEntries)
            .ToList();
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

            for (var i = column; i < columnSpans.Length; i++)
            {
                if (columnSpans[i] > 0)
                {
                    columnSpans[i]--;
                }
            }
        }

        return result;
    }

    internal static string NormalizeModuleCode(string moduleCode)
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
        public string TeacherId = null!;
        public string RoomCode = null!;
        public int WeekdayNumber;
        public string FirstLessonTime = null!;
        public int Duration;
    }
}
