// Code-UI-Modul: Layout wird komplett per JavaScript aufgebaut (kein layout.json).
// Demonstriert this.createControl(type, props) + this.tab.add(...) + Verschachtelung.

export default class CodeUiForm extends FormBase {
    constructor(ctx) {
        super(ctx);

        // Root-Panel, füllt den Tab.
        const root = this.createControl("Panel", { name: "root", dock: "Fill" });

        // Kopfzeile oben.
        const header = this.createControl("Label", {
            name: "header",
            text: "Per Code erzeugtes UI",
            dock: "Top"
        });
        root.add(header);

        // Content-Bereich.
        const body = this.createControl("Panel", { name: "body", dock: "Fill" });

        const counterLabel = this.createControl("Label", {
            name: "counterLabel",
            text: "Zähler: 0",
            dock: "Top"
        });
        body.add(counterLabel);

        const incBtn = this.createControl("Button", {
            name: "incBtn",
            text: "Erhöhen",
            dock: "Top"
        });

        let count = 0;
        incBtn.on("click", () => {
            count += 1;
            counterLabel.text = "Zähler: " + count;
            this.setStatus("Erhöht auf " + count);
        });
        body.add(incBtn);

        const resetBtn = this.createControl("Button", {
            name: "resetBtn",
            text: "Zurücksetzen",
            dock: "Top"
        });
        resetBtn.on("click", () => {
            count = 0;
            counterLabel.text = "Zähler: 0";
            this.setStatus("Zähler zurückgesetzt");
        });
        body.add(resetBtn);

        root.add(body);

        // Alles in den Tab.
        this.tab.add(root);

        console.log("Code-UI initialisiert");
    }

    activate() {
        this.setStatus("Code-UI aktiv");
    }

    deactivate() {
        this.setStatus("Code-UI verlassen");
    }
}
