using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.IO;
using System.Text.RegularExpressions;

namespace ControlPanel
{
    [DefaultProperty("Text")]
    [ToolboxData("<{0}:InformHtml runat=\"server\" />")]
    public class InformHtml : WebControl
    {
        private string filename;

        public string FileName
        {
            get { return filename; }
            set { filename = value; }
        }

        private static Regex bodyRegex = new Regex(@"\<body\>(.*)\</body\>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static Regex informUrlRegex = new Regex(@"(?<=(?:src|href)=)""?(?:inform:)?([^ >""]*)""?(?=[ >])");

        protected override void RenderContents(HtmlTextWriter output)
        {
            if (File.Exists(filename))
            {
                string text = File.ReadAllText(filename);

                Match m = bodyRegex.Match(text);
                if (m.Success)
                    text = m.Groups[1].Value;

                text = informUrlRegex.Replace(text, TranslateUrlMatch);

                output.Write(text);
            }
            else
            {
                output.Write("<i>Missing index file</i>");
            }
        }

        private string TranslateUrlMatch(Match m)
        {
            string uri = m.Groups[1].Value;

            if (uri.StartsWith("#"))
                return uri;

            if (uri.EndsWith(".png"))
            {
                string url = "/images/" + Path.GetFileName(uri);
                string physPath = Page.Server.MapPath("~" + url);
                if (File.Exists(physPath))
                    return url;
            }

            if (uri.StartsWith("/doc") && uri.EndsWith(".html"))
                return "http://www.inform-fiction.org/I7" + uri;

            // TODO: source navigation links
            if (uri.StartsWith("source:"))
                return "\"javascript:alert('Not implemented.')\"";

            if (uri.EndsWith(".html"))
            {
                if (uri.StartsWith("../"))
                    uri = uri.Substring(3);
                return string.Format("ViewIndex.aspx?realm={0}&file={1}",
                    HttpUtility.UrlEncode(Page.Request.Params["realm"]),
                    HttpUtility.UrlEncode(uri));
            }

            return "inform:" + uri;
        }
    }
}
