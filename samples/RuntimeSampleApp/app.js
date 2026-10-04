// Entry-Skript der Sample-App (Klassenform).
// ctx = { form, host, app }

export default class SampleApp extends FormBase {
    constructor(ctx) {
        super(ctx);
        console.log("Sample-App initialisiert: " + this.app.name + " v" + this.app.version);
        this.setStatus("Willkommen bei " + this.app.name);

        // Erstes Modul direkt öffnen, damit Content sichtbar ist.
        this.openModule("customers");
    }
}
