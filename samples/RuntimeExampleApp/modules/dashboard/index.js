// Dashboard module (class form): live clock via setInterval, navigation to other modules.
import { formatClock, formatDate } from "@app/shared/util.js";

export default class DashboardForm extends FormBase {
    constructor(ctx) {
        super(ctx);
        this.loadLayout(); // loads layout.json and fills this.controls
        this.timerId = 0;
        this.controls.openCounter.on("click", () => this.openModule("counter"));
        this.controls.openNotes.on("click", () => this.openModule("notes"));
        this.controls.openDialogs.on("click", () => this.openModule("dialogs"));
        this.tick();
        console.log("Dashboard initialized");
    }

    tick() {
        const now = new Date();
        this.controls.clockLabel.text = formatClock(now);
        this.controls.dateLabel.text = formatDate(now);
    }

    activate() {
        if (this.timerId) clearInterval(this.timerId);
        this.timerId = setInterval(() => this.tick(), 1000);
        this.setStatus(this.t("Dashboard_Active"));
    }

    deactivate() {
        if (this.timerId) { clearInterval(this.timerId); this.timerId = 0; }
        this.setStatus(this.t("Dashboard_Left"));
    }

    dispose() {
        if (this.timerId) { clearInterval(this.timerId); this.timerId = 0; }
    }
}
