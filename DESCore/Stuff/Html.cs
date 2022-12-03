#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.DE.Stuff
{
    /// <summary>Simple html helper.</summary>
    public static class Html
    {
        #region -- HtmlToText ---------------------------------------------------------

        #region -- enum HtmlTagTyp ----------------------------------------------------

        private enum HtmlTagTyp
        {
            Start,
            End,
            Empty
        } // enum HtmlTagTyp

        #endregion

        #region -- class HtmlTextOutput -----------------------------------------------

        private sealed class HtmlTextOutput
        {
            private TextWriter tw;
            private bool isLineEmpty = true;

            public HtmlTextOutput(TextWriter tw)
            {
                this.tw = tw ?? throw new ArgumentNullException(nameof(tw));
            } // ctor

            public void AppendChar(char c)
            {
                tw.Write(c);
                isLineEmpty = false;
            } // proc AppendChar

            public void AppendNewLine(bool softNewLine)
            {
                if (!softNewLine || !isLineEmpty)
                {
                    tw.WriteLine();
                    isLineEmpty = true;
                }
            } // proc AppendNewLine

            public void AppendWhitespace()
            {
                if (!isLineEmpty)
                    tw.Write(' ');
            } // proc AppendWhitespace

            public void AppendTextTag(string htmlText, int tagStart, int tagEnd)
            {
                var tagName = htmlText.Substring(tagStart, tagEnd - tagStart);
                if (String.Compare(tagName, "p", true) == 0)
                    AppendNewLine(true);
                if (String.Compare(tagName, "br", true) == 0)
                    AppendNewLine(false);
            } // func AppendTextTag

            public bool AppendHtmlChar(string charText)
            {
                if (TryConvertHtmlChar(charText, out var c))
                {
                    AppendChar(c);
                    return true;
                }
                else
                    return false;
            } // proc AppendHtmlChar
        } // class HtmlTextOutput

        #endregion

        #region -- Html-Chars ---------------------------------------------------------

        private static readonly KeyValuePair<string, int>[] htmlChars = new KeyValuePair<string, int>[] {
              new KeyValuePair<string, int>("quot", 34),
              new KeyValuePair<string, int>("amp", 38),
              new KeyValuePair<string, int>("lt", 60),
              new KeyValuePair<string, int>("gt", 62),

              new KeyValuePair<string, int>("nbsp", 160),
              new KeyValuePair<string, int>("iexcl", 161),
              new KeyValuePair<string, int>("cent", 162),
              new KeyValuePair<string, int>("pound", 163),
              new KeyValuePair<string, int>("curren", 164),
              new KeyValuePair<string, int>("yen", 165),
              new KeyValuePair<string, int>("brvbar", 166),
              new KeyValuePair<string, int>("sect", 167),
              new KeyValuePair<string, int>("uml", 168),
              new KeyValuePair<string, int>("copy", 169),
              new KeyValuePair<string, int>("ordf", 170),
              new KeyValuePair<string, int>("laquo", 171),
              new KeyValuePair<string, int>("not", 172),
              new KeyValuePair<string, int>("shy", 173),
              new KeyValuePair<string, int>("reg", 174),
              new KeyValuePair<string, int>("macr", 175),
              new KeyValuePair<string, int>("deg", 176),
              new KeyValuePair<string, int>("plusmn", 177),
              new KeyValuePair<string, int>("sup2", 178),
              new KeyValuePair<string, int>("sup3", 179),
              new KeyValuePair<string, int>("acute", 180),
              new KeyValuePair<string, int>("micro", 181),
              new KeyValuePair<string, int>("para", 182),
              new KeyValuePair<string, int>("middot", 183),
              new KeyValuePair<string, int>("cedil", 184),
              new KeyValuePair<string, int>("sup1", 185),
              new KeyValuePair<string, int>("ordm", 186),
              new KeyValuePair<string, int>("raquo", 187),
              new KeyValuePair<string, int>("frac14", 188),
              new KeyValuePair<string, int>("frac12", 189),
              new KeyValuePair<string, int>("frac34", 190),
              new KeyValuePair<string, int>("iquest", 191),
              new KeyValuePair<string, int>("Agrave", 192),
              new KeyValuePair<string, int>("Aacute", 193),
              new KeyValuePair<string, int>("Acirc", 194),
              new KeyValuePair<string, int>("Atilde", 195),
              new KeyValuePair<string, int>("Auml", 196),
              new KeyValuePair<string, int>("Aring", 197),
              new KeyValuePair<string, int>("AElig", 198),
              new KeyValuePair<string, int>("Ccedil", 199),
              new KeyValuePair<string, int>("Egrave", 200),
              new KeyValuePair<string, int>("Eacute", 201),
              new KeyValuePair<string, int>("Ecirc", 202),
              new KeyValuePair<string, int>("Euml", 203),
              new KeyValuePair<string, int>("Igrave", 204),
              new KeyValuePair<string, int>("Iacute", 205),
              new KeyValuePair<string, int>("Icirc", 206),
              new KeyValuePair<string, int>("Iuml", 207),
              new KeyValuePair<string, int>("ETH", 208),
              new KeyValuePair<string, int>("Ntilde", 209),
              new KeyValuePair<string, int>("Ograve", 210),
              new KeyValuePair<string, int>("Oacute", 211),
              new KeyValuePair<string, int>("Ocirc", 212),
              new KeyValuePair<string, int>("Otilde", 213),
              new KeyValuePair<string, int>("Ouml", 214),
              new KeyValuePair<string, int>("times", 215),
              new KeyValuePair<string, int>("Oslash", 216),
              new KeyValuePair<string, int>("Ugrave", 217),
              new KeyValuePair<string, int>("Uacute", 218),
              new KeyValuePair<string, int>("Ucirc", 219),
              new KeyValuePair<string, int>("Uuml", 220),
              new KeyValuePair<string, int>("Yacute", 221),
              new KeyValuePair<string, int>("THORN", 222),
              new KeyValuePair<string, int>("szlig", 223),
              new KeyValuePair<string, int>("agrave", 224),
              new KeyValuePair<string, int>("aacute", 225),
              new KeyValuePair<string, int>("acirc", 226),
              new KeyValuePair<string, int>("atilde", 227),
              new KeyValuePair<string, int>("auml", 228),
              new KeyValuePair<string, int>("aring", 229),
              new KeyValuePair<string, int>("aelig", 230),
              new KeyValuePair<string, int>("ccedil", 231),
              new KeyValuePair<string, int>("egrave", 232),
              new KeyValuePair<string, int>("eacute", 233),
              new KeyValuePair<string, int>("ecirc", 234),
              new KeyValuePair<string, int>("euml", 235),
              new KeyValuePair<string, int>("igrave", 236),
              new KeyValuePair<string, int>("iacute", 237),
              new KeyValuePair<string, int>("icirc", 238),
              new KeyValuePair<string, int>("iuml", 239),
              new KeyValuePair<string, int>("eth", 240),
              new KeyValuePair<string, int>("ntilde", 241),
              new KeyValuePair<string, int>("ograve", 242),
              new KeyValuePair<string, int>("oacute", 243),
              new KeyValuePair<string, int>("ocirc", 244),
              new KeyValuePair<string, int>("otilde", 245),
              new KeyValuePair<string, int>("ouml", 246),
              new KeyValuePair<string, int>("divide", 247),
              new KeyValuePair<string, int>("oslash", 248),
              new KeyValuePair<string, int>("ugrave", 249),
              new KeyValuePair<string, int>("uacute", 250),
              new KeyValuePair<string, int>("ucirc", 251),
              new KeyValuePair<string, int>("uuml", 252),
              new KeyValuePair<string, int>("yacute", 253),
              new KeyValuePair<string, int>("thorn", 254),
              new KeyValuePair<string, int>("yuml", 255),

              new KeyValuePair<string, int>("Alpha", 913),
              new KeyValuePair<string, int>("alpha", 945),
              new KeyValuePair<string, int>("Beta", 914),
              new KeyValuePair<string, int>("beta", 946),
              new KeyValuePair<string, int>("Gamma", 915),
              new KeyValuePair<string, int>("gamma", 947),
              new KeyValuePair<string, int>("Delta", 916),
              new KeyValuePair<string, int>("delta", 948),
              new KeyValuePair<string, int>("Epsilon", 917),
              new KeyValuePair<string, int>("epsilon", 949),
              new KeyValuePair<string, int>("Zeta", 918),
              new KeyValuePair<string, int>("zeta", 950),
              new KeyValuePair<string, int>("Eta", 919),
              new KeyValuePair<string, int>("eta", 951),
              new KeyValuePair<string, int>("Theta", 920),
              new KeyValuePair<string, int>("theta", 952),
              new KeyValuePair<string, int>("Iota", 921),
              new KeyValuePair<string, int>("iota", 953),
              new KeyValuePair<string, int>("Kappa", 922),
              new KeyValuePair<string, int>("kappa", 954),
              new KeyValuePair<string, int>("Lambda", 923),
              new KeyValuePair<string, int>("lambda", 955),
              new KeyValuePair<string, int>("Mu", 924),
              new KeyValuePair<string, int>("mu", 956),
              new KeyValuePair<string, int>("Nu", 925),
              new KeyValuePair<string, int>("nu", 957),
              new KeyValuePair<string, int>("Xi", 926),
              new KeyValuePair<string, int>("xi", 958),
              new KeyValuePair<string, int>("Omicron", 927),
              new KeyValuePair<string, int>("omicron", 959),
              new KeyValuePair<string, int>("Pi", 928),
              new KeyValuePair<string, int>("pi", 960),
              new KeyValuePair<string, int>("Rho", 929),
              new KeyValuePair<string, int>("rho", 961),
              new KeyValuePair<string, int>("Sigma", 931),
              new KeyValuePair<string, int>("sigmaf", 962),
              new KeyValuePair<string, int>("sigma", 963),
              new KeyValuePair<string, int>("Tau", 932),
              new KeyValuePair<string, int>("tau", 964),
              new KeyValuePair<string, int>("Upsilon", 933),
              new KeyValuePair<string, int>("upsilon", 965),
              new KeyValuePair<string, int>("Phi", 934),
              new KeyValuePair<string, int>("phi", 966),
              new KeyValuePair<string, int>("Chi", 935),
              new KeyValuePair<string, int>("chi", 967),
              new KeyValuePair<string, int>("Psi", 936),
              new KeyValuePair<string, int>("psi", 968),
              new KeyValuePair<string, int>("Omega", 937),
              new KeyValuePair<string, int>("omega", 969),
              new KeyValuePair<string, int>("thetasym", 977),
              new KeyValuePair<string, int>("upsih", 978),
              new KeyValuePair<string, int>("piv", 982),

              new KeyValuePair<string, int>("forall", 8704),
              new KeyValuePair<string, int>("part", 8706),
              new KeyValuePair<string, int>("exist", 8707),
              new KeyValuePair<string, int>("empty", 8709),
              new KeyValuePair<string, int>("nabla", 8711),
              new KeyValuePair<string, int>("isin", 8712),
              new KeyValuePair<string, int>("notin", 8713),
              new KeyValuePair<string, int>("ni", 8715),
              new KeyValuePair<string, int>("prod", 8719),
              new KeyValuePair<string, int>("sum", 8721),
              new KeyValuePair<string, int>("minus", 8722),
              new KeyValuePair<string, int>("lowast", 8727),
              new KeyValuePair<string, int>("radic", 8730),
              new KeyValuePair<string, int>("prop", 8733),
              new KeyValuePair<string, int>("infin", 8734),
              new KeyValuePair<string, int>("ang", 8736),
              new KeyValuePair<string, int>("and", 8743),
              new KeyValuePair<string, int>("or", 8744),
              new KeyValuePair<string, int>("cap", 8745),
              new KeyValuePair<string, int>("cup", 8746),
              new KeyValuePair<string, int>("int", 8747),
              new KeyValuePair<string, int>("there4", 8756),
              new KeyValuePair<string, int>("sim", 8764),
              new KeyValuePair<string, int>("cong", 8773),
              new KeyValuePair<string, int>("asymp", 8776),
              new KeyValuePair<string, int>("ne", 8800),
              new KeyValuePair<string, int>("equiv", 8801),
              new KeyValuePair<string, int>("le", 8804),
              new KeyValuePair<string, int>("ge", 8805),
              new KeyValuePair<string, int>("sub", 8834),
              new KeyValuePair<string, int>("sup", 8835),
              new KeyValuePair<string, int>("nsub", 8836),
              new KeyValuePair<string, int>("sube", 8838),
              new KeyValuePair<string, int>("supe", 8839),
              new KeyValuePair<string, int>("oplus", 8853),
              new KeyValuePair<string, int>("otimes", 8855),
              new KeyValuePair<string, int>("perp", 8869),
              new KeyValuePair<string, int>("sdot", 8901),
              new KeyValuePair<string, int>("loz", 9674),

              new KeyValuePair<string, int>("lceil", 8968),
              new KeyValuePair<string, int>("rceil", 8969),
              new KeyValuePair<string, int>("lfloor", 8970),
              new KeyValuePair<string, int>("rfloor", 8971),
              new KeyValuePair<string, int>("lang", 9001),
              new KeyValuePair<string, int>("rang", 9002),

              new KeyValuePair<string, int>("larr", 8592),
              new KeyValuePair<string, int>("uarr", 8593),
              new KeyValuePair<string, int>("rarr", 8594),
              new KeyValuePair<string, int>("darr", 8595),
              new KeyValuePair<string, int>("harr", 8596),
              new KeyValuePair<string, int>("crarr", 8629),
              new KeyValuePair<string, int>("lArr", 8656),
              new KeyValuePair<string, int>("uArr", 8657),
              new KeyValuePair<string, int>("rArr", 8658),
              new KeyValuePair<string, int>("dArr", 8659),
              new KeyValuePair<string, int>("hArr", 8660),

              new KeyValuePair<string, int>("bull", 8226),
              new KeyValuePair<string, int>("prime", 8242),
              new KeyValuePair<string, int>("Prime", 8243),
              new KeyValuePair<string, int>("oline", 8254),
              new KeyValuePair<string, int>("frasl", 8260),
              new KeyValuePair<string, int>("weierp", 8472),
              new KeyValuePair<string, int>("image", 8465),
              new KeyValuePair<string, int>("real", 8476),
              new KeyValuePair<string, int>("trade", 8482),
              new KeyValuePair<string, int>("euro", 8364),
              new KeyValuePair<string, int>("alefsym", 8501),
              new KeyValuePair<string, int>("spades", 9824),
              new KeyValuePair<string, int>("clubs", 9827),
              new KeyValuePair<string, int>("hearts", 9829),
              new KeyValuePair<string, int>("diams", 9830),

              new KeyValuePair<string, int>("OElig", 338),
              new KeyValuePair<string, int>("oelig", 339),
              new KeyValuePair<string, int>("Scaron", 352),
              new KeyValuePair<string, int>("scaron", 353),
              new KeyValuePair<string, int>("Yuml", 376),
              new KeyValuePair<string, int>("fnof", 402),

              new KeyValuePair<string, int>("ensp", 8194),
              new KeyValuePair<string, int>("emsp", 8195),
              new KeyValuePair<string, int>("thinsp", 8201),
              new KeyValuePair<string, int>("zwnj", 8204),
              new KeyValuePair<string, int>("zwj", 8205),
              new KeyValuePair<string, int>("lrm", 8206),
              new KeyValuePair<string, int>("rlm", 8207),
              new KeyValuePair<string, int>("ndash", 8211),
              new KeyValuePair<string, int>("mdash", 8212),
              new KeyValuePair<string, int>("lsquo", 8216),
              new KeyValuePair<string, int>("rsquo", 8217),
              new KeyValuePair<string, int>("sbquo", 8218),
              new KeyValuePair<string, int>("ldquo", 8220),
              new KeyValuePair<string, int>("rdquo", 8221),
              new KeyValuePair<string, int>("bdquo", 8222),
              new KeyValuePair<string, int>("dagger", 8224),
              new KeyValuePair<string, int>("Dagger", 8225),
              new KeyValuePair<string, int>("hellip", 8230),
              new KeyValuePair<string, int>("permil", 8240),
              new KeyValuePair<string, int>("lsaquo", 8249),
              new KeyValuePair<string, int>("rsaquo", 8250),

              new KeyValuePair<string, int>("circ", 710),
              new KeyValuePair<string, int>("tilde", 732),
        };

		#endregion

		/// <summary></summary>
		/// <param name="charText"></param>
		/// <param name="charValue"></param>
		/// <returns></returns>
		public static bool TryConvertHtmlChar(string charText, out char charValue)
        {
            if (String.IsNullOrEmpty(charText))
            {
                charValue = '\0';
                return false;
            }

            var value = -1;
            var idx = Array.FindIndex(htmlChars, c => c.Key == charText);
            if (idx >= 0)
            {
                value = htmlChars[idx].Value;
            }
            else if (charText[0] == '#' && charText.Length > 1)
            {
                if (charText[1] == 'x')
                {
                    if (!Int32.TryParse(charText.Substring(2), NumberStyles.AllowHexSpecifier, null, out value))
                        value = -1;
                }
                else if (!Int32.TryParse(charText.Substring(1), out value))
                    value = -1;
            }

            if (value > 0)
            {
                try
                {
                    charValue = Convert.ToChar(value);
                    return true;
                }
                catch
                {
                    charValue = '\0';
                    return false;
                }
            }
            else
            {
                charValue = '\0';
                return false;
            }
        } // func TryConvertHtmlChar

        /// <summary>Convert a html-formatted content to a plain text.</summary>
        /// <param name="htmlText"></param>
        /// <returns></returns>
        public static string HtmlToText(string htmlText)
        {
            if (String.IsNullOrEmpty(htmlText))
                return htmlText;

            var length = htmlText.Length;
            using (var tw = new StringWriter(new StringBuilder(length * 80 / 100)))
            {
                var i = 0;
                var o = new HtmlTextOutput(tw);

                var state = 0;
                var tagStart = 0;
                var tagEnd = 0;
                //var isCloseTag = false;

                RedoLoop:
                while (i < length)
                {
                    var c = htmlText[i];

                    switch (state)
                    {
                        case 0:
                            if (Char.IsWhiteSpace(c))
                                state = 1;
                            else if (c == '<')
                            {
                                tagStart = i + 1;
                                //isCloseTag = false;
                                state = 10;
                            }
                            else if (c == '>') // Ignoriere
                            { }
                            else if (c == '&')
                            {
                                tagStart = i + 1;
                                state = 20;
                            }
                            else
                                o.AppendChar(c);
                            break;
                        case 1: // Überspringe Leerräume
                            if (!Char.IsWhiteSpace(c))
                            {
                                o.AppendWhitespace();
                                state = 0;
                                goto case 0;
                            }
                            break;
                        case 10:
                            if (c == '/')
                            {
                                tagStart++;
                                //isCloseTag = true;
                                state = 11;
                            }
                            else
                            {
                                state = 11;
                                goto case 11;
                            }
                            break;
                        case 11:
                            if (!Char.IsLetter(c))
                            {
                                tagEnd = i;
                                state = 12;
                                goto case 12;
                            }
                            break;
                        case 12:
                            if (c == '/') // EmptyTag
                                state = 13;
                            else if (c == '>')
                            {
                                o.AppendTextTag(htmlText, tagStart, tagEnd);
                                state = 0;
                            }
                            break;
                        case 13:
                            if (c == '>')
                            {
                                o.AppendTextTag(htmlText, tagStart, tagEnd);
                                state = 0;
                            }
                            break;
                        case 20:
                            if (c == ';')
                            {
                                if (!o.AppendHtmlChar(htmlText.Substring(tagStart, i - tagStart)))
                                {
                                    i = tagStart;
                                    o.AppendChar('&');
                                }
                                state = 0;
                            }
                            break;
                    }
                    i++;
                } // while Read

                if (state == 20)
                {
                    i = tagStart;
                    state = 0;
                    o.AppendChar('&');
                    goto RedoLoop;
                }
                
                return tw.GetStringBuilder().ToString();
            }
        } // func HtmlToText

		#endregion

		#region -- TextToHtml ---------------------------------------------------------

		private static bool TryConvertHtmlChar(char c, out string charText)
		{
			var idx = Array.FindIndex(htmlChars, kv => kv.Value == (int)c);
			if (idx == -1)
			{
				charText = null;
				return false;
			}
			else
			{
				charText = htmlChars[idx].Key;
				return true;
			}
		} // func TryConvertHtmlChar

		/// <summary>Converts a char to text.</summary>
		/// <param name="c"></param>
		/// <returns></returns>
		public static string ConvertHtmlChar(char c)
			=> TryConvertHtmlChar(c, out var charText) ? '&' + charText + ';' : new String(c, 1);

		/// <summary></summary>
		/// <param name="sb"></param>
		/// <param name="text"></param>
		public static StringBuilder TextToHtml(this StringBuilder sb, string text)
		{
			if (text != null)
			{
				for (var i = 0; i < text.Length; i++)
				{
					if (TryConvertHtmlChar(text[i], out var charText))
						sb.Append('&').Append(charText).Append(';');
					else
						sb.Append(text[i]);
				}
			}
			return sb;
		} // proc TextToHtml

		#endregion
	} // class Html
}
