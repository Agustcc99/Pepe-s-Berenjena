namespace FarmIdle.EditorTools
{
    // Rutas de las escenas del build, en el orden en que se cargan: Bootstrap primero.
    internal static class ScenePaths
    {
        public const string Folder = "Assets/Scenes";
        public const string Bootstrap = Folder + "/" + SceneNames.Bootstrap + ".unity";
        public const string MainLevel = Folder + "/" + SceneNames.MainLevel + ".unity";

        public static readonly string[] All = { Bootstrap, MainLevel };
    }
}
