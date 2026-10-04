// Kunden-Modul (Klassenform).
// ctx = { form, controls, host, tab, app }
import { greet, formatCurrency } from "@app/shared/util.js";

export default class KundenForm extends FormBase {
    constructor(ctx) {
        super(ctx);
        this.loadLayout(); // lädt layout.json und füllt this.controls

        this.controls.addButton.on("click", () => {
            const result = this.host.showMessageBox(
                "Neuen Kunden anlegen?",
                "Kunden",
                "yesno",
                "question"
            );
            this.setStatus("Dialog-Ergebnis: " + result);
        });

        this.controls.searchBox.on("textchanged", (e) => {
            this.setStatus("Suche nach: " + e.target.text);
        });

        console.log(greet("Kunden-Modul"));
    }

    activate() {
        this.setStatus("Kunden-Modul aktiv");
    }

    deactivate() {
        this.setStatus("Kunden-Modul verlassen");
    }

    dispose() {
        console.log("Kunden-Modul disposed");
    }
}
