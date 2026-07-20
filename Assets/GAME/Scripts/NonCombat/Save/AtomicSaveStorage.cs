using System;
using System.IO;

namespace Game.NonCombat.Save
{
    internal sealed class AtomicSaveStorage
    {
        internal string PrimaryPath { get; }
        internal string BackupPath => PrimaryPath + ".bak";
        internal string TemporaryPath => PrimaryPath + ".tmp";

        internal AtomicSaveStorage(string primaryPath) { PrimaryPath = primaryPath; }

        internal bool TryWrite(string contents, out string error)
        {
            error = null;
            try
            {
                string directory = Path.GetDirectoryName(PrimaryPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                if (File.Exists(TemporaryPath)) File.Delete(TemporaryPath);
                File.WriteAllText(TemporaryPath, contents);
                if (File.Exists(PrimaryPath))
                {
                    if (File.Exists(BackupPath)) File.Delete(BackupPath);
                    File.Move(PrimaryPath, BackupPath);
                }
                File.Move(TemporaryPath, PrimaryPath);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                try { if (File.Exists(TemporaryPath)) File.Delete(TemporaryPath); } catch { }
                try { if (!File.Exists(PrimaryPath) && File.Exists(BackupPath)) File.Copy(BackupPath, PrimaryPath); } catch { }
                return false;
            }
        }

        internal bool TryRead(out string json, out bool recoveredBackup, out string error)
        {
            json = null; recoveredBackup = false; error = null;
            if (TryReadPath(PrimaryPath, out json, out error)) return true;
            string primaryError = error;
            if (TryReadPath(BackupPath, out json, out error)) { recoveredBackup = true; return true; }
            error = !File.Exists(PrimaryPath) && !File.Exists(BackupPath) ? "No save file exists." : $"Primary: {primaryError}; Backup: {error}";
            return false;
        }

        private static bool TryReadPath(string path, out string text, out string error)
        {
            text = null; error = null;
            if (!File.Exists(path)) { error = "File not found."; return false; }
            try { text = File.ReadAllText(path); return !string.IsNullOrWhiteSpace(text); }
            catch (Exception exception) { error = exception.Message; return false; }
        }
    }
}
