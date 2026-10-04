// Einstiegs-Skript der Beispiel-App (Klassenform).
// ctx = { form, host, app }

export default class BeispielApp extends FormBase {
    constructor(ctx) {
        super(ctx);
        console.log("Beispiel-App initialisiert: " + this.app.name + " v" + this.app.version);
        this.setStatus("Willkommen bei " + this.app.name);

        // Startseite direkt öffnen, damit beim Start Content sichtbar ist.
        this.openModule("dashboard");
    }
}
