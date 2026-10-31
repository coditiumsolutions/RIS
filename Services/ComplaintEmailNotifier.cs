using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Models;

namespace ResidentPortal.Services;

public class ComplaintEmailNotifier
{
    private readonly ApplicationDbContext _db;
    private readonly SmtpSettingsService _smtp;
    private readonly ILogger<ComplaintEmailNotifier> _logger;

    public ComplaintEmailNotifier(
        ApplicationDbContext db,
        SmtpSettingsService smtp,
        ILogger<ComplaintEmailNotifier> logger)
    {
        _db = db;
        _smtp = smtp;
        _logger = logger;
    }

    public async Task NotifyStatusChangeAsync(int complaintId, CancellationToken ct)
    {
        try
        {
            var settings = await _smtp.GetOrCreateAsync(ct);
            if (!settings.EmailNotificationsEnabled
                || string.IsNullOrWhiteSpace(settings.Host)
                || string.IsNullOrWhiteSpace(settings.FromEmail))
                return;

            var complaint = await _db.Complaints
                .AsNoTracking()
                .Include(c => c.Resident)
                .Include(c => c.Category)
                .SingleOrDefaultAsync(c => c.Uid == complaintId, ct);

            if (complaint?.Resident is null || string.IsNullOrWhiteSpace(complaint.Resident.Email))
                return;

            if (complaint.Status is ComplaintStatus.Initiated or ComplaintStatus.Cancelled)
                return;

            var subject = $"Complaint #{complaint.Uid} update: {ComplaintStatus.Display(complaint.Status)}";
            var body = BuildBody(complaint);
            await SendAsync(settings, complaint.Resident.Email!, subject, body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send complaint email for {ComplaintId}.", complaintId);
        }
    }

    public async Task<(bool Ok, string Message)> SendTestAsync(string toEmail, CancellationToken ct)
    {
        try
        {
            var settings = await _smtp.GetOrCreateAsync(ct);
            if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.FromEmail))
                return (false, "Configure SMTP host and From email first.");

            if (string.IsNullOrWhiteSpace(toEmail))
                return (false, "Enter a destination email for the test.");

            await SendAsync(settings, toEmail.Trim(), "RIS SMTP test",
                "This is a test email from the Resident Portal Management System.", ct);
            return (true, "Test email sent.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP test failed.");
            return (false, "Test email failed. Check SMTP settings and logs.");
        }
    }

    private async Task SendAsync(SmtpSettings settings, string to, string subject, string body, CancellationToken ct)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromEmail!, string.IsNullOrWhiteSpace(settings.FromName) ? settings.FromEmail : settings.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(to);

        using var client = new SmtpClient(settings.Host!, settings.Port)
        {
            EnableSsl = settings.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        var password = _smtp.UnprotectPassword(settings);
        if (!string.IsNullOrWhiteSpace(settings.Username))
            client.Credentials = new NetworkCredential(settings.Username, password ?? string.Empty);

        // SmtpClient has no CancellationToken overload on older targets; honor cancel before send.
        ct.ThrowIfCancellationRequested();
        await client.SendMailAsync(message);
    }

    private static string BuildBody(Complaint complaint)
    {
        var lines = new List<string>
        {
            $"Hello {complaint.Resident.ResidentName},",
            "",
            $"Your complaint #{complaint.Uid} ({complaint.Category.Name}) is now: {ComplaintStatus.Display(complaint.Status)}.",
            $"Priority: {complaint.Priority}."
        };

        if (complaint.Status == ComplaintStatus.Declined && !string.IsNullOrWhiteSpace(complaint.DeclineReason))
            lines.Add($"Reason: {complaint.DeclineReason}");
        if (!string.IsNullOrWhiteSpace(complaint.WorkerName) &&
            complaint.Status is ComplaintStatus.Assigned or ComplaintStatus.WorkInProgress)
            lines.Add($"Assigned worker: {complaint.WorkerName}.");

        lines.Add("");
        lines.Add("You can view details in the Resident Portal.");
        lines.Add("— RIS");
        return string.Join(Environment.NewLine, lines);
    }
}
