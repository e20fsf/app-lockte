namespace Lockte.SharedKernel.Persistence;

/// <summary>
/// แบบแผนกลางของทุกตาราง: สร้างโดย/เมื่อ + แก้ล่าสุดโดย/เมื่อ (เติมโดย <see cref="AuditInterceptor"/>)
/// และ rowversion สำหรับตรวจการแก้ชนกัน (ตั้งโดย <see cref="ModelBuilderExtensions.ApplyLockteConventions"/>).
/// Every audited row also names its <see cref="IHasBusinessKey.BusinessKey"/>, so its history — including the
/// "เพิ่ม" row, saved before the database assigns an id — is found by รหัสรายการ.
/// </summary>
public interface IAudited : IHasBusinessKey
{
    string CreatedBy { get; set; }
    DateTimeOffset CreatedAt { get; set; }
    string UpdatedBy { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
    byte[] RowVersion { get; set; }
}
