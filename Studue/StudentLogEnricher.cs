using Serilog.Core;
using Serilog.Events;

namespace Studue;

public class StudentLogEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    private const string PropertyName = "StudentId";
    private const string ParameterName = "student_id";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var request = httpContextAccessor.HttpContext?.Request;
        if (request == null)
            return;

        var studentId = request.Query.TryGetValue(ParameterName, out var queryValue) && queryValue is [{ } fromQuery]
            ? fromQuery
            : request.Cookies[ParameterName];

        if (studentId is not { Length: > 0 and <= 16 } || !studentId.All(char.IsLetterOrDigit))
            return;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(PropertyName, studentId));
    }
}
