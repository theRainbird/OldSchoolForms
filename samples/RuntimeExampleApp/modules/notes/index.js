// Notes module (class form): save/load via the host sandbox.
export default class NotesForm extends FormBase {
    constructor(ctx) {
        super(ctx);
        this.loadLayout(); // loads layout.json and fills this.controls
        this.file = "notes.txt";

        this.controls.btnSave.on("click", () => {
            this.host.writeText(this.file, this.controls.memo.text);
            this.controls.noteStatus.text = this.t("Notes_Saved");
            this.setStatus(this.t("Notes_SavedStatus"));
        });

        this.controls.btnLoad.on("click", () => {
            const content = this.host.readSandboxText(this.file);
            if (content === null || content === undefined) {
                this.controls.noteStatus.text = this.t("Notes_None");
                this.setStatus(this.t("Notes_NoneStatus"));
            } else {
                this.controls.memo.text = content;
                this.controls.noteStatus.text = this.t("Notes_Loaded");
                this.setStatus(this.t("Notes_LoadedStatus"));
            }
        });

        console.log("Notes initialized");
    }

    activate() {
        this.setStatus(this.t("Notes_Active"));
    }

    deactivate() {
        this.setStatus(this.t("Notes_Left"));
    }
}
