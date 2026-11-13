#if UNITY_ANDROID && FARMIDLE_ADMOB
using System.IO;
using UnityEditor.Android;

namespace FarmIdle.EditorTools
{
    // Las dependencias de Android del plugin de AdMob (Google Mobile Ads 11.6.0), resueltas por Gradle y sin el External Dependency
    // Manager (EDM): el plugin trae su .aar pero no sus librerías de Google (play-services-ads y tres de AndroidX). Salen de
    // GoogleMobileAdsDependencies.xml del propio plugin; si se actualiza el plugin hay que revisar estas versiones. Gradle las baja de
    // maven.google.com con la configuración que ya funciona en esta PC (~/.gradle/gradle.properties con el almacén de Windows).
    // Se agregan al build.gradle de unityLibrary una sola vez por proyecto exportado.
    internal sealed class AdMobGradleDependencies : IPostGenerateGradleAndroidProject
    {
        private const string Marker = "// FarmIdle: dependencias de AdMob";

        public int callbackOrder => 20;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // 'path' es la carpeta unityLibrary del proyecto Gradle exportado.
            string gradleFile = Path.Combine(path, "build.gradle");
            if (!File.Exists(gradleFile)) return;

            string contents = File.ReadAllText(gradleFile);
            if (contents.Contains(Marker)) return;

            contents += "\n" + Marker + "\n" +
                "dependencies {\n" +
                "    implementation 'com.google.android.gms:play-services-ads:25.5.0'\n" +
                "    implementation 'androidx.constraintlayout:constraintlayout:2.1.4'\n" +
                "    implementation 'androidx.lifecycle:lifecycle-process:2.6.2'\n" +
                "    implementation 'androidx.fragment:fragment:1.7.1'\n" +
                "}\n";
            File.WriteAllText(gradleFile, contents);
        }
    }
}
#endif
