// Shared validation logic for the customer schema.
// This module is served by the server to clients via @shared/validation/customers.js
// and is ALSO imported by the server-side customers service. One source of truth.

export const CUSTOMER_COLUMNS = ["id", "name", "email", "age"];

export function validateName(name) {
  if (name === null || name === undefined || String(name).trim().length < 2) {
    return "Name zu kurz (mind. 2 Zeichen)";
  }
  return null;
}

export function validateEmail(email) {
  if (email === null || email === undefined || String(email).length === 0) {
    return null; // optional
  }
  const value = String(email);
  if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(value)) {
    return "Ungültige E-Mail-Adresse";
  }
  return null;
}

export function validateAge(age) {
  if (age === null || age === undefined || age === "") {
    return null; // optional
  }
  const n = Number(age);
  if (!Number.isFinite(n) || n < 0 || n > 150) {
    return "Alter muss zwischen 0 und 150 liegen";
  }
  return null;
}

// Validates a whole JsDataTable against the customer schema.
// Returns an array of { row, column, message } problems (empty when valid).
export function validateCustomerTable(table) {
  const problems = [];
  const rowCount = table.rowCount;
  for (let i = 0; i < rowCount; i++) {
    const row = table.getRow(i);
    const nameError = validateName(row.get("name"));
    if (nameError) problems.push({ row: i, column: "name", message: nameError });
    const emailError = validateEmail(row.get("email"));
    if (emailError) problems.push({ row: i, column: "email", message: emailError });
    const ageError = validateAge(row.get("age"));
    if (ageError) problems.push({ row: i, column: "age", message: ageError });
  }
  return problems;
}
