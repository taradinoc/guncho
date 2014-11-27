using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace Guncho.Api
{
    public sealed class FileResult : IHttpActionResult
    {
        public HttpRequestMessage Request { get; private set; }
        public string Path { get; private set; }
        public string ContentType { get; private set; }

        public FileResult(HttpRequestMessage request, string path, string contentType = null)
        {
            Contract.Requires(request != null);
            Contract.Requires(path != null);
            Contract.Ensures(this.Request == request);
            Contract.Ensures(this.Path == path);

            this.Request = request;
            this.Path = path;
            this.ContentType = contentType ?? MimeMapping.GetMimeMapping(path);
        }

        [ContractInvariantMethod]
        private void ContractInvariant()
        {
            Contract.Invariant(Request != null);
            Contract.Invariant(Path != null);
            Contract.Invariant(ContentType != null);
        }

        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            var stream = File.OpenRead(Path);

            var response = new HttpResponseMessage(HttpStatusCode.OK);

            if (Request.Headers.Range != null)
            {
                response.Content = new ByteRangeStreamContent(stream, Request.Headers.Range, ContentType);
            }
            else
            {
                response.Content = new StreamContent(stream);
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(ContentType);
            }

            return Task.FromResult(response);
        }
    }
}
