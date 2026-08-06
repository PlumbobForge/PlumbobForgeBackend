using System.Globalization;
using Microsoft.Extensions.Options;
using PlumbobForge.Backend.Configuration;

namespace PlumbobForge.Backend.Services;

public class LocalizationService
{
    private readonly PlumbobForgeOptions _options;
    private readonly HashSet<string> _supportedLanguages = new(StringComparer.OrdinalIgnoreCase) { "en", "pl", "uk" };

    public LocalizationService(IOptionsSnapshot<PlumbobForgeOptions> options)
    {
        _options = options.Value;
    }

    public string GetCurrentLanguage()
    {
        string configured = _options.Language ?? "auto";
        if (string.Equals(configured, "auto", StringComparison.OrdinalIgnoreCase))
        {
            string systemLang = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            return _supportedLanguages.Contains(systemLang) ? systemLang : "en";
        }

        return _supportedLanguages.Contains(configured) ? configured.ToLowerInvariant() : "en";
    }

    public string GetString(string key, params object[] args)
    {
        string lang = GetCurrentLanguage();

        string template = lang switch
        {
            "pl" => GetPolishString(key),
            "uk" => GetUkrainianString(key),
            _ => GetEnglishString(key)
        };

        if (args != null && args.Length > 0)
        {
            try { return string.Format(template, args); } catch { return template; }
        }

        return template;
    }

    private static string GetEnglishString(string key) => key switch
    {
        "checking_orphan_packages" => "Checking for new orphan packages...",
        "validating_set_hierarchy" => "Validating set hierarchy...",
        "initializing_config_profile" => "Initializing configuration profile...",
        "scanning_cache_requirements" => "Scanning cache requirements...",
        "scan_complete" => "Scan complete.",
        "rebuild_error" => "Error during rebuild: {0}",
        "rebuilding_cache_for_set" => "Rebuilding cache for set: {0} ({1}/{2})...",
        "merging_item" => "Merging item {0} of {1} in '{2}'...",
        "skipping_unreadable_file" => "Skipping unreadable file '{0}': {1}",
        "error_processing_package" => "Error processing package '{0}': {1}",
        "skipping_unreadable_sims3pack" => "Skipping unreadable Sims3Pack '{0}': {1}",
        "skipping_missing_file" => "Skipping missing file '{0}'",
        "skipping_invalid_package" => "Skipping invalid package '{0}'",
        "skipping_invalid_package_sims3pack" => "Skipping invalid package in Sims3Pack '{0}'",
        "rebuild_partially_completed" => "⚠ Rebuilding partially completed. {0} file(s) were skipped:",
        "syncing_cache_to_sims3" => "Syncing cache to The Sims 3 folder...",
        "sync_success" => "Successfully synced to The Sims 3.",
        "sync_error" => "Error syncing to The Sims 3: {0}",

        // Auto-Fix
        "autofix_starting" => "Starting Auto-Fix Routine...",
        "autofix_checking_missing_sets" => "Checking for items with missing sets...",
        "autofix_found_unassigned" => "Found {0} unassigned items. Moving to Default set...",
        "autofix_marking_all_sets" => "Marking all sets for a complete cache rebuild...",
        "autofix_delegating_rebuild" => "Delegating to standard rebuild process...",
        "autofix_complete" => "Auto-Fix complete.",

        // Package Recheck
        "rechecking_items_progress" => "Rechecking items... {0}/{1}",
        "rechecking_saving_items" => "Saving {0} updated items to database...",
        "rechecking_finished" => "Finished. Updated {0} items.",

        // Import Downloads
        "import_scanning_downloads" => "Scanning Downloads folder for packages...",
        "import_no_files_found" => "No .package or .sims3pack files found in Downloads.",
        "import_moved_files" => "Moved {0} files to Library. Registering in database...",
        "import_complete" => "Import complete.",
        "import_no_files_moved" => "No files were moved.",
        "import_failed_move" => "Failed to move {0}: {1}",
        "import_no_ea_dir" => "Could not find Electronic Arts directory.",
        "import_no_downloads_dir" => "The Sims 3 Downloads folder does not exist.",

        _ => key
    };

    private static string GetPolishString(string key) => key switch
    {
        "checking_orphan_packages" => "Sprawdzanie nowych nieprzypisanych pakietów...",
        "validating_set_hierarchy" => "Sprawdzanie hierarchii zestawów...",
        "initializing_config_profile" => "Inicjalizacja profilu konfiguracji...",
        "scanning_cache_requirements" => "Skanowanie wymagań pamięci podręcznej...",
        "scan_complete" => "Skanowanie zakończone.",
        "rebuild_error" => "Błąd podczas przebudowy: {0}",
        "rebuilding_cache_for_set" => "Przebudowa pamięci podręcznej dla zestawu: {0} ({1}/{2})...",
        "merging_item" => "Łączenie elementu {0} z {1} w '{2}'...",
        "skipping_unreadable_file" => "Pominięto nieczytelny plik '{0}': {1}",
        "error_processing_package" => "Błąd podczas przetwarzania pakietu '{0}': {1}",
        "skipping_unreadable_sims3pack" => "Pominięto nieczytelny plik Sims3Pack '{0}': {1}",
        "skipping_missing_file" => "Pominięto brakujący plik '{0}'",
        "skipping_invalid_package" => "Pominięto nieprawidłowy pakiet '{0}'",
        "skipping_invalid_package_sims3pack" => "Pominięto nieprawidłowy pakiet w Sims3Pack '{0}'",
        "rebuild_partially_completed" => "⚠ Przebudowa częściowo zakończona. Pominięto {0} plik(ów):",
        "syncing_cache_to_sims3" => "Synchronizacja pamięci podręcznej z folderem The Sims 3...",
        "sync_success" => "Pomyślnie zsynchronizowano z The Sims 3.",
        "sync_error" => "Błąd podczas synchronizacji z The Sims 3: {0}",

        // Auto-Fix
        "autofix_starting" => "Rozpoczynanie procedury automatycznej naprawy...",
        "autofix_checking_missing_sets" => "Sprawdzanie elementów bez przypisanego zestawu...",
        "autofix_found_unassigned" => "Znaleziono {0} nieprzypisanych elementów. Przenoszenie do zestawu Domyślny...",
        "autofix_marking_all_sets" => "Oznaczanie wszystkich zestawów do pełnej przebudowy pamięci podręcznej...",
        "autofix_delegating_rebuild" => "Przekazywanie do standardowego procesu przebudowy...",
        "autofix_complete" => "Automatyczna naprawa zakończona.",

        // Package Recheck
        "rechecking_items_progress" => "Ponowne sprawdzanie elementów... {0}/{1}",
        "rechecking_saving_items" => "Zapisywanie {0} zaktualizowanych elementów w bazie danych...",
        "rechecking_finished" => "Zakończono. Zaktualizowano {0} elementów.",

        // Import Downloads
        "import_scanning_downloads" => "Skanowanie folderu Pobrane w poszukiwaniu pakietów...",
        "import_no_files_found" => "Nie znaleziono plików .package ani .sims3pack w Pobranych.",
        "import_moved_files" => "Przeniesiono {0} plików do Biblioteki. Rejestrowanie w bazie danych...",
        "import_complete" => "Import zakończony.",
        "import_no_files_moved" => "Nie przeniesiono żadnych plików.",
        "import_failed_move" => "Nie udało się przenieść {0}: {1}",
        "import_no_ea_dir" => "Nie można odnaleźć katalogu Electronic Arts.",
        "import_no_downloads_dir" => "Folder Pobrane gry The Sims 3 nie istnieje.",

        _ => key
    };

    private static string GetUkrainianString(string key) => key switch
    {
        "checking_orphan_packages" => "Перевірка нових нерозподілених пакетів...",
        "validating_set_hierarchy" => "Перевірка ієрархії наборів...",
        "initializing_config_profile" => "Ініціалізація профілю конфігурації...",
        "scanning_cache_requirements" => "Сканування вимог кешу...",
        "scan_complete" => "Сканування завершено.",
        "rebuild_error" => "Помилка під час перебудови: {0}",
        "rebuilding_cache_for_set" => "Перебудова кешу для набору: {0} ({1}/{2})...",
        "merging_item" => "Об'єднання елемента {0} з {1} у '{2}'...",
        "skipping_unreadable_file" => "Пропущено нечитабельний файл '{0}': {1}",
        "error_processing_package" => "Помилка обробки пакета '{0}': {1}",
        "skipping_unreadable_sims3pack" => "Пропущено нечитабельний файл Sims3Pack '{0}': {1}",
        "skipping_missing_file" => "Пропущено відсутній файл '{0}'",
        "skipping_invalid_package" => "Пропущено недійсний пакет '{0}'",
        "skipping_invalid_package_sims3pack" => "Пропущено недійсний пакет у Sims3Pack '{0}'",
        "rebuild_partially_completed" => "⚠ Перебудову частково завершено. Пропущено {0} файл(ів):",
        "syncing_cache_to_sims3" => "Синхронізація кешу з папкою The Sims 3...",
        "sync_success" => "Успішно синхронізовано з The Sims 3.",
        "sync_error" => "Помилка синхронізації з The Sims 3: {0}",

        // Auto-Fix
        "autofix_starting" => "Запуск процедури автоматичного виправлення...",
        "autofix_checking_missing_sets" => "Перевірка елементів без призначеного набору...",
        "autofix_found_unassigned" => "Знайдено {0} нерозподілених елементів. Переміщення до набору За замовчуванням...",
        "autofix_marking_all_sets" => "Позначення всіх наборів для повної перебудови кешу...",
        "autofix_delegating_rebuild" => "Передача стандартному процесу перебудови...",
        "autofix_complete" => "Автоматичне виправлення завершено.",

        // Package Recheck
        "rechecking_items_progress" => "Повторна перевірка елементів... {0}/{1}",
        "rechecking_saving_items" => "Збереження {0} оновлених елементів у базу даних...",
        "rechecking_finished" => "Завершено. Оновлено {0} елементів.",

        // Import Downloads
        "import_scanning_downloads" => "Сканування папки Завантажень...",
        "import_no_files_found" => "Файлів .package або .sims3pack у Завантаженнях не знайдено.",
        "import_moved_files" => "Переміщено {0} файлів до Бібліотеки. Реєстрація у базі даних...",
        "import_complete" => "Імпорт завершено.",
        "import_no_files_moved" => "Файли не переміщувалися.",
        "import_failed_move" => "Не вдалося перемістити {0}: {1}",
        "import_no_ea_dir" => "Не вдалося знайти каталог Electronic Arts.",
        "import_no_downloads_dir" => "Папка Завантажень The Sims 3 не існує.",

        _ => key
    };
}
