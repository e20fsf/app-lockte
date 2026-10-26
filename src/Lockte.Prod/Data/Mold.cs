using Lockte.SharedKernel.Persistence;

namespace Lockte.Prod.Data;

/// <summary>
/// แม่พิมพ์ (T-รหัสพิมพ์) — for now only รหัส + ชื่อ, the first table of the module so the schema, migrations and history
/// are in place (#103). The แม่พิมพ์ ticket (#104) adds the rest of SCR-17.
/// Internal to the module (ADR 0002): other modules see <see cref="MoldDto"/> from <see cref="MoldDirectory"/>.
/// </summary>
internal sealed class Mold : IAudited
{
    public int Id { get; set; }
    /// <summary>รหัสแม่พิมพ์ เช่น <c>BF-029-1/1</c> (business key, unique, upper case).</summary>
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Code;
}
