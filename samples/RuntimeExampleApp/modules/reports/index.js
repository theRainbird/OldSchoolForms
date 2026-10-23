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

        this.controls.btnMulti.on("click", () => {
            const report = this.buildMultiSourceReport();
            this.host.openReport(report);
            this.info(this.t("Reports_MultiOpened", report.name, report.pageCount()));
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

    // Multi-source report: an order header (Auftragskopf) plus order positions
    // (Auftragspositionen). The report header binds to the first record of the
    // header source, the detail band repeats per position record, and the page
    // footer shows a qualified cross-source field from the header.
    buildMultiSourceReport() {
        const report = this.ctx.report.create("Auftragsbericht");
        report.pageSetup.paperSize = "A4";
        report.pageSetup.landscape = false;
        report.pageSetup.setMargins(1.5);

        // Declare the data source schemas (field names + types).
        report.dataSources.add("Auftragskopf", {
            fields: [
                { name: "AuftragsNr", type: "int" },
                { name: "Kunde", type: "string" },
                { name: "Datum", type: "date" }
            ]
        });
        report.dataSources.add("Auftragspositionen", {
            fields: [
                { name: "Artikel", type: "string" },
                { name: "Menge", type: "int" },
                { name: "Einzelpreis", type: "decimal" }
            ]
        });

        // Report header shows the order header (first record of Auftragskopf).
        report.reportHeader.height = 2.0;
        report.reportHeader.dataSourceName = "Auftragskopf";
        report.reportHeader.controls.add({
            type: "label", text: "Auftrag", left: 0, top: 0.1, width: 17, height: 0.7, textAlign: "Center"
        });
        report.reportHeader.controls.add({
            type: "textBox", expression: "Kunde: {Kunde}", left: 0, top: 0.9, width: 10, height: 0.6
        });
        report.reportHeader.controls.add({
            type: "textBox", expression: "Auftrags-Nr: {AuftragsNr}", left: 11, top: 0.9, width: 6, height: 0.6
        });

        // Detail band repeats per position record.
        report.detail.height = 0.7;
        report.detail.dataSourceName = "Auftragspositionen";
        report.detail.controls.add({ type: "textBox", dataField: "Artikel", left: 0, top: 0.1, width: 8, height: 0.5 });
        report.detail.controls.add({ type: "textBox", dataField: "Menge", left: 8.2, top: 0.1, width: 2.5, height: 0.5, textAlign: "Right" });
        report.detail.controls.add({ type: "textBox", dataField: "Einzelpreis", left: 11, top: 0.1, width: 6, height: 0.5, format: "N2", textAlign: "Right" });

        // Page footer pulls a qualified field from the header source while the
        // detail loop runs against the positions source.
        report.pageFooter.height = 0.6;
        report.pageFooter.dataSourceName = "Auftragskopf";
        report.pageFooter.controls.add({
            type: "textBox", expression: "Kunde: {Auftragskopf.Kunde}  ·  Seite {PageNumber}",
            left: 0, top: 0.1, width: 17, height: 0.5
        });

        // Attach the runtime data to each source.
        report.dataSources.get("Auftragskopf").data = [
            { AuftragsNr: 2026001, Kunde: "Musterfirma GmbH", Datum: "2026-10-21" }
        ];
        report.dataSources.get("Auftragspositionen").data = [
            { Artikel: "Widget A", Menge: 10, Einzelpreis: 19.95 },
            { Artikel: "Widget B", Menge: 4, Einzelpreis: 49.90 },
            { Artikel: "Gadget X", Menge: 2, Einzelpreis: 129.00 }
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
