using System.Text;

namespace Guncho
{
    /// <summary>
    /// Utility methods for formatting and sanitizing text.
    /// </summary>
    public static class TextUtils
    {
        /// <summary>
        /// Formats a TimeSpan in a human-readable format (e.g., "02d05h", "03h15m", "12m34s").
        /// </summary>
        public static string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.Days >= 1)
                return string.Format("{0:00}d{1:00}h", timeSpan.Days, timeSpan.Hours);
            else if (timeSpan.Hours >= 1)
                return string.Format("{0:00}h{1:00}m", timeSpan.Hours, timeSpan.Minutes);
            else
                return string.Format("{0:00}m{1:00}s", timeSpan.Minutes, timeSpan.Seconds);
        }

        /// <summary>
        /// Sanitizes input by escaping special characters.
        /// </summary>
        public static string Sanitize(string str)
        {
            StringBuilder sb = new StringBuilder(str.Length);
            foreach (char c in str)
            {
                switch (c)
                {
                    case '$': sb.Append("&dollar;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '&': sb.Append("&amp;"); break;
                    case ':': sb.Append("&colon;"); break;
                    case '\n': sb.Append("&nl;"); break;
                    case '\r': break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Combines a player ID with a command to form an input line for a realm.
        /// </summary>
        public static string MakeInputLine(string playerId, string line, bool silent = false)
        {
            return string.Format("{0}{1}:{2}",
                silent ? "$silent " : "",
                playerId,
                line);
        }

        /// <summary>
        /// Reverses sanitization by unescaping special characters.
        /// </summary>
        public static string Desanitize(string str)
        {
            StringBuilder sb = new StringBuilder(str.Length);
            int i = -1, j = -1;
            do
            {
                i = str.IndexOf('&', j + 1);
                if (i >= 0)
                {
                    sb.Append(str.Substring(j + 1, i - j - 1));
                    j = str.IndexOf(';', i + 1);
                    if (j >= 0)
                    {
                        switch (str.Substring(i + 1, j - i - 1))
                        {
                            case "dollar": sb.Append('$'); break;
                            case "lt": sb.Append('<'); break;
                            case "gt": sb.Append('>'); break;
                            case "amp": sb.Append('&'); break;
                            case "colon": sb.Append(':'); break;
                            case "nl": sb.Append('\n'); break;
                            default: sb.Append(str.Substring(i, j - i + 1)); break;
                        }
                    }
                    else
                    {
                        sb.Append(str.Substring(i));
                        break;
                    }
                }
                else
                {
                    sb.Append(str.Substring(j + 1));
                }
            }
            while (i >= 0);
            return sb.ToString();
        }
    }
}
