namespace OldSchoolForms.Ui.Runtime.Sources;

/// <summary>
/// Abstraktion über den Ort, aus dem ein OldSchoolForms-App-Paket (Manifest, Layouts,
/// JavaScript-Module) gelesen wird. Alle Pfade sind relativ zur Paketwurzel und
/// verwenden Vorwärtsschrägstriche (<c>/</c>).
/// </summary>
public interface IAppSource
{
    /// <summary>
    /// Prüft, ob die angegebene relative Datei im Paket existiert.
    /// </summary>
    /// <param name="relativePath">Relativer Pfad zur Paketwurzel, z. B. <c>modules/customers/index.js</c>.</param>
    /// <returns><see langword="true"/>, wenn die Datei vorhanden ist.</returns>
    bool Exists(string relativePath);

    /// <summary>
    /// Liest den Inhalt einer Datei als Bytefolge.
    /// </summary>
    /// <param name="relativePath">Relativer Pfad zur Paketwurzel.</param>
    /// <returns>Der Dateiinhalt.</returns>
    /// <exception cref="FileNotFoundException">Die Datei existiert nicht im Paket.</exception>
    byte[] ReadBytes(string relativePath);

    /// <summary>
    /// Liest den Inhalt einer Datei als UTF-8-Text.
    /// </summary>
    /// <param name="relativePath">Relativer Pfad zur Paketwurzel.</param>
    /// <returns>Der Dateiinhalt als Text.</returns>
    /// <exception cref="FileNotFoundException">Die Datei existiert nicht im Paket.</exception>
    string ReadText(string relativePath);

    /// <summary>
    /// Liefert eine Versionsmarke (ETag) für die angegebene Datei, sofern die Quelle
    /// solche Informationen anbietet.
    /// </summary>
    /// <param name="relativePath">Relativer Pfad zur Paketwurzel.</param>
    /// <returns>Die Versionsmarke oder <see langword="null"/>.</returns>
    string? GetETag(string relativePath);

    /// <summary>
    /// Aktualisiert das Paket aus der fernen Quelle (z. B. Server-Abgleich).
    /// Lokale Quellen implementieren dies als No-op.
    /// </summary>
    /// <param name="progress">Optionaler Fortschrittsbericht.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Eine Aufgabe, die den Abgleich abschließt.</returns>
    Task SyncAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Menschenlesbare Beschreibung der Quelle für Logging und Statusanzeigen.
    /// </summary>
    string Describe { get; }
}
