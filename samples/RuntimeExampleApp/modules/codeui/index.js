// Code-UI module: the layout is built entirely in JavaScript (no layout.json).
// Demonstrates this.createControl(type, props) + this.tab.add(...) + nesting.

export default class CodeUiForm extends FormBase {
    constructor(ctx) {
        super(ctx);

        // Root panel, fills the tab.
        const root = this.createControl("Panel", { name: "root", dock: "Fill" });

        // Header at the top.
        const header = this.createControl("Label", {
            name: "header",
            text: this.t("CodeUi_Header"),
            dock: "Top"
        });
        root.add(header);

        // Content area.
        const body = this.createControl("Panel", { name: "body", dock: "Fill" });

        const counterLabel = this.createControl("Label", {
            name: "counterLabel",
            text: this.t("CodeUi_Counter", 0),
            dock: "Top"
        });
        body.add(counterLabel);

        const incBtn = this.createControl("Button", {
            name: "incBtn",
            text: this.t("CodeUi_Increase"),
            dock: "Top"
        });

        let count = 0;
        incBtn.on("click", () => {
            count += 1;
            counterLabel.text = this.t("CodeUi_Counter", count);
            this.setStatus(this.t("CodeUi_Incremented", count));
        });
        body.add(incBtn);

        const resetBtn = this.createControl("Button", {
            name: "resetBtn",
            text: this.t("CodeUi_Reset"),
            dock: "Top"
        });
        resetBtn.on("click", () => {
            count = 0;
            counterLabel.text = this.t("CodeUi_Counter", 0);
            this.setStatus(this.t("Counter_Reset"));
        });
        body.add(resetBtn);

        root.add(body);

        // Everything into the tab.
        this.tab.add(root);

        console.log("Code-UI initialized");
    }

    activate() {
        this.setStatus(this.t("CodeUi_Active"));
    }

    deactivate() {
        this.setStatus(this.t("CodeUi_Left"));
    }
}
