// Dashboard-Modul: Live-Uhr via setInterval, Navigation zu anderen Modulen.
import { formatClock, formatDate } from "@app/shared/util.js";

let ctxRef = null;
let timerId = 0;

function tick() {
    if (!ctxRef) return;
    const now = new Date();
    ctxRef.controls.clockLabel.text = formatClock(now);
    ctxRef.controls.dateLabel.text = formatDate(now);
}

export function init(ctx) {
    ctxRef = ctx;
    ctx.controls.openCounter.on("click", () => ctx.host.openModule("counter"));
    ctx.controls.openNotes.on("click", () => ctx.host.openModule("notes"));
    ctx.controls.openDialogs.on("click", () => ctx.host.openModule("dialogs"));
    tick();
    console.log("Dashboard initialisiert");
}

export function activate() {
    if (timerId) clearInterval(timerId);
    timerId = setInterval(tick, 1000);
    if (ctxRef) ctxRef.host.setStatus("Dashboard aktiv");
}

export function deactivate() {
    if (timerId) { clearInterval(timerId); timerId = 0; }
    if (ctxRef) ctxRef.host.setStatus("Dashboard verlassen");
}

export function dispose() {
    if (timerId) { clearInterval(timerId); timerId = 0; }
    ctxRef = null;
}
