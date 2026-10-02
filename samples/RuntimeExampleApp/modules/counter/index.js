// Zähler-Modul: Plus/Minus/Reset mit einstellbarer Schrittweite.
let ctxRef = null;
let count = 0;

function stepValue() {
    if (!ctxRef) return 1;
    const raw = parseInt(ctxRef.controls.stepBox.text, 10);
    return isNaN(raw) || raw === 0 ? 1 : raw;
}

function render() {
    if (ctxRef) ctxRef.controls.countLabel.text = String(count);
}

export function init(ctx) {
    ctxRef = ctx;
    ctx.controls.btnPlus.on("click", () => { count += stepValue(); render(); });
    ctx.controls.btnMinus.on("click", () => { count -= stepValue(); render(); });
    ctx.controls.btnReset.on("click", () => {
        count = 0;
        render();
        ctx.host.setStatus("Zähler zurückgesetzt");
    });
    render();
    console.log("Zähler initialisiert");
}

export function activate() {
    if (ctxRef) ctxRef.host.setStatus("Zähler aktiv");
}

export function deactivate() {
    if (ctxRef) ctxRef.host.setStatus("Zähler verlassen");
}

export function dispose() {
    ctxRef = null;
}
