using System;
using System.Collections.Generic;
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
        private readonly string path, contentType;

        public FileResult(string path, string contentType = null)
        {
            this.path = path;
            this.contentType = contentType;
        }

        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(File.OpenRead(path)),
            };

            var contentType = this.contentType ?? MimeMapping.GetMimeMapping(path);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            return Task.FromResult(response);
        }
    }
}
