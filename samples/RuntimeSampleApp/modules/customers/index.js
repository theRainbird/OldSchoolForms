// Kunden-Modul.
// ctx = { form, controls, host, tab, app }
import { greet, formatCurrency } from "@app/shared/util.js";

let ctxRef = null;

export function init(ctx) {
    ctxRef = ctx;
    const c = ctx.controls;

    c.addButton.on("click", () => {
        const result = ctx.host.showMessageBox(
            "Neuen Kunden anlegen?",
            "Kunden",
            "yesno",
            "question"
        );
        ctx.host.setStatus("Dialog-Ergebnis: " + result);
    });

    c.searchBox.on("textchanged", (e) => {
        ctx.host.setStatus("Suche nach: " + e.target.text);
    });

    console.log(greet("Kunden-Modul"));
}

export function activate() {
    if (ctxRef) ctxRef.host.setStatus("Kunden-Modul aktiv");
}

export function deactivate() {
    if (ctxRef) ctxRef.host.setStatus("Kunden-Modul verlassen");
}

export function dispose() {
    console.log("Kunden-Modul disposed");
    ctxRef = null;
}
