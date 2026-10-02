// Gemeinsame Hilfsfunktionen, importierbar via "@app/shared/util.js".

// Führt eine Zahl auf zwei Stellen mit führender Null.
export function pad(n) {
    return n < 10 ? "0" + n : "" + n;
}

// Formatiert eine Uhrzeit als HH:MM:SS.
export function formatClock(d) {
    return pad(d.getHours()) + ":" + pad(d.getMinutes()) + ":" + pad(d.getSeconds());
}

// Formatiert ein Datum als YYYY-MM-DD.
export function formatDate(d) {
    return d.getFullYear() + "-" + pad(d.getMonth() + 1) + "-" + pad(d.getDate());
}
