using System.ComponentModel.DataAnnotations;
using EventsApp.Models;

namespace EventsApp.ViewModels.Layouts
{
    public class VenueLayoutListViewModel
    {
        public IReadOnlyList<VenueLayoutRowViewModel> Layouts { get; set; } = Array.Empty<VenueLayoutRowViewModel>();
    }

    public class VenueLayoutRowViewModel
    {
        public int Id { get; set; }
        public string VenueName { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int Version { get; set; }
        public VenueLayoutStatus Status { get; set; }
        public int SectionsCount { get; set; }
        public int SeatsCount { get; set; }
        public bool HasSoldSeats { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class VenueLayoutEditorViewModel
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(160)]
        [Display(Name = "Име на зала / място")]
        public string VenueName { get; set; } = null!;

        [Required]
        [StringLength(160)]
        [Display(Name = "Име на layout")]
        public string Name { get; set; } = null!;

        public int Version { get; set; } = 1;

        public VenueLayoutStatus Status { get; set; } = VenueLayoutStatus.Active;

        public bool IsInUseWithSoldSeats { get; set; }

        public string LayoutJson { get; set; } = "{\"sections\":[]}";
    }

    public class VenueLayoutJsonModel
    {
        public double CanvasWidth { get; set; } = 1200;

        public double CanvasHeight { get; set; } = 760;

        public List<LayoutFloorJsonModel> Floors { get; set; } = new();

        public List<LayoutSectionJsonModel> Sections { get; set; } = new();
    }

    public class LayoutFloorJsonModel
    {
        public string ClientId { get; set; } = "floor-1";

        public string Name { get; set; } = "Floor 1";
    }

    public class LayoutSectionJsonModel
    {
        public int? Id { get; set; }
        public string? ClientId { get; set; }
        public string Name { get; set; } = "Секция";
        public string FloorId { get; set; } = "floor-1";

        public string FloorName { get; set; } = "Floor 1";

        public LayoutSectionType Type { get; set; } = LayoutSectionType.Seated;

        public string Shape { get; set; } = "Rectangle";
        public int Capacity { get; set; }
        public decimal PriceModifier { get; set; }
        public string ColorHex { get; set; } = "#2456ff";
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 220;
        public double Height { get; set; } = 140;

        public double Rotation { get; set; }
        public List<SeatJsonModel> Seats { get; set; } = new();
    }

    public class SeatJsonModel
    {
        public int? Id { get; set; }

        public string? ClientId { get; set; }

        public string Row { get; set; } = "A";

        public string Number { get; set; } = "1";

        public string? Label { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public double Radius { get; set; } = 16;

        public double Rotation { get; set; }

        public int Capacity { get; set; } = 1;

        public bool IsCapacityUnlimited { get; set; }

        public SeatType SeatType { get; set; } = SeatType.Standard;
        public LayoutSeatStatus Status { get; set; } = LayoutSeatStatus.Active;
    }

    public class EventSeatMapViewModel
    {
        public int LayoutId { get; set; }
        public string LayoutName { get; set; } = null!;
        public IReadOnlyList<string> Floors { get; set; } = Array.Empty<string>();
        public IReadOnlyList<EventSeatSectionViewModel> Sections { get; set; } = Array.Empty<EventSeatSectionViewModel>();
    }

    public class EventSeatSectionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string FloorName { get; set; } = "Floor 1";
        public LayoutSectionType Type { get; set; }
        public string Shape { get; set; } = "Rectangle";
        public decimal PriceModifier { get; set; }
        public string ColorHex { get; set; } = "#2456ff";
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }
        public IReadOnlyList<EventSeatViewModel> Seats { get; set; } = Array.Empty<EventSeatViewModel>();
    }

    public class EventSeatViewModel
    {
        public int Id { get; set; }
        public int? InventoryId { get; set; }
        public string Label { get; set; } = null!;
        public string Row { get; set; } = null!;
        public string Number { get; set; } = null!;
        public double X { get; set; }
        public double Y { get; set; }
        public double Radius { get; set; } = 16;
        public double Rotation { get; set; }
        public int Capacity { get; set; } = 1;
        public bool IsCapacityUnlimited { get; set; }
        public SeatType SeatType { get; set; }
        public EventSeatInventoryStatus Status { get; set; }
        public bool Selectable => Status == EventSeatInventoryStatus.Available;
    }
}
