// Entry-Skript der Sample-App.
// ctx = { form, host, app }

export function init(ctx) {
    console.log("Sample-App initialisiert: " + ctx.app.name + " v" + ctx.app.version);
    ctx.host.setStatus("Willkommen bei " + ctx.app.name);

    // Erstes Modul direkt öffnen, damit Content sichtbar ist.
    ctx.host.openModule("customers");
}
