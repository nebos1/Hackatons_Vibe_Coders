using EventsApp.ViewModels.Tickets;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EventsApp.Services
{
    public interface ITicketDocumentService
    {
        byte[] GenerateQrPng(string payload);
        byte[] GenerateTicketPdf(UserTicketDetailsViewModel ticket);
    }

    public class TicketDocumentService : ITicketDocumentService
    {
        static TicketDocumentService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateQrPng(string payload)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            using var png = new PngByteQRCode(data);
            return png.GetGraphic(20);
        }

        public byte[] GenerateTicketPdf(UserTicketDetailsViewModel ticket)
        {
            var qrPng = GenerateQrPng(ticket.QrCode);
            var statusText = ticket.IsUsed ? "USED" : "VALID";
            var statusColor = ticket.IsUsed ? Colors.Red.Medium : Colors.Green.Medium;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.DefaultTextStyle(t => t.FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("GrooveOn").FontSize(28).Bold().FontColor(Colors.Indigo.Medium);
                        col.Item().Text("Event Ticket").FontSize(14).FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    });

                    page.Content().PaddingVertical(15).Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Spacing(6);
                            col.Item().Text(ticket.EventTitle).FontSize(18).Bold();
                            col.Item().Text($"Ticket: {ticket.TicketName}").FontSize(13);
                            col.Item().Text($"Location: {ticket.Address}, {ticket.City}");
                            col.Item().Text($"Starts: {ticket.StartTime:yyyy-MM-dd HH:mm}");
                            col.Item().Text($"Price: {ticket.Price:0.00}");
                            col.Item().Text($"Holder: {ticket.OwnerUserName}");
                            col.Item().Text($"Email: {ticket.OwnerEmail}");
                            col.Item().Text($"Transaction: {ticket.TransactionStatus}");
                            col.Item().PaddingTop(6).Text(text =>
                            {
                                text.Span("Status: ").Bold();
                                text.Span(statusText).FontColor(statusColor).Bold();
                            });
                            if (ticket.IsUsed && ticket.UsedAt.HasValue)
                            {
                                col.Item().Text($"Used at: {ticket.UsedAt:yyyy-MM-dd HH:mm}").FontColor(Colors.Grey.Darken1);
                            }
                        });

                        row.ConstantItem(180).AlignRight().Column(col =>
                        {
                            col.Item().Image(qrPng).FitWidth();
                            col.Item().PaddingTop(4).AlignCenter()
                                .Text(ticket.QrCode).FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Demo purchase — payment integration coming soon. ").FontColor(Colors.Grey.Darken1).FontSize(9);
                        text.Span($"Ticket ID: {ticket.Id}").FontSize(9);
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
