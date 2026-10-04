// Notizen-Modul (Klassenform): Speichern/Laden über die Host-Sandbox.
export default class NotesForm extends FormBase {
    constructor(ctx) {
        super(ctx);
        this.loadLayout(); // lädt layout.json und füllt this.controls
        this.file = "notizen.txt";

        this.controls.btnSave.on("click", () => {
            this.host.writeText(this.file, this.controls.memo.text);
            this.controls.noteStatus.text = "Gespeichert.";
            this.setStatus("Notiz gespeichert");
        });

        this.controls.btnLoad.on("click", () => {
            const content = this.host.readSandboxText(this.file);
            if (content === null || content === undefined) {
                this.controls.noteStatus.text = "Keine gespeicherte Notiz.";
                this.setStatus("Keine Notiz gefunden");
            } else {
                this.controls.memo.text = content;
                this.controls.noteStatus.text = "Geladen.";
                this.setStatus("Notiz geladen");
            }
        });

        console.log("Notizen initialisiert");
    }

    activate() {
        this.setStatus("Notizen aktiv");
    }

    deactivate() {
        this.setStatus("Notizen verlassen");
    }
}
