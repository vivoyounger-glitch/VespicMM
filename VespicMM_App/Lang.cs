namespace CULauncher
{
    static class Lang
    {
        public static bool IsUkr = false;

        static string T(string en, string ua) => IsUkr ? ua : en;

        public static string AppTitle      => T("Casualties Unknown — Mod Launcher", "Casualties Unknown — Мод Лончер");
        public static string NewFolder     => T("+ NEW FOLDER", "+ НОВА ПАПКА");
        public static string Refresh       => T("↺ REFRESH", "↺ ОНОВИТИ");
        public static string OpenPlugins   => T("📁 OPEN PLUGINS", "📁 ВІДКРИТИ PLUGINS");
        public static string ModPacks      => T("MOD PACKS", "ПАКЕТИ МОДІВ");
        public static string FilesInPack   => T("FILES IN PACK:", "ФАЙЛИ В ПАКЕТІ:");
        public static string Disable       => T("DISABLE", "ВИМКНУТИ");
        public static string Enable        => T("ENABLE", "УВІМКНУТИ");
        public static string Delete        => T("DELETE", "ВИДАЛИТИ");
        public static string Play          => T("▶  PLAY", "▶  ГРАТИ");
        public static string Active        => T("active", "активний");
        public static string Disabled      => T("disabled", "вимкнено");
        public static string SelectLeft    => T("Select a pack on the left", "Вибери пакет зліва");
        public static string DragHint      => T("\n  Drag files here to add to this pack", "\n  Перетягни файли сюди щоб додати до пакету");
        public static string DragToInstall => T("  Drag .dll or folder here to install", "  Перетягни .dll або папку сюди для встановлення");
        public static string NewFolderPrompt => T("Name for new mod pack folder:", "Назва нової папки для пакету модів:");
        public static string ConfirmDelete  => T("Delete \"{0}\"?\nThis cannot be undone.", "Видалити \"{0}\"?\nЦю дію не можна скасувати.");
        public static string ConfirmTitle   => T("Confirm", "Підтвердження");
        public static string SelectExe      => T("Select game .exe", "Вкажи .exe файл гри");
        public static string ErrNoExe       => T("Game .exe not found!", "Файл гри не знайдено!");
        public static string ErrNoPlugins   => T("plugins folder not found!", "Папку plugins не знайдено!");
        public static string SelectPlugins  => T("Select BepInEx\\plugins folder", "Вкажи папку BepInEx\\plugins гри");
        public static string StatusPacks    => T("Packs: {0}   Active: {1}", "Пакетів: {0}   Активних: {1}");
        public static string Added          => T("Added: {0}", "Додано: {0}");
        public static string Enabled2       => T("Enabled: {0}", "Увімкнено: {0}");
        public static string Disabled2      => T("Disabled: {0}", "Вимкнено: {0}");
        public static string Deleted        => T("Deleted: {0}", "Видалено: {0}");
        public static string Created        => T("Created: {0}", "Створено: {0}");
        public static string ErrPrefix      => T("Error: {0}", "Помилка: {0}");
        public static string ShortcutDone   => T("Shortcut created on Desktop!", "Ярлик створено на робочому столі!");
        public static string Shortcut       => T("🔗 SHORTCUT", "🔗 ЯРЛИК");
        public static string AddToGroup     => T("+ Add to DLL group", "+ Додати до групи DLL-файлів");
        public static string RemoveFromGroup => T("- Remove from DLL group", "- Видалити з групи DLL-файлів");
    }
}
