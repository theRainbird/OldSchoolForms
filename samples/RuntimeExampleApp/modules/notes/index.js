// Notizen-Modul: Speichern/Laden über die Host-Sandbox.
let ctxRef = null;
const FILE = "notizen.txt";

export function init(ctx) {
    ctxRef = ctx;

    ctx.controls.btnSave.on("click", () => {
        ctx.host.writeText(FILE, ctx.controls.memo.text);
        ctx.controls.noteStatus.text = "Gespeichert.";
        ctx.host.setStatus("Notiz gespeichert");
    });

    ctx.controls.btnLoad.on("click", () => {
        const content = ctx.host.readSandboxText(FILE);
        if (content === null || content === undefined) {
            ctx.controls.noteStatus.text = "Keine gespeicherte Notiz.";
            ctx.host.setStatus("Keine Notiz gefunden");
        } else {
            ctx.controls.memo.text = content;
            ctx.controls.noteStatus.text = "Geladen.";
            ctx.host.setStatus("Notiz geladen");
        }
    });

    console.log("Notizen initialisiert");
}

export function activate() {
    if (ctxRef) ctxRef.host.setStatus("Notizen aktiv");
}

export function deactivate() {
    if (ctxRef) ctxRef.host.setStatus("Notizen verlassen");
}

export function dispose() {
    ctxRef = null;
}
