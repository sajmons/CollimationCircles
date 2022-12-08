namespace CollimationCircles.Extensions
{
    public static class StringExtenstions
    {
        public static string F(this string value, params object[] args)
        {
            return string.Format(value, args);
        }
    }
}
