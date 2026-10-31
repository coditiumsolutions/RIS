namespace ResidentPortal.Models;

public class VisitorPhoto
{
    public int Uid { get; set; }

    public int VisitorRequestId { get; set; }

    /// <summary>CheckIn or CheckOut.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Server-generated relative filename (GUID + extension). Never a client-supplied name.</summary>
    public string FilePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public int SizeBytes { get; set; }

    public int? CapturedByReceptionistId { get; set; }

    public int? CapturedByAdminId { get; set; }

    public DateTime CapturedAtUtc { get; set; }

    public VisitorRequest VisitorRequest { get; set; } = null!;
}

public static class VisitorPhotoKind
{
    public const string CheckIn = "CheckIn";
    public const string CheckOut = "CheckOut";
}
