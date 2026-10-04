// Zähler-Modul (Klassenform): Plus/Minus/Reset mit einstellbarer Schrittweite.
export default class CounterForm extends FormBase {
    constructor(ctx) {
        super(ctx);
        this.loadLayout(); // lädt layout.json und füllt this.controls
        this.count = 0;
        this.controls.btnPlus.on("click", () => { this.count += this.stepValue(); this.render(); });
        this.controls.btnMinus.on("click", () => { this.count -= this.stepValue(); this.render(); });
        this.controls.btnReset.on("click", () => {
            this.count = 0;
            this.render();
            this.setStatus("Zähler zurückgesetzt");
        });
        this.render();
        console.log("Zähler initialisiert");
    }

    stepValue() {
        const raw = parseInt(this.controls.stepBox.text, 10);
        return isNaN(raw) || raw === 0 ? 1 : raw;
    }

    render() {
        this.controls.countLabel.text = String(this.count);
    }

    activate() {
        this.setStatus("Zähler aktiv");
    }

    deactivate() {
        this.setStatus("Zähler verlassen");
    }
}
