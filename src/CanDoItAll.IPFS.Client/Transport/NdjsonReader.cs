using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ipfs.Engine.Client.Transport
{
    internal static class NdjsonReader
    {
        public static async Task ReadAsync(Stream stream, Func<string, Task> onLine, CancellationToken cancellationToken)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }
            if (onLine == null)
            {
                throw new ArgumentNullException(nameof(onLine));
            }

            var buffer = ArrayPool<byte>.Shared.Rent(4096);
            try
            {
                using (var lineBuffer = new MemoryStream())
                {
                    while (true)
                    {
                        var count = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                        if (count == 0)
                        {
                            if (lineBuffer.Length > 0)
                            {
                                await EmitAsync(lineBuffer, onLine).ConfigureAwait(false);
                            }
                            return;
                        }

                        var segmentStart = 0;
                        for (var i = 0; i < count; ++i)
                        {
                            if (buffer[i] != (byte)'\n')
                            {
                                continue;
                            }

                            lineBuffer.Write(buffer, segmentStart, i - segmentStart);
                            await EmitAsync(lineBuffer, onLine).ConfigureAwait(false);
                            lineBuffer.SetLength(0);
                            segmentStart = i + 1;
                        }

                        if (segmentStart < count)
                        {
                            lineBuffer.Write(buffer, segmentStart, count - segmentStart);
                        }
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static Task EmitAsync(MemoryStream lineBuffer, Func<string, Task> onLine)
        {
            var line = Encoding.UTF8.GetString(lineBuffer.ToArray()).TrimEnd('\r');
            if (line.Length == 0)
            {
                return Task.CompletedTask;
            }

            return onLine(line);
        }
    }
}
