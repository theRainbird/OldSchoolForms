namespace OldSchoolForms.Server.Runtime.Tests;

/// <summary>
/// Creates a disposable temporary server package directory for tests.
/// </summary>
public sealed class TestPackage : IDisposable
{
    /// <summary>Absolute root of the temporary package.</summary>
    public string Root { get; }

    public TestPackage()
    {
        Root = Path.Combine(Path.GetTempPath(), "osf-server-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Root);
    }

    /// <summary>
    /// Writes a file into the package.
    /// </summary>
    /// <param name="relativePath">Relative path within the package.</param>
    /// <param name="content">File content.</param>
    public void Write(string relativePath, string content)
    {
        var full = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    /// <summary>
    /// Writes the standard test package: math + customers services and a shared validation module.
    /// </summary>
    public void WriteDefaultPackage()
    {
        Write("server.json", """
        {
          "name": "TestServer",
          "version": "1.0.0",
          "host": "127.0.0.1",
          "port": 19555,
          "encryption": false,
          "services": [
            { "id": "math", "script": "services/math/index.js", "lifetime": "singleton" },
            { "id": "customers", "script": "services/customers/index.js", "lifetime": "singleton", "description": "Customer services" },
            { "id": "counter", "script": "services/counter/index.js", "lifetime": "singlecall" }
          ],
          "shared": { "dir": "shared", "version": "1.0.0" }
        }
        """);

        Write("services/math/index.js", """
        export default class MathService {
          constructor(ctx) { this.ctx = ctx; }
          add(a, b) { return a + b; }
          describe() { return this.ctx.config.serviceId; }
        }
        """);

        Write("services/customers/index.js", """
        import { validateName } from '@shared/validation/customers.js';

        export default class CustomersService {
          constructor(ctx) { this.ctx = ctx; }

          list() {
            return this.ctx.data.tableFromRows('Customers', [
              { id: 1, name: 'Ada' },
              { id: 2, name: 'Bob' }
            ]);
          }

          countChanges(table) {
            const changes = table.getChanges();
            return changes.rowCount;
          }

          validate(name) {
            return validateName(name);
          }

          echoTable() {
            const t = this.ctx.data.tableFromRows('Echo', [{ x: 42 }]);
            return t;
          }
        }
        """);

        Write("services/counter/index.js", """
        export default class CounterService {
          constructor(ctx) { this.ctx = ctx; }
          value() { return 7; }
        }
        """);

        Write("shared/validation/customers.js", """
        export function validateName(name) {
          if (!name || String(name).trim().length < 2) return 'Name too short';
          return null;
        }
        """);
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, true); } catch { /* best effort */ }
    }
}
