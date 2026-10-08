// Server-side customers service. Runs ONLY on the server.
// Imports the shared validation module (same code the client downloads) so
// validation rules are identical on both ends.

import { validateCustomerTable, validateName } from "@shared/validation/customers.js";
import { CUSTOMER_SCHEMA, createTable } from "@shared/data/schema.js";

export default class CustomersService {
  constructor(context) {
    this.ctx = context;
    this.nextId = 3;
    this.store = createTable(context, CUSTOMER_SCHEMA);
    this.store.add({ id: 1, name: "Ada Lovelace", email: "ada@example.com", age: 36 });
    this.store.add({ id: 2, name: "Bob Smith", email: "bob@example.com", age: 41 });
    this.store.acceptChanges();
  }

  // Returns the full customer table (typed DataTable over the wire).
  list() {
    return this.store;
  }

  // Returns a single customer row as a plain object, or null.
  get(id) {
    for (let i = 0; i < this.store.rowCount; i++) {
      const row = this.store.getRow(i);
      if (Number(row.get("id")) === Number(id)) {
        return row.toObject();
      }
    }
    return null;
  }

  // Server-side validation of a single name (shared rule).
  checkName(name) {
    return validateName(name);
  }

  // Validates a whole table sent from the client; returns array of problems.
  validate(table) {
    return validateCustomerTable(table);
  }

  // Persists changes from a client table. Validates first; on success merges the
  // changes into the store and returns the updated table. On failure returns
  // { ok: false, problems: [...] }.
  save(changes) {
    const problems = validateCustomerTable(changes);
    if (problems.length > 0) {
      return { ok: false, problems: problems };
    }

    const delta = changes.getChanges();
    for (let i = 0; i < delta.rowCount; i++) {
      const row = delta.getRow(i);
      const state = row.state;
      const id = Number(row.get("id"));
      if (state === "added") {
        const copy = row.toObject();
        copy.id = this.nextId++;
        this.store.add(copy);
      } else if (state === "modified") {
        for (let j = 0; j < this.store.rowCount; j++) {
          const existing = this.store.getRow(j);
          if (Number(existing.get("id")) === id) {
            existing.set("name", row.get("name"));
            existing.set("email", row.get("email"));
            existing.set("age", row.get("age"));
            break;
          }
        }
      } else if (state === "deleted") {
        for (let j = 0; j < this.store.rowCount; j++) {
          const existing = this.store.getRow(j);
          if (Number(existing.get("id")) === id) {
            this.store.deleteRow(existing);
            break;
          }
        }
      }
    }
    this.store.acceptChanges();
    this.ctx.log.info("Saved customer changes");
    return { ok: true, table: this.store };
  }
}
