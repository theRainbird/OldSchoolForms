// Entry script of the sample app (class form).
// ctx = { form, host, app, i18n }

export default class SampleApp extends FormBase {
    constructor(ctx) {
        super(ctx);
        console.log("Sample app initialized: " + this.app.name + " v" + this.app.version);
        this.setStatus(this.t("App_Welcome", this.app.name));

        // Open the start page directly so content is visible at startup.
        this.openModule("dashboard");
    }
}
