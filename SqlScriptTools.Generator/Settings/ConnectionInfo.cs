namespace SqlScriptTools.Generator.Settings
{
    public sealed class ConnectionInfo
    {
        public string Server { get; set; }
        public string[] Databases { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }

    }
}
