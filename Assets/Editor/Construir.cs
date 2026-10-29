using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// El ejecutable del juego (entrega 3.9), como en el video de apoyo (File > Build Settings > Build), pero desde un menú o
// desde la terminal: las escenas activas de Build Settings, la plataforma de este equipo (macOS) y la carpeta Builds/, que
// no se sube al repositorio (.gitignore). Lo que sale corre solo, sin el editor de Unity.
//   Menú: Recolector > Construir el ejecutable para macOS
//   Terminal: Unity -batchmode -quit -projectPath <proyecto> -executeMethod Construir.MacOS [-salida <ruta del .app>]
public static class Construir
{
    const string Salida = "Builds/macOS/El recolector.app";

    [MenuItem("Recolector/Construir el ejecutable para macOS")]
    public static void MacOS()
    {
        string salida = Argumento("-salida") ?? Salida;
        var escenas = EditorBuildSettings.scenes.Where(e => e.enabled).Select(e => e.path).ToArray();
        var reporte = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = escenas,
            locationPathName = salida,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        });
        var r = reporte.summary;
        Debug.Log("CONSTRUIR " + r.result + " escenas=" + string.Join(",", escenas) + " salida=" + Path.GetFullPath(salida)
            + " tamano=" + (r.totalSize / 1048576.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "MB errores="
            + r.totalErrors + " avisos=" + r.totalWarnings + " tiempo=" + r.totalTime);
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(r.result == BuildResult.Succeeded ? 0 : 1);
        }
    }

    static string Argumento(string nombre)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, nombre);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
