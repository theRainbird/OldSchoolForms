// Dashboard-Modul (Klassenform): Live-Uhr via setInterval, Navigation zu anderen Modulen.
import { formatClock, formatDate } from "@app/shared/util.js";

export default class DashboardForm extends FormBase {
    constructor(ctx) {
        super(ctx);
        this.loadLayout(); // lädt layout.json und füllt this.controls
        this.timerId = 0;
        this.controls.openCounter.on("click", () => this.openModule("counter"));
        this.controls.openNotes.on("click", () => this.openModule("notes"));
        this.controls.openDialogs.on("click", () => this.openModule("dialogs"));
        this.tick();
        console.log("Dashboard initialisiert");
    }

    tick() {
        const now = new Date();
        this.controls.clockLabel.text = formatClock(now);
        this.controls.dateLabel.text = formatDate(now);
    }

    activate() {
        if (this.timerId) clearInterval(this.timerId);
        this.timerId = setInterval(() => this.tick(), 1000);
        this.setStatus("Dashboard aktiv");
    }

    deactivate() {
        if (this.timerId) { clearInterval(this.timerId); this.timerId = 0; }
        this.setStatus("Dashboard verlassen");
    }

    dispose() {
        if (this.timerId) { clearInterval(this.timerId); this.timerId = 0; }
    }
}
