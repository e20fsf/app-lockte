namespace Lockte.SharedKernel.Persistence;

/// <summary>
/// แบบแผนกลางของทุกตาราง: สร้างโดย/เมื่อ + แก้ล่าสุดโดย/เมื่อ (เติมโดย <see cref="AuditStampInterceptor"/>)
/// และ rowversion สำหรับตรวจการแก้ชนกัน (ตั้งโดย <see cref="ModelBuilderExtensions.ApplyLockteConventions"/>).
/// </summary>
public interface IAudited
{
    string CreatedBy { get; set; }
    DateTimeOffset CreatedAt { get; set; }
    string UpdatedBy { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
    byte[] RowVersion { get; set; }
}
