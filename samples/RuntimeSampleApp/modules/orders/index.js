// Aufträge-Modul (Klassenform).
import { formatCurrency } from "@app/shared/util.js";

export default class AuftragsForm extends FormBase {
    constructor(ctx) {
        super(ctx);
        this.loadLayout(); // lädt layout.json und füllt this.controls
        this.timerId = 0;

        this.controls.refreshButton.on("click", () => {
            this.setStatus("Aufträge werden aktualisiert...");
            let value = 0;
            this.timerId = setInterval(() => {
                value += 10;
                if (value >= 100) {
                    clearInterval(this.timerId);
                    this.timerId = 0;
                    this.setStatus("Aktualisierung fertig. Summe: " + formatCurrency(1234.5));
                }
            }, 100);
        });
    }

    activate() {
        this.setStatus("Aufträge-Modul aktiv");
    }

    deactivate() {
        if (this.timerId) { clearInterval(this.timerId); this.timerId = 0; }
    }

    dispose() {
        if (this.timerId) { clearInterval(this.timerId); this.timerId = 0; }
    }
}
