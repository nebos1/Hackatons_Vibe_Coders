namespace EventsApp.Services
{
    public class EmailSettings
    {
        public const string SectionName = "Email";

        public string? FromName { get; set; }

        public string? FromAddress { get; set; }

        public SmtpSettings Smtp { get; set; } = new();
    }

    public class SmtpSettings
    {
        public string? Host { get; set; }

        public int Port { get; set; } = 587;

        public bool EnableSsl { get; set; } = true;

        public string? UserName { get; set; }

        public string? Password { get; set; }
    }
}