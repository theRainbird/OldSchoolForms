// Dialogs module (class form): try out all messagebox combinations.
export default class DialogsForm extends FormBase {
    constructor(ctx) {
        super(ctx);
        this.loadLayout(); // loads layout.json and fills this.controls
        this.controls.btnOk.on("click", () => this.show(this.t("Dialogs_MsgOk"), "ok", "info"));
        this.controls.btnOkCancel.on("click", () => this.show(this.t("Dialogs_MsgOkCancel"), "okcancel", "question"));
        this.controls.btnYesNo.on("click", () => this.show(this.t("Dialogs_MsgYesNo"), "yesno", "question"));
        this.controls.btnYesNoCancel.on("click", () => this.show(this.t("Dialogs_MsgYesNoCancel"), "yesnocancel", "warning"));
        console.log("Dialogs initialized");
    }

    show(message, buttons, icon) {
        const result = this.host.showMessageBox(message, this.t("Dialogs_Caption"), buttons, icon);
        this.controls.resultLabel.text = this.t("Dialogs_Result", result);
        this.setStatus(this.t("Dialogs_ResultStatus", result));
    }

    activate() {
        this.setStatus(this.t("Dialogs_Active"));
    }

    deactivate() {
        this.setStatus(this.t("Dialogs_Left"));
    }
}
