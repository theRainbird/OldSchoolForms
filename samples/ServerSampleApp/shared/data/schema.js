// Shared data schema definitions.
// Both the server services and the client build their DataTables from these
// definitions so the wire format is identical on both ends.

export const CUSTOMER_SCHEMA = {
  name: "Customers",
  columns: [
    { name: "id", type: "long" },
    { name: "name", type: "string" },
    { name: "email", type: "string" },
    { name: "age", type: "long" }
  ]
};

export const ORDER_SCHEMA = {
  name: "Orders",
  columns: [
    { name: "id", type: "long" },
    { name: "customerId", type: "long" },
    { name: "item", type: "string" },
    { name: "quantity", type: "long" },
    { name: "total", type: "double" }
  ]
};

// Builds an empty, typed JsDataTable from a schema definition.
// Works on both the server (ctx.data) and the client (ctx.data).
export function createTable(ctx, schema) {
  const table = ctx.data.newTable(schema.name);
  for (const col of schema.columns) {
    table.addColumn(col.name, col.type);
  }
  return table;
}
