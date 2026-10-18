// ไม่ใช้งานเกิน Auth:IdleTimeoutMinutes → กลับหน้า login (#39).
// Blazor Server sends no HTTP requests while the user works, so the login cookie would expire mid-work:
// while there is activity, ping /Account/KeepAlive (at most once a minute) to renew it.
// The last activity is shared through localStorage so an idle tab does not log out a busy one.
(() => {
    const script = document.currentScript;
    const timeoutMs = Number(script.dataset.timeoutMinutes || 30) * 60_000;
    const key = "lockte.lastActivity";
    const pingEveryMs = 60_000;
    let lastPing = Date.now();

    const now = () => Date.now();
    const lastActivity = () => Number(localStorage.getItem(key)) || now();
    const markActive = () => localStorage.setItem(key, String(now()));
    markActive();

    for (const type of ["mousedown", "keydown", "wheel", "touchstart"])
        document.addEventListener(type, markActive, { passive: true, capture: true });

    setInterval(async () => {
        const idleFor = now() - lastActivity();
        if (idleFor >= timeoutMs) {
            location.href = "Account/Login?reason=idle";
            return;
        }
        if (idleFor < pingEveryMs && now() - lastPing >= pingEveryMs) {
            lastPing = now();
            const response = await fetch("Account/KeepAlive", { method: "POST", credentials: "same-origin" });
            // Not signed in any more (expired, deactivated): the cookie handler answers with a redirect to login.
            if (!response.ok || response.redirected) location.href = "Account/Login";
        }
    }, 15_000);
})();
