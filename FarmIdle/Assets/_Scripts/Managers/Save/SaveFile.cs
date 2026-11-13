using System;

namespace FarmIdle
{
    // Cómo terminó de cargarse la partida (SaveFile.Load); lo muestra el panel de debug.
    public enum SaveLoadStatus
    {
        NotLoaded,                 // todavía no se intentó
        NewGame,                   // no había archivo: partida nueva
        Loaded,                    // se leyó la partida
        Migrated,                  // se leyó una partida de un formato viejo y se la llevó al actual
        RecoveredFromBackup,       // el archivo principal faltaba o estaba dañado y se usó el respaldo
        CorruptStartedFresh,       // principal y respaldo dañados: partida nueva (el archivo roto se conserva aparte)
        FutureVersionStartedFresh  // la partida es de una versión más nueva: partida nueva (se conserva aparte, sin tocar)
    }

    public sealed class SaveLoadResult
    {
        public SaveLoadStatus Status = SaveLoadStatus.NotLoaded;
        public SaveData Data;                      // null con NewGame y con los "StartedFresh"
        public string Message = string.Empty;      // para avisar en consola

        public bool HasData => Data != null;
        public bool NeedsWarning => Status == SaveLoadStatus.RecoveredFromBackup || Status == SaveLoadStatus.CorruptStartedFresh || Status == SaveLoadStatus.FutureVersionStartedFresh;
    }

    // El protocolo de archivos de la partida, sin Unity: una partida principal, un respaldo con la anterior y los archivos
    // "apartados" cuando algo no se puede usar. Trabaja sobre un ISaveStorage, así que se prueba en EditMode con uno en memoria.
    //   Escribir: el respaldo pasa a ser la última partida buena y recién después se escribe la nueva (de forma segura:
    //             el storage escribe a un temporal y reemplaza). Si la escritura falla, la partida anterior sigue intacta.
    //   Leer:     principal -> respaldo -> partida nueva. Un archivo dañado se COPIA a 'save.corrupt' (y uno de una versión
    //             más nueva a 'save.future') antes de seguir, para que nunca se pierda un archivo que alguien podría rescatar;
    //             el juego arranca igual, sin tirar excepciones.
    public sealed class SaveFile
    {
        public const string MainSlot = "save";
        public const string BackupSlot = "save.bak";
        public const string CorruptSlot = "save.corrupt";
        public const string FutureSlot = "save.future";

        private readonly ISaveStorage _storage;
        private readonly SaveMigrator _migrator;
        private string _lastGoodText;   // el último texto que se sabe bueno (leído o escrito): será el respaldo del próximo guardado

        public SaveFile(ISaveStorage storage, SaveMigrator migrator)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
        }

        public SaveLoadResult Load()
        {
            bool hasMain = _storage.TryRead(MainSlot, out string mainText);

            if (hasMain)
            {
                SaveParseResult main = SaveSerializer.Parse(mainText, _migrator);
                if (main.IsUsable)
                {
                    _lastGoodText = mainText;
                    return new SaveLoadResult
                    {
                        Status = main.Status == SaveParseStatus.Migrated ? SaveLoadStatus.Migrated : SaveLoadStatus.Loaded,
                        Data = main.Data,
                        Message = main.Message
                    };
                }

                if (main.Status == SaveParseStatus.Future)
                {
                    // No se intenta con el respaldo: es de la misma época. Se guarda aparte y se empieza de cero.
                    _storage.TryWrite(FutureSlot, mainText);
                    return new SaveLoadResult
                    {
                        Status = SaveLoadStatus.FutureVersionStartedFresh,
                        Message = main.Message + " Se conservó como '" + FutureSlot + "' y se empieza una partida nueva."
                    };
                }

                // Vacío o dañado: se aparta una copia antes de que el próximo guardado lo pise.
                _storage.TryWrite(CorruptSlot, mainText ?? string.Empty);
                return LoadBackup("El archivo principal no se pudo leer (" + main.Message + ").");
            }

            // Sin principal. Si tampoco hay respaldo es una partida nueva de verdad.
            if (!_storage.TryRead(BackupSlot, out _)) return new SaveLoadResult { Status = SaveLoadStatus.NewGame };

            return LoadBackup("Falta el archivo principal.");
        }

        private SaveLoadResult LoadBackup(string reason)
        {
            if (_storage.TryRead(BackupSlot, out string backupText))
            {
                SaveParseResult backup = SaveSerializer.Parse(backupText, _migrator);
                if (backup.IsUsable)
                {
                    _lastGoodText = backupText;
                    return new SaveLoadResult
                    {
                        Status = SaveLoadStatus.RecoveredFromBackup,
                        Data = backup.Data,
                        Message = reason + " Se recuperó el respaldo."
                    };
                }

                return new SaveLoadResult
                {
                    Status = SaveLoadStatus.CorruptStartedFresh,
                    Message = reason + " El respaldo tampoco sirve (" + backup.Message + "): se empieza una partida nueva."
                };
            }

            return new SaveLoadResult
            {
                Status = SaveLoadStatus.CorruptStartedFresh,
                Message = reason + " No hay respaldo: se empieza una partida nueva."
            };
        }

        // Escribe la partida. Devuelve false si no se pudo (la anterior queda como estaba).
        public bool Write(string text)
        {
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("No se puede guardar un texto vacío.", nameof(text));

            // El respaldo es la última partida buena; solo se reescribe si cambió.
            if (_lastGoodText != null) _storage.TryWrite(BackupSlot, _lastGoodText);

            if (!_storage.TryWrite(MainSlot, text)) return false;

            _lastGoodText = text;
            _storage.Flush();
            return true;
        }

        // Borra la partida y su respaldo (los archivos apartados 'corrupt' y 'future' se conservan: no son parte de la partida).
        public void DeleteSave()
        {
            _storage.Delete(MainSlot);
            _storage.Delete(BackupSlot);
            _lastGoodText = null;
            _storage.Flush();
        }
    }
}
