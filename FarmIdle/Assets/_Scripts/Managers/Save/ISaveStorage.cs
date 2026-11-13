namespace FarmIdle
{
    // Dónde se escriben los textos de la partida: archivos en el celular y en la PC, IndexedDB en la web (el sistema de
    // archivos de Unity WebGL vive en memoria hasta que se sincroniza). SaveFile le pide "slots" con nombre (partida,
    // respaldo...) y no sabe cómo se guardan; así la lógica de recuperación se prueba con una versión en memoria.
    public interface ISaveStorage
    {
        // Lee el slot. Devuelve false si no existe o no se pudo leer.
        bool TryRead(string slot, out string content);

        // Escribe el slot de forma segura: si algo falla a mitad de camino, el contenido anterior sigue intacto.
        bool TryWrite(string slot, string content);

        // Borra el slot (no es un error que no exista). Devuelve false si no se pudo borrar.
        bool Delete(string slot);

        // Termina de bajar lo escrito hasta ahora al almacenamiento persistente (en la web, IndexedDB; en lo demás no hace nada).
        void Flush();
    }
}
