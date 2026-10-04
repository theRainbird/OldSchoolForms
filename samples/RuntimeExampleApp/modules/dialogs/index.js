// Dialoge-Modul (Klassenform): alle Messagebox-Kombinationen durchprobieren.
export default class DialogsForm extends FormBase {
    constructor(ctx) {
        super(ctx);
        this.loadLayout(); // lädt layout.json und füllt this.controls
        this.controls.btnOk.on("click", () => this.show("Nur eine Bestätigung.", "ok", "info"));
        this.controls.btnOkCancel.on("click", () => this.show("OK oder Abbrechen?", "okcancel", "question"));
        this.controls.btnYesNo.on("click", () => this.show("Ja oder Nein?", "yesno", "question"));
        this.controls.btnYesNoCancel.on("click", () => this.show("Ja, Nein oder Abbrechen?", "yesnocancel", "warning"));
        console.log("Dialoge initialisiert");
    }

    show(message, buttons, icon) {
        const result = this.host.showMessageBox(message, "Dialog-Beispiel", buttons, icon);
        this.controls.resultLabel.text = "Ergebnis: " + result;
        this.setStatus("Dialog-Ergebnis: " + result);
    }

    activate() {
        this.setStatus("Dialoge aktiv");
    }

    deactivate() {
        this.setStatus("Dialoge verlassen");
    }
}
