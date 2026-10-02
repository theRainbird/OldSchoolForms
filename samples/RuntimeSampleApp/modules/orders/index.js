// Aufträge-Modul.
import { formatCurrency } from "@app/shared/util.js";

let ctxRef = null;
let timerId = 0;

export function init(ctx) {
    ctxRef = ctx;
    const c = ctx.controls;

    c.refreshButton.on("click", () => {
        ctx.host.setStatus("Aufträge werden aktualisiert...");
        let value = 0;
        timerId = setInterval(() => {
            value += 10;
            if (value >= 100) {
                clearInterval(timerId);
                timerId = 0;
                ctx.host.setStatus("Aktualisierung fertig. Summe: " + formatCurrency(1234.5));
            }
        }, 100);
    });
}

export function activate() {
    if (ctxRef) ctxRef.host.setStatus("Aufträge-Modul aktiv");
}

export function deactivate() {
    if (timerId) { clearInterval(timerId); timerId = 0; }
}

export function dispose() {
    if (timerId) { clearInterval(timerId); timerId = 0; }
    ctxRef = null;
}
