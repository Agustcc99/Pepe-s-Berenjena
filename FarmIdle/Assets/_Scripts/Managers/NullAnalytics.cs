namespace FarmIdle
{
    // Sin analítica: no registra nada ni cuesta nada. Es el servicio de los builds finales mientras no haya un SDK.
    public sealed class NullAnalytics : IAnalytics
    {
        public string Name => "Null";

        public void Log(string eventName) { }

        public void Log(string eventName, string key, string value) { }

        public void Log(string eventName, string key, long value) { }
    }
}
