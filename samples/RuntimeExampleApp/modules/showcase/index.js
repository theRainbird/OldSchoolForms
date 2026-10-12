// Controls showcase: demonstrates the typed JS facades and their events.
export default class ShowcaseForm extends FormBase {
    constructor(ctx) {
        super(ctx);
        this.loadLayout();
        this.logLines = [];

        // Populate combo + list via the typed facade item APIs.
        this.controls.cmbPick.items = ["Rot", "Grün", "Blau"];
        this.controls.lstItems.items = ["Apfel", "Birne", "Kirsche", "Traube"];
        this.controls.pbar.minimum = 0;
        this.controls.pbar.maximum = 100;
        this.controls.pbar.value = 30;

        // Button click via typed facade.
        this.controls.btnClick.on("click", () => {
            this.log(this.t("Showcase_Clicked"));
        });

        // CheckBox checkedchanged.
        this.controls.chkAgree.on("checkedchanged", () => {
            this.log(this.t("Showcase_AgreeChanged", this.controls.chkAgree.checked ? "an" : "aus"));
        });

        // Radio buttons.
        this.controls.radA.on("checkedchanged", () => {
            if (this.controls.radA.checked) this.log("Radio A");
        });
        this.controls.radB.on("checkedchanged", () => {
            if (this.controls.radB.checked) this.log("Radio B");
        });

        // TextBox textchanged.
        this.controls.txtInput.on("textchanged", () => {
            this.log(this.t("Showcase_TextChanged", this.controls.txtInput.text));
        });

        // ComboBox selectedindexchanged shows the selected item.
        this.controls.cmbPick.on("selectedindexchanged", () => {
            const i = this.controls.cmbPick.selectedIndex;
            this.log(this.t("Showcase_ComboPicked", i >= 0 ? this.controls.cmbPick.items[i] : "-"));
        });

        // ListBox selectedindexchanged.
        this.controls.lstItems.on("selectedindexchanged", () => {
            const i = this.controls.lstItems.selectedIndex;
            this.log(this.t("Showcase_ListPicked", i >= 0 ? this.controls.lstItems.items[i] : "-"));
        });

        // ProgressBar valuechanged.
        this.controls.pbar.on("valuechanged", () => {
            this.log(this.t("Showcase_Progress", this.controls.pbar.value));
        });
        this.controls.btnUp.on("click", () => {
            this.controls.pbar.value = Math.min(this.controls.pbar.maximum, this.controls.pbar.value + 10);
        });
        this.controls.btnDown.on("click", () => {
            this.controls.pbar.value = Math.max(this.controls.pbar.minimum, this.controls.pbar.value - 10);
        });

        // DateTimePicker valuechanged (ISO string).
        this.controls.dtp.on("valuechanged", () => {
            this.log(this.t("Showcase_Date", this.controls.dtp.value));
        });

        this.log(this.t("Showcase_Ready"));
    }

    log(msg) {
        this.logLines.push(msg);
        if (this.logLines.length > 200) this.logLines.shift();
        this.controls.logBox.text = this.logLines.join("\n");
    }

    activate() {
        this.setStatus(this.t("Showcase_Active"));
    }

    deactivate() {
        this.setStatus(this.t("Showcase_Left"));
    }
}
