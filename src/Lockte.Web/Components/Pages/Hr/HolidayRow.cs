namespace Lockte.Web.Components.Pages.Hr;

/// <summary>
/// One row of SCR-15 / SCR-16 as edited in place: วันที่ and the text column (ป้าย / ชื่อวันหยุด). <see cref="Id"/> null =
/// the blank new row. Remembers what was loaded so a rejected edit can be put back.
/// </summary>
public sealed class HolidayRow
{
    public HolidayRow() { }

    public HolidayRow(int id, DateOnly date, string? text, byte[] rowVersion)
    {
        Id = id;
        Date = loadedDate = date.ToDateTime(TimeOnly.MinValue);
        Text = loadedText = text;
        RowVersion = rowVersion;
    }

    private readonly DateTime? loadedDate;
    private readonly string? loadedText;

    public int? Id { get; }
    public DateTime? Date { get; set; }
    public string? Text { get; set; }
    public byte[]? RowVersion { get; }

    public DateOnly? DateOnly => Date is { } d ? System.DateOnly.FromDateTime(d) : null;

    public void Undo()
    {
        if (Id is null) return; // a new row keeps what was typed, to be fixed
        Date = loadedDate;
        Text = loadedText;
    }
}
