// Einstiegs-Skript der Beispiel-App.
// ctx = { form, host, app }

export function init(ctx) {
    console.log("Beispiel-App initialisiert: " + ctx.app.name + " v" + ctx.app.version);
    ctx.host.setStatus("Willkommen bei " + ctx.app.name);

    // Startseite direkt öffnen, damit beim Start Content sichtbar ist.
    ctx.host.openModule("dashboard");
}
