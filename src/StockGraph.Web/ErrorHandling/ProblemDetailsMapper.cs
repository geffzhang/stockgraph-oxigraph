namespace StockGraph.Web.ErrorHandling;

public static class ProblemDetailsMapper
{
    public static (int status, string detail) MapException(Exception ex) => ex switch
    {
        ArgumentException ae => (400, ae.Message),
        InvalidOperationException ioe when ioe.Message.Contains("locked") => (409, "Storage is currently locked by another build operation"),
        InvalidOperationException ioe when ioe.Message.Contains("not opened") => (503, "Storage is not yet available"),
        OperationCanceledException => (499, "The request was cancelled by the client"),
        _ => (500, "An unexpected error occurred"),
    };
}
