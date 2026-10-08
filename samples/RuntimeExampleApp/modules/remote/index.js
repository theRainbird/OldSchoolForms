// Remote customers module: consumes the server's customers service via ctx.remote
// and reuses the SAME validation code the server uses, downloaded via @shared.
// Data binding uses a BindingSource (ctx.data.createBindingSource) for live grid display,
// filtering (DataView.RowFilter), sorting and master-detail editing of the current row.
import { validateName, validateEmail, validateAge, validateCustomerTable } from "@shared/validation/customers.js";

export default class RemoteCustomersForm extends FormBase {
    constructor(ctx) {
        super(ctx);
        console.log("REMOTE ctor START");
        this.loadLayout();
        console.log("REMOTE ctor after loadLayout, controls=" + (this.controls ? Object.keys(this.controls).join(",") : "NULL"));
        this.table = null;
        this.bs = null;

        this.controls.btnLoad.on("click", () => this.load());
        this.controls.btnLoadAsync.on("click", () => this.loadAsync());
        this.controls.btnApply.on("click", () => this.applyFilterSort());
        this.controls.navFirst.on("click", () => this.bs && this.bs.moveFirst());
        this.controls.navPrev.on("click", () => this.bs && this.bs.movePrevious());
        this.controls.navNext.on("click", () => this.bs && this.bs.moveNext());
        this.controls.navLast.on("click", () => this.bs && this.bs.moveLast());
        this.controls.btnAdd.on("click", () => this.addCustomer());
        this.controls.btnSave.on("click", () => this.save());
        this.controls.btnValidate.on("click", () => this.validateAll());
    }

    load() {
        try {
            this.table = this.ctx.remote.invoke("customers", "list");
            this.bindView();
            this.setStatus(this.t("Remote_Loaded") + ": " + this.bs.count);
        } catch (e) {
            this.setStatus(this.t("Remote_Error") + ": " + e.message);
        }
    }

    loadAsync() {
        this.setStatus(this.t("Remote_Loading"));
        this.ctx.remote.invokeAsync("customers", "list")
            .then((table) => {
                this.table = table;
                this.bindView();
                this.setStatus(this.t("Remote_Loaded") + ": " + this.bs.count);
            })
            .catch((err) => {
                this.setStatus(this.t("Remote_Error") + ": " + err.message);
            });
    }

    bindView() {
        if (this.bs) {
            this.bs.dispose();
        }
        this.bs = this.ctx.data.createBindingSource(this.table);

        // Two-way binding of the edit boxes to the current row's columns.
        this.bs.bind(this.controls.nameBox, "text", "name");
        this.bs.bind(this.controls.emailBox, "text", "email");
        this.bs.bind(this.controls.ageBox, "text", "age");

        // Live grid bound to the filtered/sorted view.
        this.controls.grid.bindSource(this.bs);

        this.bs.onCurrentChanged(() => this.updatePosition());
        this.bs.onListChanged(() => this.updatePosition());
        this.updatePosition();
    }

    applyFilterSort() {
        if (!this.bs) { return; }
        try {
            this.bs.filter = this.controls.filterBox.text || "";
            this.bs.sort = this.controls.sortBox.text || "";
            this.setStatus(this.t("Remote_FilterApplied") + ": " + this.bs.count);
        } catch (e) {
            this.setStatus(this.t("Remote_Error") + ": " + e.message);
        }
    }

    addCustomer() {
        if (!this.table) { this.load(); }
        const name = this.controls.nameBox.text;
        const email = this.controls.emailBox.text;
        const age = this.controls.ageBox.text;
        console.log("ADDDEBUG name=[" + name + "] email=[" + email + "] age=[" + age + "]");
        console.log("ADDDEBUG2 nbName=" + this.controls.nameBox.name + " nbText=[" + this.controls.nameBox.text + "]");

        const problems = [];
        const nameErr = validateName(name);
        if (nameErr) problems.push(nameErr);
        const emailErr = validateEmail(email);
        if (emailErr) problems.push(emailErr);
        const ageErr = validateAge(age);
        if (ageErr) problems.push(ageErr);

        if (problems.length > 0) {
            this.setStatus(this.t("Remote_Invalid") + ": " + problems.join("; "));
            return;
        }

        this.table.add({ id: 0, name: name, email: email, age: parseInt(age, 10) });
        this.bs.moveLast();
        this.clearBoxes();
        this.setStatus(this.t("Remote_RowAdded"));
    }

    save() {
        if (!this.table) { this.setStatus(this.t("Remote_NothingToSave")); return; }
        try {
            const result = this.ctx.remote.invoke("customers", "save", [this.table]);
            if (result.ok) {
                this.table = result.table;
                this.bindView();
                this.setStatus(this.t("Remote_Saved"));
            } else {
                const msgs = (result.problems || []).map((p) => "Zeile " + (p.row + 1) + " " + p.column + ": " + p.message);
                this.setStatus(this.t("Remote_ValidationFailed") + ": " + msgs.join("; "));
            }
        } catch (e) {
            this.setStatus(this.t("Remote_Error") + ": " + e.message);
        }
    }

    validateAll() {
        if (!this.table) { this.setStatus(this.t("Remote_NothingToValidate")); return; }
        const problems = validateCustomerTable(this.table);
        if (problems.length === 0) {
            this.setStatus(this.t("Remote_AllValid"));
        } else {
            const msgs = problems.map((p) => "Zeile " + (p.row + 1) + " " + p.column + ": " + p.message);
            this.setStatus(this.t("Remote_ValidationFailed") + ": " + msgs.join("; "));
        }
    }

    updatePosition() {
        if (!this.bs) {
            this.controls.posLabel.text = "";
            return;
        }
        const total = this.bs.count;
        const pos = total > 0 ? this.bs.position + 1 : 0;
        this.controls.posLabel.text = this.t("Remote_Position", pos, total);
    }

    clearBoxes() {
        this.controls.nameBox.text = "";
        this.controls.emailBox.text = "";
        this.controls.ageBox.text = "";
    }
}
