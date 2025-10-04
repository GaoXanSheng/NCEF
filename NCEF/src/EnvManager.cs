using System;

namespace NCEF
{
    #region EnvManager
    public static class EnvManager
    {
        public static string GetString(string name, string defaultValue = "")
        {
            string value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        public static int GetInt(string name, int defaultValue = 0)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return int.TryParse(value, out int result) ? result : defaultValue;
        }
    }
    #endregion

}