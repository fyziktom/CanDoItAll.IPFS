using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Ipfs.Engine.Client.Transport
{
    internal sealed class MultipartFilePart
    {
        public MultipartFilePart(Stream stream, string formFieldName, string fileName, string? contentType = null)
        {
            Stream = stream;
            FormFieldName = formFieldName;
            FileName = fileName;
            ContentType = contentType;
        }

        public Stream Stream { get; }

        public string FormFieldName { get; }

        public string FileName { get; }

        public string? ContentType { get; }
    }

    internal static class MultipartRequestFactory
    {
        public static MultipartFormDataContent CreateFile(Stream stream, string formFieldName, string fileName, string? contentType = null)
        {
            var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(new NonOwningStream(stream));
            if (!string.IsNullOrWhiteSpace(contentType))
            {
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            }

            content.Add(fileContent, formFieldName, fileName);
            return content;
        }

        public static MultipartFormDataContent CreateFiles(IEnumerable<MultipartFilePart> files, IEnumerable<KeyValuePair<string, string>>? formValues = null)
        {
            var content = new MultipartFormDataContent();

            if (formValues != null)
            {
                foreach (var pair in formValues)
                {
                    content.Add(new StringContent(pair.Value ?? string.Empty), pair.Key);
                }
            }

            foreach (var file in files)
            {
                var fileContent = new StreamContent(new NonOwningStream(file.Stream));
                if (!string.IsNullOrWhiteSpace(file.ContentType))
                {
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                }

                content.Add(fileContent, file.FormFieldName, file.FileName);
            }

            return content;
        }
    }
}
