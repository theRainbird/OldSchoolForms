// Counter module (class form): plus/minus/reset with configurable step size.
export default class CounterForm extends FormBase {
    constructor(ctx) {
        super(ctx);
        this.loadLayout(); // loads layout.json and fills this.controls
        this.count = 0;
        this.controls.btnPlus.on("click", () => { this.count += this.stepValue(); this.render(); });
        this.controls.btnMinus.on("click", () => { this.count -= this.stepValue(); this.render(); });
        this.controls.btnReset.on("click", () => {
            this.count = 0;
            this.render();
            this.setStatus(this.t("Counter_Reset"));
        });
        this.render();
        console.log("Counter initialized");
    }

    stepValue() {
        const raw = parseInt(this.controls.stepBox.text, 10);
        return isNaN(raw) || raw === 0 ? 1 : raw;
    }

    render() {
        this.controls.countLabel.text = String(this.count);
    }

    activate() {
        this.setStatus(this.t("Counter_Active"));
    }

    deactivate() {
        this.setStatus(this.t("Counter_Left"));
    }
}
