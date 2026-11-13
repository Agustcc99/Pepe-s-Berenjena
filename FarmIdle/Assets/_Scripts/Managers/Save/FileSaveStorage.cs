using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace FarmIdle
{
    // Los slots como archivos en una carpeta (Application.persistentDataPath: en Android es el almacenamiento privado de la
    // app). Escribe de forma segura: primero un archivo temporal completo y recién después lo pone en lugar del
    // anterior, así un corte a mitad de escritura nunca deja un archivo a medias.
    // En WebGL esa carpeta es un sistema de archivos en memoria que hay que subir a IndexedDB: Flush lo hace (WebPersistence).
    public sealed class FileSaveStorage : ISaveStorage
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);   // sin marca BOM

        private readonly string _directory;

        public FileSaveStorage(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Hace falta una carpeta.", nameof(directory));

            _directory = directory;
        }

        public string Directory => _directory;

        public string PathOf(string slot)
        {
            return Path.Combine(_directory, slot + ".json");
        }

        public bool TryRead(string slot, out string content)
        {
            content = null;
            try
            {
                string path = PathOf(slot);
                if (!File.Exists(path)) return false;

                content = File.ReadAllText(path, Utf8);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FarmIdle] No se pudo leer '" + slot + "': " + e.Message);
                content = null;
                return false;
            }
        }

        public bool TryWrite(string slot, string content)
        {
            string path = PathOf(slot);
            string temp = path + ".tmp";

            try
            {
                System.IO.Directory.CreateDirectory(_directory);
                File.WriteAllText(temp, content ?? string.Empty, Utf8);

                if (!File.Exists(path))
                {
                    File.Move(temp, path);
                    return true;
                }

                try
                {
                    File.Replace(temp, path, null);   // reemplazo atómico donde el sistema lo soporta
                }
                catch (Exception)
                {
                    // Plan B (File.Replace no existe en todas las plataformas): el temporal ya está completo, así que si algo
                    // se corta entre borrar y mover, queda el temporal entero y el respaldo cubre la partida anterior.
                    File.Delete(path);
                    File.Move(temp, path);
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FarmIdle] No se pudo escribir '" + slot + "': " + e.Message);
                TryDeleteQuietly(temp);
                return false;
            }
        }

        public bool Delete(string slot)
        {
            try
            {
                string path = PathOf(slot);
                if (File.Exists(path)) File.Delete(path);
                TryDeleteQuietly(path + ".tmp");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FarmIdle] No se pudo borrar '" + slot + "': " + e.Message);
                return false;
            }
        }

        public void Flush()
        {
            WebPersistence.SyncFileSystem();
        }

        private static void TryDeleteQuietly(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception)
            {
                // Un temporal que no se puede borrar no importa: el próximo guardado lo pisa.
            }
        }
    }
}
