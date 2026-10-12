// Reports demo: builds a report via the JS facades, opens it in the viewer,
// exports PDF, and loads a report definition from a JSON file.
export default class ReportsForm extends FormBase {
    constructor(ctx) {
        super(ctx);
        this.loadLayout();

        this.controls.btnOpen.on("click", () => {
            const report = this.buildReport();
            this.host.openReport(report);
            this.info(this.t("Reports_Opened", report.name, report.pageCount()));
        });

        this.controls.btnExport.on("click", () => {
            const report = this.buildReport();
            const b64 = report.exportPdfBytes();
            this.info(this.t("Reports_Exported", b64.length));
        });

        this.controls.btnLoad.on("click", () => {
            const report = this.ctx.report.load("sales.json");
            this.host.openReport(report);
            this.info(this.t("Reports_Loaded", report.name));
        });

        this.info(this.t("Reports_Ready"));
    }

    buildReport() {
        const report = this.ctx.report.create("Umsatzbericht");
        report.pageSetup.paperSize = "A4";
        report.pageSetup.landscape = false;
        report.pageSetup.setMargins(1.5);

        // Page header with a title label.
        report.pageHeader.height = 1.2;
        report.pageHeader.controls.add({
            type: "label", text: "Umsatz nach Region",
            left: 0, top: 0.3, width: 17, height: 0.7, textAlign: "Center"
        });

        // Detail band bound to the data source.
        report.detail.height = 0.8;
        report.detail.controls.add({ type: "textBox", dataField: "region", left: 0, top: 0.1, width: 6, height: 0.6 });
        report.detail.controls.add({ type: "textBox", dataField: "product", left: 6.2, top: 0.1, width: 6, height: 0.6 });
        report.detail.controls.add({ type: "textBox", dataField: "amount", left: 12.4, top: 0.1, width: 4.5, height: 0.6, format: "N2", textAlign: "Right" });

        // Group by region with a group header.
        const group = report.groups.add("region", { name: "Nach Region", keepTogether: true });
        group.header.height = 0.8;
        group.header.controls.add({ type: "label", text: "Region: {region}", left: 0, top: 0.1, width: 17, height: 0.6 });

        // Data source as a JS array of objects.
        report.dataSource = [
            { region: "Nord", product: "Widget", amount: 1200.5 },
            { region: "Nord", product: "Gadget", amount: 800.25 },
            { region: "Süd", product: "Widget", amount: 950.0 },
            { region: "Süd", product: "Gizmo", amount: 1450.75 }
        ];

        return report;
    }

    info(msg) {
        this.controls.infoBox.text = msg;
    }

    activate() {
        this.setStatus(this.t("Reports_Active"));
    }

    deactivate() {
        this.setStatus(this.t("Reports_Left"));
    }
}
