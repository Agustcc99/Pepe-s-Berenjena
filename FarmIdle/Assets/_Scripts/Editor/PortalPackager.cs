using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.EditorTools
{
    // Arma la variante CrazyGames del build web (ROADMAP D-03): una copia de la carpeta del build con el SDK del portal y el adaptador
    // (WebPortals/crazygames-adapter.js) ya enchufados en el index.html, lista para subir. El build común no se toca: sigue sirviendo para
    // probar y para otros hostings (sin adaptador no muestra anuncios).
    //   Menú:    FarmIdle > Release > Pack CrazyGames (desde el último Web)
    //   Consola: Unity.exe -batchmode -projectPath <ruta> -quit -executeMethod FarmIdle.EditorTools.PortalPackager.PackLastBuilds
    //   BuildWeb lo llama solo al terminar (ReleaseBuilder).
    // El SDK NO se descarga ni se copia: el index.html lo pide a sdk.crazygames.com, como indica la documentación del portal. Por eso la
    // variante no suma peso al build.
    public static class PortalPackager
    {
        public const string CrazyGamesSdkUrl = "https://sdk.crazygames.com/crazygames-sdk-v3.js";
        public const string CrazyGamesAdapterFile = "crazygames-adapter.js";
        public const string Suffix = "-CrazyGames";

        private const string AdapterSource = "WebPortals/" + CrazyGamesAdapterFile;

        // Mete el SDK y el adaptador al final del <head>: el SDK primero (bloquea hasta cargar) y el adaptador después, los dos ANTES del
        // loader de Unity, que está en el <body>. Si ya están, no se repiten.
        public static string PatchIndexHtml(string html)
        {
            if (html == null) throw new ArgumentNullException(nameof(html));
            if (html.Contains(CrazyGamesAdapterFile)) return html;

            int head = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
            if (head < 0) throw new InvalidOperationException("El index.html no tiene </head>: no se puede enchufar el SDK de CrazyGames.");

            string tags = "    <script src=\"" + CrazyGamesSdkUrl + "\"></script>\n"
                        + "    <script src=\"" + CrazyGamesAdapterFile + "\"></script>\n  ";
            return html.Insert(head, tags);
        }

        // Copia 'webFolder' (un build web) a '<webFolder>-CrazyGames' y le enchufa el SDK. Devuelve la carpeta nueva.
        public static string Pack(string webFolder)
        {
            string indexPath = Path.Combine(webFolder, "index.html");
            if (!File.Exists(indexPath)) throw new FileNotFoundException("No hay un build web en " + webFolder + " (falta index.html).");
            if (!File.Exists(AdapterSource)) throw new FileNotFoundException("Falta el adaptador " + AdapterSource);

            string target = webFolder.TrimEnd('/', '\\') + Suffix;
            if (Directory.Exists(target)) Directory.Delete(target, true);
            CopyDirectory(webFolder, target);

            string targetIndex = Path.Combine(target, "index.html");
            File.WriteAllText(targetIndex, PatchIndexHtml(File.ReadAllText(targetIndex)));
            File.Copy(AdapterSource, Path.Combine(target, CrazyGamesAdapterFile), true);

            Debug.Log("[FarmIdle] Variante CrazyGames lista en " + target + " (SDK desde " + CrazyGamesSdkUrl + ").");
            return target;
        }

        [MenuItem("FarmIdle/Release/Pack CrazyGames (desde el último Web)")]
        public static void PackLastBuilds()
        {
            int packed = 0;
            foreach (string folder in new[] { "Builds/Release/Web", "Builds/Release/Web-Gzip" })
            {
                if (!File.Exists(Path.Combine(folder, "index.html"))) continue;

                Pack(folder);
                packed++;
            }

            if (packed == 0) Debug.LogError("[FarmIdle] No hay un build web de release: primero FarmIdle > Release > Build Web.");
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            }

            foreach (string directory in Directory.GetDirectories(source))
            {
                CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
            }
        }
    }
}
