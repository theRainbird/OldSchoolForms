// Server-side orders service. Runs ONLY on the server.

import { ORDER_SCHEMA, createTable } from "@shared/data/schema.js";

export default class OrdersService {
  constructor(context) {
    this.ctx = context;
    this.nextId = 2;
    this.orders = createTable(context, ORDER_SCHEMA);
    this.orders.add({ id: 1, customerId: 1, item: "Tastatur", quantity: 2, total: 59.98 });
  }

  list() {
    return this.orders;
  }

  listByCustomer(customerId) {
    const result = createTable(this.ctx, ORDER_SCHEMA);
    for (let i = 0; i < this.orders.rowCount; i++) {
      const row = this.orders.getRow(i);
      if (Number(row.get("customerId")) === Number(customerId)) {
        result.add(row.toObject());
      }
    }
    return result;
  }

  create(customerId, item, quantity, unitPrice) {
    const total = Number(quantity) * Number(unitPrice);
    const row = this.orders.add({
      id: this.nextId++,
      customerId: Number(customerId),
      item: String(item),
      quantity: Number(quantity),
      total: total
    });
    this.ctx.log.info("Order " + row.get("id") + " created");
    return row.toObject();
  }

  totalFor(customerId) {
    let sum = 0;
    for (let i = 0; i < this.orders.rowCount; i++) {
      const row = this.orders.getRow(i);
      if (Number(row.get("customerId")) === Number(customerId)) {
        sum += Number(row.get("total"));
      }
    }
    return sum;
  }
}
