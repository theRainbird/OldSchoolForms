// Gemeinsame Hilfsfunktionen, importierbar via "@app/shared/util.js".

export function formatCurrency(value) {
    return value.toFixed(2) + " EUR";
}

export function greet(name) {
    return "Hallo, " + name + "!";
}
