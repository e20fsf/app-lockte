namespace Lockte.SharedKernel.Persistence;

/// <summary>
/// Whether the user acting may see ข้อมูลค่าจ้าง (#277: ค่าแรง เงินเดือน OT โบนัส เงินสงเคราะห์ เลขบัญชี).
/// Services that return or save such fields ask this, so the rule is kept in one place and a screen that
/// forgets to hide a field still never receives the value.
/// </summary>
public interface IPayAccess
{
    bool CanViewPay { get; }
}

/// <summary>Work outside a signed-in user (migration tool, reset-admin) sees everything.</summary>
public sealed class OpenPayAccess : IPayAccess
{
    public bool CanViewPay => true;
}
