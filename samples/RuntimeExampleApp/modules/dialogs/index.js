// Dialoge-Modul: alle Messagebox-Kombinationen durchprobieren.
let ctxRef = null;

function show(message, buttons, icon) {
    if (!ctxRef) return;
    const result = ctxRef.host.showMessageBox(message, "Dialog-Beispiel", buttons, icon);
    ctxRef.controls.resultLabel.text = "Ergebnis: " + result;
    ctxRef.host.setStatus("Dialog-Ergebnis: " + result);
}

export function init(ctx) {
    ctxRef = ctx;
    ctx.controls.btnOk.on("click", () => show("Nur eine Bestätigung.", "ok", "info"));
    ctx.controls.btnOkCancel.on("click", () => show("OK oder Abbrechen?", "okcancel", "question"));
    ctx.controls.btnYesNo.on("click", () => show("Ja oder Nein?", "yesno", "question"));
    ctx.controls.btnYesNoCancel.on("click", () => show("Ja, Nein oder Abbrechen?", "yesnocancel", "warning"));
    console.log("Dialoge initialisiert");
}

export function activate() {
    if (ctxRef) ctxRef.host.setStatus("Dialoge aktiv");
}

export function deactivate() {
    if (ctxRef) ctxRef.host.setStatus("Dialoge verlassen");
}

export function dispose() {
    ctxRef = null;
}
