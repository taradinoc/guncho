using Microsoft.Owin;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Guncho.Api
{
    public class ApplyProxyHeadersMiddleware : OwinMiddleware
    {
        public ApplyProxyHeadersMiddleware(OwinMiddleware next)
            : base(next)
        {
        }

        private static bool ShouldUseProxyHeaders(IOwinContext context)
        {
            switch (context.Request.RemoteIpAddress)
            {
                case "127.0.0.1":
                case "::1":
                    return true;

                default:
                    return false;
            }
        }

        public async override Task Invoke(IOwinContext context)
        {
            if (ShouldUseProxyHeaders(context))
            {
                var request = context.Request;
                var headers = request.Headers;

                // scheme
                string scheme;

                if (string.Equals(GetLastHeader(headers, "X-Forwarded-Ssl"), "on", StringComparison.OrdinalIgnoreCase))
                {
                    scheme = "https";
                }
                else
                {
                    scheme = GetLastHeader(headers, "X-Forwarded-Scheme");

                    if (string.IsNullOrWhiteSpace(scheme))
                    {
                        scheme = GetLastHeader(headers, "X-Forwarded-Proto");
                    }

                    if (string.IsNullOrWhiteSpace(scheme))
                    {
                        scheme = request.Scheme;
                    }
                }

                // host
                string host = GetLastHeader(headers, "X-Forwarded-Host");

                if (string.IsNullOrWhiteSpace(host))
                {
                    host = headers.Get("Host");
                }

                if (string.IsNullOrWhiteSpace(host))
                {
                    host = request.LocalIpAddress ?? "localhost";

                    if (request.LocalPort.HasValue && request.LocalPort != 80)
                    {
                        host = host + ":" + request.LocalPort;
                    }
                }

                // update request headers
                request.Scheme = scheme;

                /* This doesn't work by default:
                 * 
                 *    request.Host = new HostString(host);
                 * 
                 * ...because the host header is ultimately read from
                 * Microsoft.Owin.Host.HttpListener.RequestProcessing.RequestHeadersDictionary,
                 * which doesn't use the value that request.Host sets. Setting the value it
                 * actually uses is infeasible, so we'll just replace the header dictionary
                 * with a wrapper that will use the value we set.
                 */

                const string OwinRequestHeaders = "owin.RequestHeaders";
                var requestHeaders = (IDictionary<string, string[]>)context.Environment[OwinRequestHeaders];
                if (!(requestHeaders is WrappedRequestHeaders))
                {
                    context.Environment[OwinRequestHeaders] = requestHeaders = new WrappedRequestHeaders(requestHeaders);
                }
                request.Host = new HostString(host);
            }

            await Next.Invoke(context);
        }

        private static string GetLastHeader(IHeaderDictionary headers, string key)
        {
            var parts = headers.GetCommaSeparatedValues(key);

            if (parts == null || parts.Count == 0)
            {
                return null;
            }
            else
            {
                return parts[parts.Count - 1];
            }
        }

        private class WrappedRequestHeaders : IDictionary<string, string[]>
        {
            private readonly IDictionary<string, string[]> inner;
            private readonly IDictionary<string, string[]> outer;

            public WrappedRequestHeaders(IDictionary<string, string[]> inner)
            {
                Contract.Requires(inner != null);

                this.inner = inner;
                this.outer = new Dictionary<string, string[]>();
            }

            #region IDictionary<string,string[]> Members

            public void Add(string key, string[] value)
            {
                outer.Add(key, value);
            }

            public bool ContainsKey(string key)
            {
                return outer.ContainsKey(key) || inner.ContainsKey(key);
            }

            public ICollection<string> Keys
            {
                get { return new List<string>(inner.Keys.Union(outer.Keys)).AsReadOnly(); }
            }

            public bool Remove(string key)
            {
                return inner.Remove(key) | outer.Remove(key);   // NOTE: | not ||
            }

            public bool TryGetValue(string key, out string[] value)
            {
                return outer.TryGetValue(key, out value) || inner.TryGetValue(key, out value);
            }

            public ICollection<string[]> Values
            {
                get { return new List<string[]>(this.Keys.Select(k => this[k])).AsReadOnly(); }
            }

            public string[] this[string key]
            {
                get
                {
                    string[] result;
                    if (outer.TryGetValue(key, out result))
                    {
                        return result;
                    }

                    return inner[key];
                }
                set
                {
                    inner[key] = outer[key] = value;
                }
            }

            #endregion

            #region ICollection<KeyValuePair<string,string[]>> Members

            public void Add(KeyValuePair<string, string[]> item)
            {
                outer.Add(item);
            }

            public void Clear()
            {
                outer.Clear();
                inner.Clear();
            }

            public bool Contains(KeyValuePair<string, string[]> item)
            {
                string[] value;
                return ((outer.TryGetValue(item.Key, out value) || inner.TryGetValue(item.Key, out value)) && value == item.Value);
            }

            public void CopyTo(KeyValuePair<string, string[]>[] array, int arrayIndex)
            {
                foreach (var pair in this)
                {
                    array[arrayIndex++] = pair;
                }
            }

            public int Count
            {
                get { return inner.Keys.Union(outer.Keys).Count(); }
            }

            public bool IsReadOnly
            {
                get { return false; }
            }

            public bool Remove(KeyValuePair<string, string[]> item)
            {
                string[] value;
                if (TryGetValue(item.Key, out value) && value == item.Value)
                {
                    return Remove(item.Key);
                }

                return false;
            }

            #endregion

            #region IEnumerable<KeyValuePair<string,string[]>> Members

            public IEnumerator<KeyValuePair<string, string[]>> GetEnumerator()
            {
                foreach (var key in inner.Keys.Union(outer.Keys))
                {
                    yield return new KeyValuePair<string, string[]>(key, this[key]);
                }
            }

            #endregion

            #region IEnumerable Members

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }

            #endregion
        }
    }
}
